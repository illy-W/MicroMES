using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.MachineStates;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Services;

public class WorkOrderService(
    ProductionDataLoader loader,
    ProductionOrderStatusUpdater orderStatus,
    ScrapEntryResolver scrapResolver,
    IUnitOfWork unitOfWork)
{
    // Repositories always come from the unit of work (same scope, same DbContext, same transaction).
    private IWorkOrderRepository WorkOrders => unitOfWork.GetRepository<IWorkOrderRepository>();
    private IWorkOrderResponseRepository Sessions => unitOfWork.GetRepository<IWorkOrderResponseRepository>();
    private IWorkOrderResponseScrapRepository ScrapRows => unitOfWork.GetRepository<IWorkOrderResponseScrapRepository>();
    private IWorkOrderResponseCorrectionRepository CorrectionRows => unitOfWork.GetRepository<IWorkOrderResponseCorrectionRepository>();

    public async Task<ProductionOrderResult<List<WorkOrderModel>>> GetAllAsync(
        WorkOrderStatus? status, int? productionOrderId, string? search)
    {
        var data = await loader.LoadAllAsync();
        var term = search?.Trim();

        var items = data.WorkOrders.Values
            .Where(w => (status is null || w.Status == status)
                        && (productionOrderId is null || w.ProductionOrderId == productionOrderId))
            .Select(data.ToWorkOrderModel)
            .Where(m => string.IsNullOrEmpty(term)
                        || Contains(m.Code, term) || Contains(m.ProcessSegmentCode, term) || Contains(m.ProcessSegmentName, term)
                        || Contains(m.MaterialCode, term) || Contains(m.MaterialName, term))
            .OrderBy(m => m.ProductionOrderCode, StringComparer.Ordinal).ThenBy(m => m.OperationNumber)
            .ToList();
        return ProductionOrderResult<List<WorkOrderModel>>.Ok(items);
    }

    public async Task<ProductionOrderResult<WorkOrderDetailModel>> GetAsync(int id)
    {
        var data = await loader.LoadFamilyByWorkOrderAsync(id);
        return data is null || !data.WorkOrders.TryGetValue(id, out var wo)
            ? ProductionOrderResult<WorkOrderDetailModel>.NotFound("Ordine di lavoro non trovato.")
            : ProductionOrderResult<WorkOrderDetailModel>.Ok(data.ToWorkOrderDetail(wo));
    }

    public Task<ProductionOrderResult<WorkOrderDetailModel>> StartAsync(int id)
        => ExecuteAsync(id, WorkOrderTrigger.Start, m => m.Start());

    public Task<ProductionOrderResult<WorkOrderDetailModel>> PauseAsync(int id, CloseSessionRequest request)
        => ExecuteAsync(id, WorkOrderTrigger.Pause, m => m.Pause(request.GoodQuantity, request.ScrapQuantity, request.Notes), request);

    public Task<ProductionOrderResult<WorkOrderDetailModel>> ResumeAsync(int id)
        => ExecuteAsync(id, WorkOrderTrigger.Resume, m => m.Resume());

    public Task<ProductionOrderResult<WorkOrderDetailModel>> StopAsync(int id, CloseSessionRequest request)
        => ExecuteAsync(id, WorkOrderTrigger.Stop, m => m.Stop(request.GoodQuantity, request.ScrapQuantity, request.Notes), request);

    public Task<ProductionOrderResult<WorkOrderDetailModel>> AbortAsync(int id, string? notes)
        => ExecuteAsync(id, WorkOrderTrigger.Abort, m => m.Abort(notes));

    private async Task<ProductionOrderResult<WorkOrderDetailModel>> ExecuteAsync(
        int id, WorkOrderTrigger trigger, Action<WorkOrderMachineState> fire, CloseSessionRequest? close = null)
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

        try
        {
            unitOfWork.BeginTransaction();
            await ApplyAsync(data, wo, fire, scrap);
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
    /// Fires a transition through the machine and persists its effects (work order, sessions, unlocked successors,
    /// order status). The caller owns the transaction and has already checked CanFire.
    /// </summary>
    public async Task ApplyAsync(ProductionData data, WorkOrder wo, Action<WorkOrderMachineState> fire,
        IReadOnlyList<ResolvedScrapEntry>? scrap = null)
    {
        var machine = data.MachineOf(wo);
        fire(machine);

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
            NewScrapDetail = ScrapEntryRules.Describe(resolution.Entries.Select(r => (r.ReasonName, r.Quantity)))
        };

        try
        {
            unitOfWork.BeginTransaction();
            session.GoodQuantity = request.GoodQuantity;
            session.ScrapQuantity = request.ScrapQuantity;
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
