using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.MachineStates;
using MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Services;

/// <summary>Fires the Start and Complete triggers of the order machine after a work order command (same transaction).</summary>
public class ProductionOrderStatusUpdater(IProductionOrderRepository orders)
{
    /// <summary>The first started work order takes the order to InProgress (ignored when already InProgress).</summary>
    public async Task OnWorkOrderStartedAsync(ProductionData data, ProductionOrder order)
    {
        var machine = data.MachineOf(order);
        if (order.Status != ProductionOrderStatus.Released || !machine.CanFire(ProductionOrderTrigger.Start)) return;
        machine.Fire(ProductionOrderTrigger.Start);
        Persist.Ensure(await orders.UpdateAsync(order), "Impossibile aggiornare l'ordine di produzione.");
    }

    /// <summary>When every work order of the order is Ended the order is Completed.</summary>
    public async Task OnWorkOrderEndedAsync(ProductionData data, ProductionOrder order)
    {
        var own = data.WorkOrdersOf(order.Id).ToList();
        if (own.Count == 0 || own.Any(w => w.Status != WorkOrderStatus.Ended)) return;

        var machine = data.MachineOf(order);
        if (!machine.CanFire(ProductionOrderTrigger.Complete)) return;
        machine.Fire(ProductionOrderTrigger.Complete);
        Persist.Ensure(await orders.UpdateAsync(order), "Impossibile aggiornare l'ordine di produzione.");
    }
}
