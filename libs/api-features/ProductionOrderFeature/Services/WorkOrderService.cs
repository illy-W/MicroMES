using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.MachineStates;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Services;

public class WorkOrderService(
    ProductionDataLoader loader,
    ProductionOrderStatusUpdater orderStatus,
    IWorkOrderRepository workOrders,
    IWorkOrderResponseRepository sessions,
    IUnitOfWork unitOfWork)
{
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
        => ExecuteAsync(id, WorkOrderTrigger.Pause, m => m.Pause(request.GoodQuantity, request.ScrapQuantity, request.Notes));

    public Task<ProductionOrderResult<WorkOrderDetailModel>> ResumeAsync(int id)
        => ExecuteAsync(id, WorkOrderTrigger.Resume, m => m.Resume());

    public Task<ProductionOrderResult<WorkOrderDetailModel>> StopAsync(int id, CloseSessionRequest request)
        => ExecuteAsync(id, WorkOrderTrigger.Stop, m => m.Stop(request.GoodQuantity, request.ScrapQuantity, request.Notes));

    public Task<ProductionOrderResult<WorkOrderDetailModel>> AbortAsync(int id, string? notes)
        => ExecuteAsync(id, WorkOrderTrigger.Abort, m => m.Abort(notes));

    private async Task<ProductionOrderResult<WorkOrderDetailModel>> ExecuteAsync(
        int id, WorkOrderTrigger trigger, Action<WorkOrderMachineState> fire)
    {
        var data = await loader.LoadFamilyByWorkOrderAsync(id);
        if (data is null || !data.WorkOrders.TryGetValue(id, out var wo))
            return ProductionOrderResult<WorkOrderDetailModel>.NotFound("Ordine di lavoro non trovato.");

        var machine = data.MachineOf(wo);
        if (!machine.CanFire(trigger))
            return ProductionOrderResult<WorkOrderDetailModel>.Conflict(machine.DeniedMessage(trigger));

        try
        {
            unitOfWork.BeginTransaction();
            await ApplyAsync(data, wo, fire);
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
    public async Task ApplyAsync(ProductionData data, WorkOrder wo, Action<WorkOrderMachineState> fire)
    {
        var machine = data.MachineOf(wo);
        fire(machine);

        Persist.Ensure(await workOrders.UpdateAsync(wo), "Impossibile aggiornare l'ordine di lavoro.");
        if (machine.OpenedSession is { } opened)
        {
            var added = Persist.Ensure(await sessions.AddAsync(opened), "Impossibile registrare la sessione di lavoro.");
            data.Sessions.Add(added);
        }
        if (machine.ClosedSession is { } closed)
            Persist.Ensure(await sessions.UpdateAsync(closed), "Impossibile aggiornare la sessione di lavoro.");

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

    /// <summary>Waiting successors (also in other orders) become Ready when all their predecessors are Ended.</summary>
    private async Task UnlockSuccessorsAsync(ProductionData data, WorkOrder ended)
    {
        foreach (var successorId in data.SuccessorIds(ended.Id).Distinct().ToList())
        {
            var successor = data.WorkOrders[successorId];
            if (successor.Status != WorkOrderStatus.Waiting) continue;
            if (data.PredecessorIds(successorId).Any(p => data.WorkOrders[p].Status != WorkOrderStatus.Ended)) continue;

            data.MachineOf(successor).PredecessorsEnded();
            Persist.Ensure(await workOrders.UpdateAsync(successor), "Impossibile aggiornare l'ordine di lavoro.");
        }
    }

    private static bool Contains(string value, string term) => value.Contains(term, StringComparison.OrdinalIgnoreCase);
}
