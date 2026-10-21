using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.MaterialFeature.Repositories;
using MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Services;

/// <summary>Loads <see cref="ProductionData"/> through the repositories.</summary>
public class ProductionDataLoader(
    IProductionOrderRepository orders,
    IWorkOrderRepository workOrders,
    IWorkOrderDependencyRepository dependencies,
    IWorkOrderMaterialRepository lines,
    IWorkOrderResponseRepository sessions,
    IMaterialRepository materials,
    TimeProvider timeProvider)
{
    /// <summary>Every order (list pages).</summary>
    public Task<ProductionData> LoadAllAsync() => LoadAsync(null);

    /// <summary>The family (root ancestor and all its descendants) of an order; null when the order does not exist.</summary>
    public async Task<ProductionData?> LoadFamilyAsync(int orderId)
    {
        var all = new Dictionary<int, ProductionOrder>();
        await foreach (var o in orders.FetchAsync(_ => true)) all[o.Id] = o;
        if (!all.ContainsKey(orderId)) return null;

        var root = all[orderId];
        while (root.ParentOrderId is { } pid && all.TryGetValue(pid, out var parent)) root = parent;

        var ids = new HashSet<int>();
        var stack = new Stack<int>([root.Id]);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!ids.Add(id)) continue;
            foreach (var c in all.Values.Where(o => o.ParentOrderId == id)) stack.Push(c.Id);
        }
        return await LoadAsync(all, ids);
    }

    /// <summary>The family of the order a work order belongs to; null when the work order does not exist.</summary>
    public async Task<ProductionData?> LoadFamilyByWorkOrderAsync(int workOrderId)
    {
        var wo = await workOrders.GetSingleAsync(workOrderId);
        return wo is null ? null : await LoadFamilyAsync(wo.ProductionOrderId);
    }

    private async Task<ProductionData> LoadAsync(HashSet<int>? orderIds) => await LoadAsync(null, orderIds);

    private async Task<ProductionData> LoadAsync(Dictionary<int, ProductionOrder>? preloaded, HashSet<int>? orderIds)
    {
        var data = new ProductionData(timeProvider);

        if (preloaded is null)
            await foreach (var o in orders.FetchAsync(_ => true)) data.Orders[o.Id] = o;
        else
            foreach (var o in preloaded.Values.Where(o => orderIds is null || orderIds.Contains(o.Id))) data.Orders[o.Id] = o;

        var ids = data.Orders.Keys.ToList();
        await foreach (var w in workOrders.FetchAsync(x => ids.Contains(x.ProductionOrderId))) data.WorkOrders[w.Id] = w;

        var woIds = data.WorkOrders.Keys.ToList();
        await foreach (var d in dependencies.FetchAsync(x => woIds.Contains(x.PredecessorWorkOrderId) || woIds.Contains(x.SuccessorWorkOrderId)))
            data.Dependencies.Add(d);
        await foreach (var s in sessions.FetchAsync(x => woIds.Contains(x.WorkOrderId))) data.Sessions.Add(s);
        await foreach (var l in lines.FetchAsync(x => woIds.Contains(x.WorkOrderId))) data.Lines.Add(l);

        var materialIds = data.Orders.Values.Select(o => o.MaterialId).Concat(data.Lines.Select(l => l.MaterialId)).Distinct().ToList();
        await foreach (var m in materials.FetchAsync(x => materialIds.Contains(x.Id))) data.Materials[m.Id] = m;

        return data;
    }
}
