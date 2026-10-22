using System.Text.RegularExpressions;
using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.BomFeature.Repositories;
using MicroMES.ApiFeatures.EquipmentFeature.Services;
using MicroMES.ApiFeatures.MaterialFeature.Repositories;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Repositories;
using MicroMES.ApiFeatures.ProductionOrderFeature.MachineStates;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;
using MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;
using MicroMES.ApiFeatures.RoutingFeature.Repositories;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Services;

public class ProductionOrderService(
    ProductionDataLoader loader,
    WorkOrderService workOrderService,
    EquipmentSelector selector,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private const string NotFoundMessage = "Ordine di produzione non trovato.";

    // Repositories always come from the unit of work (same scope, same DbContext, same transaction).
    private IProductionOrderRepository Orders => unitOfWork.GetRepository<IProductionOrderRepository>();
    private IWorkOrderRepository WorkOrders => unitOfWork.GetRepository<IWorkOrderRepository>();
    private IWorkOrderDependencyRepository Dependencies => unitOfWork.GetRepository<IWorkOrderDependencyRepository>();
    private IWorkOrderMaterialRepository Lines => unitOfWork.GetRepository<IWorkOrderMaterialRepository>();
    private IMaterialRepository Materials => unitOfWork.GetRepository<IMaterialRepository>();
    private IBomItemRepository BomItems => unitOfWork.GetRepository<IBomItemRepository>();
    private IRoutingStepRepository RoutingSteps => unitOfWork.GetRepository<IRoutingStepRepository>();
    private IRoutingStepDependencyRepository RoutingDependencies => unitOfWork.GetRepository<IRoutingStepDependencyRepository>();
    private IProcessSegmentRepository Segments => unitOfWork.GetRepository<IProcessSegmentRepository>();

    // ---- queries ------------------------------------------------------------------------------------

    public async Task<ProductionOrderResult<List<ProductionOrderModel>>> GetAllAsync(
        ProductionOrderStatus? status, int? materialId, string? search, bool includeChildren)
    {
        var data = await loader.LoadAllAsync();
        var term = search?.Trim();

        var items = data.Orders.Values
            .Where(o => (includeChildren || o.ParentOrderId is null)
                        && (status is null || o.Status == status)
                        && (materialId is null || o.MaterialId == materialId))
            .Select(data.ToOrderModel)
            .Where(m => string.IsNullOrEmpty(term)
                        || m.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || m.MaterialCode.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || m.MaterialName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.Priority).ThenBy(m => m.Code, StringComparer.Ordinal)
            .ToList();
        return ProductionOrderResult<List<ProductionOrderModel>>.Ok(items);
    }

    public async Task<ProductionOrderResult<ProductionOrderDetailModel>> GetAsync(int id)
    {
        var data = await loader.LoadFamilyAsync(id);
        return data is null
            ? ProductionOrderResult<ProductionOrderDetailModel>.NotFound(NotFoundMessage)
            : ProductionOrderResult<ProductionOrderDetailModel>.Ok(data.ToOrderDetail(data.Orders[id]));
    }

    // ---- create / update ----------------------------------------------------------------------------

    public async Task<ProductionOrderResult<ProductionOrderDetailModel>> CreateAsync(CreateProductionOrderRequest request)
    {
        var material = await Materials.GetSingleAsync(request.MaterialId!.Value);
        if (material is null) return ProductionOrderResult<ProductionOrderDetailModel>.NotFound("Materiale non trovato.");
        if (!material.IsActive) return ProductionOrderResult<ProductionOrderDetailModel>.Invalid("Il materiale non è attivo.");
        if (material.Type is not (MaterialType.Intermediate or MaterialType.FinishedGood))
            return ProductionOrderResult<ProductionOrderDetailModel>.Invalid("Si può produrre solo un materiale di tipo semilavorato (Intermediate) o prodotto finito (FinishedGood).");

        var start = NormalizeUtc(request.RequestedStart);
        var end = NormalizeUtc(request.RequestedEnd);
        if (start is not null && end is not null && end < start)
            return ProductionOrderResult<ProductionOrderDetailModel>.Invalid("La fine richiesta non può precedere l'inizio richiesto.");

        // The code is max+1, computed inside the transaction. Two concurrent creations can still pick the same
        // one: the unique index rejects the loser, which fails cleanly (the request scope is not reused, so no
        // retry here) and is answered 409 "esiste già" by the constraint-violation mapping.
        ProductionOrder order;
        try
        {
            unitOfWork.BeginTransaction();
            var now = Now();
            var code = await NextCodeAsync();
            order = Persist.Ensure(await Orders.AddAsync(new ProductionOrder
            {
                Code = code,
                MaterialId = material.Id,
                Quantity = request.Quantity!.Value,
                Status = ProductionOrderStatus.Planned,
                Priority = request.Priority,
                RequestedStart = start,
                RequestedEnd = end,
                Notes = request.Notes,
                CreatedAt = now,
                UpdatedAt = now
            }), "Impossibile creare l'ordine di produzione.");
            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return ProductionOrderResult<ProductionOrderDetailModel>.Failed(ProductionOrderFailure.None, ex.Message);
        }

        return await GetAsync(order.Id);
    }

    public async Task<ProductionOrderResult<ProductionOrderDetailModel>> UpdateAsync(int id, UpdateProductionOrderRequest request)
    {
        var order = await Orders.GetSingleAsync(id);
        if (order is null) return ProductionOrderResult<ProductionOrderDetailModel>.NotFound(NotFoundMessage);
        if (ChildGuard(order) is { } childError) return childError;
        if (order.Status != ProductionOrderStatus.Planned)
            return ProductionOrderResult<ProductionOrderDetailModel>.Conflict(
                $"L'ordine è in stato '{ProductionOrderMachineState.StatusLabel(order.Status)}': si può modificare solo un ordine Pianificato.");

        var start = NormalizeUtc(request.RequestedStart);
        var end = NormalizeUtc(request.RequestedEnd);
        if (start is not null && end is not null && end < start)
            return ProductionOrderResult<ProductionOrderDetailModel>.Invalid("La fine richiesta non può precedere l'inizio richiesto.");

        order.Quantity = request.Quantity!.Value;
        order.Priority = request.Priority;
        order.RequestedStart = start;
        order.RequestedEnd = end;
        order.Notes = request.Notes;
        order.UpdatedAt = Now();

        var saved = await Orders.UpdateAsync(order);
        if (!saved.IsSuccessful)
            return ProductionOrderResult<ProductionOrderDetailModel>.Failed(ProductionOrderFailure.None, string.Join("; ", saved.Errors));
        return await GetAsync(id);
    }

    // ---- release ------------------------------------------------------------------------------------

    public async Task<ProductionOrderResult<ProductionOrderDetailModel>> ReleaseAsync(int id)
    {
        var data = await loader.LoadFamilyAsync(id);
        if (data is null) return ProductionOrderResult<ProductionOrderDetailModel>.NotFound(NotFoundMessage);
        var order = data.Orders[id];
        if (ChildGuard(order) is { } childError) return childError;

        var machine = data.MachineOf(order);
        if (!machine.CanFire(ProductionOrderTrigger.Release))
            return ProductionOrderResult<ProductionOrderDetailModel>.Conflict(machine.DeniedMessage(ProductionOrderTrigger.Release));

        var catalog = await LoadCatalogAsync(order.MaterialId);
        var plan = ProductionOrderPlanner.Plan(order.MaterialId, order.Quantity, catalog);
        if (plan.Order is null) return ProductionOrderResult<ProductionOrderDetailModel>.Invalid(plan.Message!);

        try
        {
            unitOfWork.BeginTransaction();
            var created = new Dictionary<PlannedWorkOrder, WorkOrder>();
            var machines = await selector.MachinesBySegmentAsync();
            await PersistPlanAsync(data, plan.Order, order, created, machines);
            await ReadyStartStepsAsync(data, created.Values);
            data.MachineOf(order).Fire(ProductionOrderTrigger.Release);
            Persist.Ensure(await Orders.UpdateAsync(order), "Impossibile aggiornare l'ordine di produzione.");
            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return ProductionOrderResult<ProductionOrderDetailModel>.Failed(ProductionOrderFailure.None, $"Impossibile rilasciare l'ordine: {ex.Message}");
        }

        return ProductionOrderResult<ProductionOrderDetailModel>.Ok(data.ToOrderDetail(order));
    }

    private async Task<PlannerCatalog> LoadCatalogAsync(int rootMaterialId)
    {
        var bom = new Dictionary<int, List<BomItem>>();
        await foreach (var b in BomItems.FetchAsync(_ => true))
        {
            if (!bom.TryGetValue(b.ParentMaterialId, out var list)) bom[b.ParentMaterialId] = list = [];
            list.Add(b);
        }

        // Materials reachable from the root through the BOM.
        var reachable = new HashSet<int>();
        var stack = new Stack<int>([rootMaterialId]);
        while (stack.Count > 0)
        {
            var m = stack.Pop();
            if (!reachable.Add(m)) continue;
            foreach (var b in bom.GetValueOrDefault(m) ?? []) stack.Push(b.ComponentMaterialId);
        }
        var ids = reachable.ToList();

        var catalog = new PlannerCatalog();
        await foreach (var m in Materials.FetchAsync(x => ids.Contains(x.Id)))
            catalog.Materials[m.Id] = new PlannerMaterial(m.Id, m.Code, m.Type, m.IsActive);
        foreach (var (parent, items) in bom.Where(kv => reachable.Contains(kv.Key)))
            catalog.Bom[parent] = items.OrderBy(i => i.Id).Select(i => new PlannerBomLine(i.ComponentMaterialId, i.Quantity, i.ConsumedInStepId)).ToList();

        var steps = new List<RoutingStep>();
        await foreach (var s in RoutingSteps.FetchAsync(x => ids.Contains(x.MaterialId))) steps.Add(s);
        var stepIds = steps.Select(s => s.Id).ToList();
        var segmentIds = steps.Select(s => s.ProcessSegmentId).Distinct().ToList();
        var segmentMap = new Dictionary<int, ProcessSegment>();
        await foreach (var seg in Segments.FetchAsync(x => segmentIds.Contains(x.Id))) segmentMap[seg.Id] = seg;
        var deps = new List<RoutingStepDependency>();
        await foreach (var d in RoutingDependencies.FetchAsync(x => stepIds.Contains(x.PredecessorStepId))) deps.Add(d);

        foreach (var group in steps.GroupBy(s => s.MaterialId))
        {
            var groupIds = group.Select(s => s.Id).ToHashSet();
            catalog.Routings[group.Key] = new PlannerRouting(
                group.OrderBy(s => s.OperationNumber).Select(s =>
                {
                    var seg = segmentMap[s.ProcessSegmentId];
                    return new PlannerStep(
                        s.Id, s.OperationNumber, seg.Id, seg.Code, seg.Name,
                        s.SetupMinutes ?? seg.SetupMinutes, s.RunMinutesPerUnit ?? seg.RunMinutesPerUnit, s.Notes);
                }).ToList(),
                deps.Where(d => groupIds.Contains(d.PredecessorStepId)).Select(d => (d.PredecessorStepId, d.SuccessorStepId)).ToList());
        }
        return catalog;
    }

    /// <summary>Creates work orders, child orders, materials and dependencies of a planned order (recursive).</summary>
    private async Task PersistPlanAsync(
        ProductionData data, PlannedOrder plan, ProductionOrder order, Dictionary<PlannedWorkOrder, WorkOrder> created,
        Dictionary<int, List<SegmentMachine>> machines)
    {
        var now = Now();
        foreach (var planned in plan.WorkOrders)
        {
            // Planned machine: the default of the phase, else its only active machine, else none (no arbitrary choice).
            var machinesOfPhase = machines.GetValueOrDefault(planned.Step.ProcessSegmentId);
            var assigned = EquipmentSelector.Pick(machinesOfPhase);
            var wo = Persist.Ensure(await WorkOrders.AddAsync(new WorkOrder
            {
                AssignedEquipmentId = assigned?.Id,
                ProductionOrderId = order.Id,
                OperationNumber = planned.OperationNumber,
                ProcessSegmentId = planned.Step.ProcessSegmentId,
                ProcessSegmentCode = planned.Step.ProcessSegmentCode,
                ProcessSegmentName = planned.Step.ProcessSegmentName,
                SetupMinutes = planned.Step.SetupMinutes,
                RunMinutesPerUnit = planned.Step.RunMinutesPerUnit,
                PlannedQuantity = planned.PlannedQuantity,
                Notes = planned.Step.Notes,
                Status = WorkOrderStatus.Waiting,
                CreatedAt = now,
                UpdatedAt = now
            }), "Impossibile creare un ordine di lavoro.");
            created[planned] = wo;
            data.WorkOrders[wo.Id] = wo;
            foreach (var m in machinesOfPhase ?? [])
                data.EquipmentLinks.Add((m.Equipment.Id, wo.ProcessSegmentId));
            if (assigned is not null) data.Equipment[assigned.Id] = assigned;
        }

        // Child orders (recursive): created as Planned, then released through the machine.
        var childEntities = new Dictionary<PlannedOrder, ProductionOrder>();
        var index = 0;
        foreach (var childPlan in plan.Children)
        {
            index++;
            var child = Persist.Ensure(await Orders.AddAsync(new ProductionOrder
            {
                Code = $"{order.Code}-{index}",
                MaterialId = childPlan.MaterialId,
                Quantity = childPlan.Quantity,
                Status = ProductionOrderStatus.Planned,
                Priority = order.Priority,
                ParentOrderId = order.Id,
                CreatedAt = now,
                UpdatedAt = now
            }), "Impossibile creare un ordine figlio.");
            data.Orders[child.Id] = child;
            if (!data.Materials.ContainsKey(child.MaterialId))
                data.Materials[child.MaterialId] = (await Materials.GetSingleAsync(child.MaterialId))!;
            childEntities[childPlan] = child;

            await PersistPlanAsync(data, childPlan, child, created, machines);

            data.MachineOf(child).Fire(ProductionOrderTrigger.Release);
            Persist.Ensure(await Orders.UpdateAsync(child), "Impossibile aggiornare un ordine figlio.");
        }

        // Required materials: on the work order of the step that consumes each one.
        foreach (var material in plan.Materials)
        {
            var target = created[material.ConsumerStep];
            var line = Persist.Ensure(await Lines.AddAsync(new WorkOrderMaterial
            {
                WorkOrderId = target.Id,
                MaterialId = material.MaterialId,
                RequiredQuantity = material.RequiredQuantity,
                ChildProductionOrderId = material.Child is null ? null : childEntities[material.Child].Id
            }), "Impossibile creare un materiale richiesto.");
            data.Lines.Add(line);
            if (!data.Materials.ContainsKey(line.MaterialId))
                data.Materials[line.MaterialId] = (await Materials.GetSingleAsync(line.MaterialId))!;
        }

        foreach (var (pred, succ) in plan.Dependencies)
        {
            var dep = Persist.Ensure(await Dependencies.AddAsync(new WorkOrderDependency
            {
                PredecessorWorkOrderId = created[pred].Id,
                SuccessorWorkOrderId = created[succ].Id
            }), "Impossibile creare una dipendenza.");
            data.Dependencies.Add(dep);
        }
    }

    /// <summary>Work orders with no predecessor become Ready (the others stay Waiting).</summary>
    private async Task ReadyStartStepsAsync(ProductionData data, IEnumerable<WorkOrder> created)
    {
        foreach (var wo in created)
        {
            if (data.PredecessorIds(wo.Id).Any()) continue;
            data.MachineOf(wo).PredecessorsEnded();
            Persist.Ensure(await WorkOrders.UpdateAsync(wo), "Impossibile aggiornare un ordine di lavoro.");
        }
    }

    // ---- cancel / abort -----------------------------------------------------------------------------

    public async Task<ProductionOrderResult<ProductionOrderDetailModel>> CancelAsync(int id)
    {
        var data = await loader.LoadFamilyAsync(id);
        if (data is null) return ProductionOrderResult<ProductionOrderDetailModel>.NotFound(NotFoundMessage);
        var order = data.Orders[id];
        if (ChildGuard(order) is { } childError) return childError;

        var machine = data.MachineOf(order);
        if (!machine.CanFire(ProductionOrderTrigger.Cancel))
            return ProductionOrderResult<ProductionOrderDetailModel>.Conflict(machine.DeniedMessage(ProductionOrderTrigger.Cancel));

        return await RunAsync(data, order, async () =>
        {
            // Children first, then the order itself; the work orders are aborted (the order has none started).
            foreach (var o in data.Subtree(id).OrderByDescending(x => x.Id))
            {
                await AbortOpenWorkOrdersAsync(data, o, null);
                var m = data.MachineOf(o);
                if (!m.CanFire(ProductionOrderTrigger.Cancel))
                    throw new InvalidOperationException(m.DeniedMessage(ProductionOrderTrigger.Cancel));
                m.Fire(ProductionOrderTrigger.Cancel);
                Persist.Ensure(await Orders.UpdateAsync(o), "Impossibile aggiornare l'ordine di produzione.");
            }
        });
    }

    public async Task<ProductionOrderResult<ProductionOrderDetailModel>> AbortAsync(int id, string? notes)
    {
        var data = await loader.LoadFamilyAsync(id);
        if (data is null) return ProductionOrderResult<ProductionOrderDetailModel>.NotFound(NotFoundMessage);
        var order = data.Orders[id];
        if (ChildGuard(order) is { } childError) return childError;

        var machine = data.MachineOf(order);
        if (!machine.CanFire(ProductionOrderTrigger.Abort))
            return ProductionOrderResult<ProductionOrderDetailModel>.Conflict(machine.DeniedMessage(ProductionOrderTrigger.Abort));

        return await RunAsync(data, order, async () =>
        {
            // The order and the children that are not terminal; every work order not Ended is aborted.
            foreach (var o in data.Subtree(id).OrderByDescending(x => x.Id))
            {
                var m = data.MachineOf(o);
                if (!m.CanFire(ProductionOrderTrigger.Abort)) continue;
                await AbortOpenWorkOrdersAsync(data, o, o.Id == id ? notes : null);
                m.Abort(o.Id == id ? notes : null);
                Persist.Ensure(await Orders.UpdateAsync(o), "Impossibile aggiornare l'ordine di produzione.");
            }
        });
    }

    private async Task AbortOpenWorkOrdersAsync(ProductionData data, ProductionOrder order, string? notes)
    {
        foreach (var wo in data.WorkOrdersOf(order.Id).Where(w => w.Status is not (WorkOrderStatus.Ended or WorkOrderStatus.Aborted)).ToList())
            await workOrderService.ApplyAsync(data, wo, m => m.Abort(notes));
    }

    private async Task<ProductionOrderResult<ProductionOrderDetailModel>> RunAsync(
        ProductionData data, ProductionOrder order, Func<Task> work)
    {
        try
        {
            unitOfWork.BeginTransaction();
            await work();
            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return ProductionOrderResult<ProductionOrderDetailModel>.Failed(ProductionOrderFailure.None, $"Impossibile completare il comando: {ex.Message}");
        }
        return ProductionOrderResult<ProductionOrderDetailModel>.Ok(data.ToOrderDetail(order));
    }

    // ---- bulk plan lock -----------------------------------------------------------------------------

    /// <summary>
    /// Locks the current plan of every open work order of the order (and of its descendants when asked) that can be locked
    /// (planned, machine assigned when the phase has machines, status and order allowed); the others are skipped with a reason.
    /// All or nothing (one transaction). 409 when the order itself is not Released/InProgress.
    /// </summary>
    public async Task<ProductionOrderResult<LockAllResultModel>> LockAllAsync(int id, bool includeChildren)
    {
        var data = await loader.LoadFamilyAsync(id);
        if (data is null) return ProductionOrderResult<LockAllResultModel>.NotFound(NotFoundMessage);
        if (BulkDenial(data.Orders[id], "si fissano") is { } denial) return ProductionOrderResult<LockAllResultModel>.Conflict(denial);

        var skipped = new List<LockSkippedWorkOrderModel>();
        var toLock = new List<WorkOrder>();
        foreach (var wo in data.OpenWorkOrdersOf(id, includeChildren).Where(w => !w.IsPlanLocked))
        {
            if (data.LockSkipReason(wo) is { } reason)
                skipped.Add(new LockSkippedWorkOrderModel { WorkOrderId = wo.Id, Code = ProductionData.CodeOf(data.Orders[wo.ProductionOrderId], wo), Reason = reason });
            else toLock.Add(wo);
        }

        try
        {
            unitOfWork.BeginTransaction();
            var now = Now();
            foreach (var wo in toLock)
            {
                wo.IsPlanLocked = true;
                wo.PlanLockedAt = now;
                wo.UpdatedAt = now;
                Persist.Ensure(await WorkOrders.UpdateAsync(wo), "Impossibile aggiornare un ordine di lavoro.");
            }
            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return ProductionOrderResult<LockAllResultModel>.Failed(ProductionOrderFailure.None, $"Impossibile fissare i lavori: {ex.Message}");
        }
        return ProductionOrderResult<LockAllResultModel>.Ok(new LockAllResultModel { Locked = toLock.Count, Skipped = skipped });
    }

    /// <summary>Removes the lock from every locked open work order of the order (and of its descendants when asked); nothing else changes.</summary>
    public async Task<ProductionOrderResult<UnlockAllResultModel>> UnlockAllAsync(int id, bool includeChildren)
    {
        var data = await loader.LoadFamilyAsync(id);
        if (data is null) return ProductionOrderResult<UnlockAllResultModel>.NotFound(NotFoundMessage);
        if (BulkDenial(data.Orders[id], "si sbloccano") is { } denial) return ProductionOrderResult<UnlockAllResultModel>.Conflict(denial);

        var locked = data.OpenWorkOrdersOf(id, includeChildren).Where(w => w.IsPlanLocked).ToList();
        try
        {
            unitOfWork.BeginTransaction();
            var now = Now();
            foreach (var wo in locked)
            {
                wo.IsPlanLocked = false;
                wo.PlanLockedAt = null;
                wo.UpdatedAt = now;
                Persist.Ensure(await WorkOrders.UpdateAsync(wo), "Impossibile aggiornare un ordine di lavoro.");
            }
            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return ProductionOrderResult<UnlockAllResultModel>.Failed(ProductionOrderFailure.None, $"Impossibile sbloccare i lavori: {ex.Message}");
        }
        return ProductionOrderResult<UnlockAllResultModel>.Ok(new UnlockAllResultModel { Unlocked = locked.Count });
    }

    private static string? BulkDenial(ProductionOrder order, string verb)
        => order.Status is ProductionOrderStatus.Released or ProductionOrderStatus.InProgress
            ? null
            : $"L'ordine {order.Code} è in stato '{ProductionOrderMachineState.StatusLabel(order.Status)}': i lavori {verb} solo per un ordine Rilasciato o In corso.";

    // ---- helpers ------------------------------------------------------------------------------------

    /// <summary>Child orders follow the state of their parent: they take no direct command.</summary>
    private static ProductionOrderResult<ProductionOrderDetailModel>? ChildGuard(ProductionOrder order)
        => order.ParentOrderId is null
            ? null
            : ProductionOrderResult<ProductionOrderDetailModel>.Conflict(
                "Un ordine figlio segue lo stato dell'ordine padre: il comando va eseguito sull'ordine padre.");

    private async Task<string> NextCodeAsync()
    {
        var year = timeProvider.GetUtcNow().Year;
        var prefix = $"OP-{year}-";
        var pattern = new Regex($"^OP-{year}-(\\d+)$");
        var max = 0;
        await foreach (var o in Orders.FetchAsync(x => x.Code.StartsWith(prefix)))
        {
            var match = pattern.Match(o.Code);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n > max) max = n;
        }
        return $"{prefix}{max + 1:D4}";
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;

    private static DateTime? NormalizeUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } v => v,
        { Kind: DateTimeKind.Local } v => v.ToUniversalTime(),
        var v => DateTime.SpecifyKind(v!.Value, DateTimeKind.Utc)
    };
}
