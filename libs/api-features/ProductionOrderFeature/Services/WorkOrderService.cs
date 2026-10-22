using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.EquipmentFeature.Services;
using MicroMES.ApiFeatures.ProductionOrderFeature.MachineStates;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;
using MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Services;

public class WorkOrderService(
    ProductionDataLoader loader,
    ProductionOrderStatusUpdater orderStatus,
    ScrapEntryResolver scrapResolver,
    EquipmentSelector selector,
    IUnitOfWork unitOfWork)
{
    // Repositories always come from the unit of work (same scope, same DbContext, same transaction).
    private IWorkOrderRepository WorkOrders => unitOfWork.GetRepository<IWorkOrderRepository>();
    private IWorkOrderResponseRepository Sessions => unitOfWork.GetRepository<IWorkOrderResponseRepository>();
    private IWorkOrderResponseScrapRepository ScrapRows => unitOfWork.GetRepository<IWorkOrderResponseScrapRepository>();
    private IWorkOrderResponseCorrectionRepository CorrectionRows => unitOfWork.GetRepository<IWorkOrderResponseCorrectionRepository>();

    public async Task<ProductionOrderResult<List<WorkOrderModel>>> GetAllAsync(
        WorkOrderStatus? status, int? productionOrderId, string? search, int? equipmentId = null,
        string? sort = null, PlanStatus? planStatus = null, bool? locked = null)
    {
        if (!string.IsNullOrWhiteSpace(sort) && !sort.Equals("plannedStart", StringComparison.OrdinalIgnoreCase) && !sort.Equals("code", StringComparison.OrdinalIgnoreCase))
            return ProductionOrderResult<List<WorkOrderModel>>.Invalid($"Ordinamento '{sort}' non valido: usa 'code' (predefinito) o 'plannedStart'.");
        var data = await loader.LoadAllAsync();
        var term = search?.Trim();

        var items = data.WorkOrders.Values
            .Where(w => (status is null || w.Status == status)
                        && (productionOrderId is null || w.ProductionOrderId == productionOrderId)
                        && (equipmentId is null || w.AssignedEquipmentId == equipmentId))
            .Select(data.ToWorkOrderModel)
            .Where(m => planStatus is null || m.PlanStatus == planStatus)
            .Where(m => locked is not true || m.IsPlanLocked)
            .Where(m => string.IsNullOrEmpty(term)
                        || Contains(m.Code, term) || Contains(m.ProcessSegmentCode, term) || Contains(m.ProcessSegmentName, term)
                        || Contains(m.MaterialCode, term) || Contains(m.MaterialName, term));
        // plannedStart: the queue of the machines (earliest planned start first, work orders without a plan last); same order code/operation otherwise.
        var ordered = string.Equals(sort, "plannedStart", StringComparison.OrdinalIgnoreCase)
            ? items.OrderBy(m => m.PlannedStartAt is null).ThenBy(m => m.PlannedStartAt)
                .ThenBy(m => m.ProductionOrderCode, StringComparer.Ordinal).ThenBy(m => m.OperationNumber)
            : items.OrderBy(m => m.ProductionOrderCode, StringComparer.Ordinal).ThenBy(m => m.OperationNumber);
        return ProductionOrderResult<List<WorkOrderModel>>.Ok(ordered.ToList());
    }

    public async Task<ProductionOrderResult<WorkOrderDetailModel>> GetAsync(int id)
    {
        var data = await loader.LoadFamilyByWorkOrderAsync(id);
        return data is null || !data.WorkOrders.TryGetValue(id, out var wo)
            ? ProductionOrderResult<WorkOrderDetailModel>.NotFound("Ordine di lavoro non trovato.")
            : ProductionOrderResult<WorkOrderDetailModel>.Ok(data.ToWorkOrderDetail(wo));
    }

    public Task<ProductionOrderResult<WorkOrderDetailModel>> StartAsync(int id, OpenSessionRequest? request = null)
        => ExecuteAsync(id, WorkOrderTrigger.Start, m => m.Start(), open: request ?? new OpenSessionRequest());

    public Task<ProductionOrderResult<WorkOrderDetailModel>> PauseAsync(int id, CloseSessionRequest request)
        => ExecuteAsync(id, WorkOrderTrigger.Pause, m => m.Pause(request.GoodQuantity, request.ScrapQuantity, request.Notes), request);

    public Task<ProductionOrderResult<WorkOrderDetailModel>> ResumeAsync(int id, OpenSessionRequest? request = null)
        => ExecuteAsync(id, WorkOrderTrigger.Resume, m => m.Resume(), open: request ?? new OpenSessionRequest());

    public Task<ProductionOrderResult<WorkOrderDetailModel>> StopAsync(int id, CloseSessionRequest request)
        => ExecuteAsync(id, WorkOrderTrigger.Stop, m => m.Stop(request.GoodQuantity, request.ScrapQuantity, request.Notes), request);

    public Task<ProductionOrderResult<WorkOrderDetailModel>> AbortAsync(int id, string? notes)
        => ExecuteAsync(id, WorkOrderTrigger.Abort, m => m.Abort(notes));

    private async Task<ProductionOrderResult<WorkOrderDetailModel>> ExecuteAsync(
        int id, WorkOrderTrigger trigger, Action<WorkOrderMachineState> fire,
        CloseSessionRequest? close = null, OpenSessionRequest? open = null)
    {
        var data = await loader.LoadFamilyByWorkOrderAsync(id);
        if (data is null || !data.WorkOrders.TryGetValue(id, out var wo))
            return ProductionOrderResult<WorkOrderDetailModel>.NotFound("Ordine di lavoro non trovato.");

        var machine = data.MachineOf(wo);
        if (!machine.CanFire(trigger))
            return ProductionOrderResult<WorkOrderDetailModel>.Conflict(machine.DeniedMessage(trigger));

        List<ResolvedScrapEntry>? scrap = null;
        if (close is not null)
        {
            var resolution = await scrapResolver.ResolveAsync(close.ScrapQuantity, close.ScrapEntries, wo.ProcessSegmentId);
            if (!resolution.IsValid) return ProductionOrderResult<WorkOrderDetailModel>.Invalid(resolution.Error!);
            scrap = resolution.Entries;
        }

        // Start and resume open a session: the machine is chosen (and validated) before anything is written.
        Equipment? equipment = null;
        if (open is not null)
        {
            var choice = await ChooseMachineAsync(data, wo, trigger, open.EquipmentId);
            if (choice.Error is not null) return ProductionOrderResult<WorkOrderDetailModel>.Invalid(choice.Error);
            equipment = choice.Equipment;
        }

        try
        {
            unitOfWork.BeginTransaction();
            await ApplyAsync(data, wo, fire, scrap, equipment);
            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return ProductionOrderResult<WorkOrderDetailModel>.Failed(ProductionOrderFailure.None, $"Impossibile completare il comando: {ex.Message}");
        }

        return ProductionOrderResult<WorkOrderDetailModel>.Ok(data.ToWorkOrderDetail(wo));
    }

    /// <summary>
    /// Machine of the session being opened: the requested one (validated), else the assigned one (start) or the one of the
    /// last session (resume, fallback assigned) when still enabled. Required only when the phase has active linked machines.
    /// </summary>
    private async Task<(Equipment? Equipment, string? Error)> ChooseMachineAsync(
        ProductionData data, WorkOrder wo, WorkOrderTrigger trigger, int? requested)
    {
        if (requested is { } id) return await selector.ValidateAsync(wo.ProcessSegmentId, id);

        var machines = await selector.MachinesOfSegmentAsync(wo.ProcessSegmentId);
        var inferred = trigger == WorkOrderTrigger.Resume
            ? data.SessionsOf(wo.Id).LastOrDefault()?.EquipmentId ?? wo.AssignedEquipmentId
            : wo.AssignedEquipmentId;
        if (inferred is { } known && EquipmentSelector.IsEnabled(machines, known))
            return (machines.First(m => m.Equipment.Id == known).Equipment, null);
        if (!EquipmentSelector.RequiresMachine(machines)) return (null, null);

        var stale = inferred is { } old ? machines.FirstOrDefault(m => m.Equipment.Id == old)?.Equipment : null;
        return (null, stale is null
            ? EquipmentSelector.ChooseMachineMessage
            : $"Scegli la macchina: {stale.Code} non è più abilitata per questa fase.");
    }

    /// <summary>
    /// Assigns (or removes, with a null machine) the planned machine of a work order that is Waiting, Ready or Interrupted.
    /// Not a state transition: the work order status does not change.
    /// </summary>
    public async Task<ProductionOrderResult<WorkOrderDetailModel>> AssignEquipmentAsync(int id, AssignEquipmentRequest request)
    {
        var data = await loader.LoadFamilyByWorkOrderAsync(id);
        if (data is null || !data.WorkOrders.TryGetValue(id, out var wo))
            return ProductionOrderResult<WorkOrderDetailModel>.NotFound("Ordine di lavoro non trovato.");
        if (wo.Status is not (WorkOrderStatus.Waiting or WorkOrderStatus.Ready or WorkOrderStatus.Interrupted))
            return ProductionOrderResult<WorkOrderDetailModel>.Conflict(
                $"Il lavoro è in stato '{WorkOrderMachineState.StatusLabel(wo.Status)}': la macchina si assegna solo a un lavoro In attesa, Pronto o Interrotto.");

        Equipment? equipment = null;
        if (request.EquipmentId is { } equipmentId)
        {
            var choice = await selector.ValidateAsync(wo.ProcessSegmentId, equipmentId);
            if (choice.Error is not null) return ProductionOrderResult<WorkOrderDetailModel>.Invalid(choice.Error);
            equipment = choice.Equipment;
        }

        try
        {
            unitOfWork.BeginTransaction();
            wo.AssignedEquipmentId = equipment?.Id;
            wo.UpdatedAt = data.TimeProvider.GetUtcNow().UtcDateTime;
            Persist.Ensure(await WorkOrders.UpdateAsync(wo), "Impossibile aggiornare l'ordine di lavoro.");
            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return ProductionOrderResult<WorkOrderDetailModel>.Failed(ProductionOrderFailure.None, $"Impossibile assegnare la macchina: {ex.Message}");
        }

        if (equipment is not null) Remember(data, wo, equipment);
        return ProductionOrderResult<WorkOrderDetailModel>.Ok(data.ToWorkOrderDetail(wo));
    }

    /// <summary>Locks the current plan (start, end, machine unchanged). 409 when not planned or not lockable.</summary>
    public Task<ProductionOrderResult<WorkOrderDetailModel>> LockPlanAsync(int id) => SetPlanLockAsync(id, true);

    /// <summary>Removes the lock; nothing else changes.</summary>
    public Task<ProductionOrderResult<WorkOrderDetailModel>> UnlockPlanAsync(int id) => SetPlanLockAsync(id, false);

    private async Task<ProductionOrderResult<WorkOrderDetailModel>> SetPlanLockAsync(int id, bool locked)
    {
        var data = await loader.LoadFamilyByWorkOrderAsync(id);
        if (data is null || !data.WorkOrders.TryGetValue(id, out var wo))
            return ProductionOrderResult<WorkOrderDetailModel>.NotFound("Ordine di lavoro non trovato.");

        if (locked)
        {
            if (data.PlanCommandDenial(wo, "si fissa") is { } denial) return ProductionOrderResult<WorkOrderDetailModel>.Conflict(denial);
            if (wo.IsPlanLocked) return ProductionOrderResult<WorkOrderDetailModel>.Conflict("Il lavoro è già fissato.");
            if (wo.PlannedStartAt is null || wo.PlannedEndAt is null)
                return ProductionOrderResult<WorkOrderDetailModel>.Conflict("Il lavoro non è pianificato: pianifica il lavoro oppure scegli un orario con Sposta.");
            if (!data.IsPlanLockable(wo))
                return ProductionOrderResult<WorkOrderDetailModel>.Conflict("Il lavoro non ha una macchina assegnata: assegna la macchina oppure scegli macchina e orario con Sposta.");
        }
        else if (!wo.IsPlanLocked)
            return ProductionOrderResult<WorkOrderDetailModel>.Conflict("Il lavoro non è fissato: non c'è nulla da sbloccare.");

        try
        {
            unitOfWork.BeginTransaction();
            var now = data.TimeProvider.GetUtcNow().UtcDateTime;
            wo.IsPlanLocked = locked;
            wo.PlanLockedAt = locked ? now : null;
            wo.UpdatedAt = now;
            Persist.Ensure(await WorkOrders.UpdateAsync(wo), "Impossibile aggiornare l'ordine di lavoro.");
            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return ProductionOrderResult<WorkOrderDetailModel>.Failed(ProductionOrderFailure.None,
                $"Impossibile {(locked ? "fissare" : "sbloccare")} il lavoro: {ex.Message}");
        }
        return ProductionOrderResult<WorkOrderDetailModel>.Ok(data.ToWorkOrderDetail(wo));
    }

    private static void Remember(ProductionData data, WorkOrder wo, Equipment equipment)
    {
        data.Equipment[equipment.Id] = equipment;
        data.EquipmentLinks.Add((equipment.Id, wo.ProcessSegmentId));
    }

    /// <summary>
    /// Fires a transition through the machine and persists its effects (work order, sessions, unlocked successors,
    /// order status). The caller owns the transaction and has already checked CanFire.
    /// </summary>
    public async Task ApplyAsync(ProductionData data, WorkOrder wo, Action<WorkOrderMachineState> fire,
        IReadOnlyList<ResolvedScrapEntry>? scrap = null, Equipment? equipment = null)
    {
        var machine = data.MachineOf(wo);
        fire(machine);

        // The session records the machine used; the work order's assignment follows the reality.
        if (equipment is not null)
        {
            wo.AssignedEquipmentId = equipment.Id;
            Remember(data, wo, equipment);
            if (machine.OpenedSession is { } session)
            {
                session.EquipmentId = equipment.Id;
                session.EquipmentCode = equipment.Code;
                session.EquipmentName = equipment.Name;
            }
        }

        // The plan lock disappears when the work order starts or ends (also when its order is closed): the real dates take over.
        if (wo.Status is WorkOrderStatus.Running or WorkOrderStatus.Ended or WorkOrderStatus.Aborted && wo.IsPlanLocked)
        {
            wo.IsPlanLocked = false;
            wo.PlanLockedAt = null;
        }

        Persist.Ensure(await WorkOrders.UpdateAsync(wo), "Impossibile aggiornare l'ordine di lavoro.");
        if (machine.OpenedSession is { } opened)
        {
            var added = Persist.Ensure(await Sessions.AddAsync(opened), "Impossibile registrare la sessione di lavoro.");
            data.Sessions.Add(added);
        }
        if (machine.ClosedSession is { } closed)
        {
            Persist.Ensure(await Sessions.UpdateAsync(closed), "Impossibile aggiornare la sessione di lavoro.");
            foreach (var entry in scrap ?? [])
                data.Scraps.Add(Persist.Ensure(await ScrapRows.AddAsync(NewRow(closed.Id, entry)), "Impossibile registrare lo scarto."));
        }

        var order = data.Orders[wo.ProductionOrderId];
        switch (wo.Status)
        {
            case WorkOrderStatus.Running:
                await orderStatus.OnWorkOrderStartedAsync(data, order);
                break;
            case WorkOrderStatus.Ended:
                await UnlockSuccessorsAsync(data, wo);
                await orderStatus.OnWorkOrderEndedAsync(data, order);
                break;
        }
    }

    private static WorkOrderResponseScrap NewRow(int sessionId, ResolvedScrapEntry e) => new()
    {
        WorkOrderResponseId = sessionId, ScrapReasonId = e.ScrapReasonId, ReasonCode = e.ReasonCode,
        ReasonName = e.ReasonName, Quantity = e.Quantity
    };

    /// <summary>
    /// Corrects a closed session: new good/scrap/reasons replace the previous ones and a trace row keeps both.
    /// Work order and order statuses are not touched; totals and progress follow from the sessions.
    /// </summary>
    public async Task<ProductionOrderResult<WorkOrderDetailModel>> CorrectSessionAsync(
        int workOrderId, int responseId, CorrectSessionRequest request)
    {
        var data = await loader.LoadFamilyByWorkOrderAsync(workOrderId);
        if (data is null || !data.WorkOrders.TryGetValue(workOrderId, out var wo))
            return ProductionOrderResult<WorkOrderDetailModel>.NotFound("Ordine di lavoro non trovato.");

        var session = data.Sessions.FirstOrDefault(x => x.Id == responseId && x.WorkOrderId == workOrderId);
        if (session is null)
            return ProductionOrderResult<WorkOrderDetailModel>.NotFound("Sessione di lavoro non trovata.");
        if (session.EndedAt is null)
            return ProductionOrderResult<WorkOrderDetailModel>.Conflict(
                "La sessione è ancora in corso: non si corregge, si chiude con i valori giusti.");

        var order = data.Orders[wo.ProductionOrderId];
        if (order.Status is ProductionOrderStatus.Completed or ProductionOrderStatus.Cancelled)
            return ProductionOrderResult<WorkOrderDetailModel>.Conflict(
                $"L'ordine di produzione {order.Code} è {(order.Status == ProductionOrderStatus.Completed ? "completato" : "annullato")}: le sue sessioni non si possono più correggere.");

        var notes = request.Notes?.Trim();
        if (string.IsNullOrEmpty(notes))
            return ProductionOrderResult<WorkOrderDetailModel>.Invalid("La nota di correzione è obbligatoria.");

        var resolution = await scrapResolver.ResolveAsync(request.ScrapQuantity, request.ScrapEntries, wo.ProcessSegmentId);
        if (!resolution.IsValid) return ProductionOrderResult<WorkOrderDetailModel>.Invalid(resolution.Error!);

        Equipment? newEquipment = null;
        if (request.EquipmentId is { } requestedEquipment && requestedEquipment != session.EquipmentId)
        {
            var choice = await selector.ValidateAsync(wo.ProcessSegmentId, requestedEquipment);
            if (choice.Error is not null) return ProductionOrderResult<WorkOrderDetailModel>.Invalid(choice.Error);
            newEquipment = choice.Equipment;
        }

        var oldRows = data.Scraps.Where(e => e.WorkOrderResponseId == session.Id).ToList();
        var trace = new WorkOrderResponseCorrection
        {
            WorkOrderResponseId = session.Id,
            CorrectedAt = data.TimeProvider.GetUtcNow().UtcDateTime,
            Notes = notes,
            PreviousGoodQuantity = session.GoodQuantity,
            PreviousScrapQuantity = session.ScrapQuantity,
            PreviousScrapDetail = ScrapEntryRules.Describe(oldRows.Select(r => (r.ReasonName, r.Quantity))),
            NewGoodQuantity = request.GoodQuantity,
            NewScrapQuantity = request.ScrapQuantity,
            NewScrapDetail = ScrapEntryRules.Describe(resolution.Entries.Select(r => (r.ReasonName, r.Quantity))),
            PreviousEquipmentCode = session.EquipmentCode,
            PreviousEquipmentName = session.EquipmentName,
            NewEquipmentCode = newEquipment is null ? session.EquipmentCode : newEquipment.Code,
            NewEquipmentName = newEquipment is null ? session.EquipmentName : newEquipment.Name
        };

        try
        {
            unitOfWork.BeginTransaction();
            session.GoodQuantity = request.GoodQuantity;
            session.ScrapQuantity = request.ScrapQuantity;
            if (newEquipment is not null)
            {
                session.EquipmentId = newEquipment.Id;
                session.EquipmentCode = newEquipment.Code;
                session.EquipmentName = newEquipment.Name;
                Remember(data, wo, newEquipment);
            }
            Persist.Ensure(await Sessions.UpdateAsync(session), "Impossibile aggiornare la sessione di lavoro.");
            foreach (var row in oldRows)
            {
                if (!(await ScrapRows.DeleteAsync(row)).IsSuccessful)
                    throw new InvalidOperationException("Impossibile aggiornare lo scarto.");
                data.Scraps.Remove(row);
            }
            foreach (var entry in resolution.Entries)
                data.Scraps.Add(Persist.Ensure(await ScrapRows.AddAsync(NewRow(session.Id, entry)), "Impossibile registrare lo scarto."));
            data.Corrections.Add(Persist.Ensure(await CorrectionRows.AddAsync(trace), "Impossibile registrare la correzione."));
            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return ProductionOrderResult<WorkOrderDetailModel>.Failed(ProductionOrderFailure.None, $"Impossibile completare la correzione: {ex.Message}");
        }

        return ProductionOrderResult<WorkOrderDetailModel>.Ok(data.ToWorkOrderDetail(wo));
    }

    /// <summary>Waiting successors (also in other orders) become Ready when all their predecessors are Ended.</summary>
    private async Task UnlockSuccessorsAsync(ProductionData data, WorkOrder ended)
    {
        foreach (var successorId in data.SuccessorIds(ended.Id).Distinct().ToList())
        {
            var successor = data.WorkOrders[successorId];
            if (successor.Status != WorkOrderStatus.Waiting) continue;
            if (data.PredecessorIds(successorId).Any(p => data.WorkOrders[p].Status != WorkOrderStatus.Ended)) continue;

            data.MachineOf(successor).PredecessorsEnded();
            Persist.Ensure(await WorkOrders.UpdateAsync(successor), "Impossibile aggiornare l'ordine di lavoro.");
        }
    }

    private static bool Contains(string value, string term) => value.Contains(term, StringComparison.OrdinalIgnoreCase);
}
