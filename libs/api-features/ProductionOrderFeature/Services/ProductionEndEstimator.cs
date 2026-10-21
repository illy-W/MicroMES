using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Services;

/// <summary>
/// Indicative end of an order (UTC), never persisted. Pure: works on the in-memory <see cref="ProductionData"/>.
/// <para>
/// Rule: only the open work orders of the order and of its descendants count (Ended/Aborted ones are done: their real data
/// is already in the past, so they never delay anything). A work order starts at the later of "now" and the end of its open
/// predecessors; a Running/Interrupted one counts only the remaining standard minutes (planned − closed sessions − the open
/// session so far), from now. The standard minutes are divided by the efficiency of the machine (the one in use, else the
/// assigned one, else 100%) and consumed on that machine's effective calendar (own or default, company and machine
/// exceptions). The end is the latest finish: it is the end of the longest path when the calendars are the same; with
/// different calendars the propagation over every path is more faithful than choosing the path by minutes alone.
/// </para>
/// Capacity conflicts are ignored on purpose: machines are treated as always free (finite-capacity planning is a later spec).
/// Null when the order is not open (Planned, Completed, Cancelled, Aborted), nothing is left to do, calendars are missing
/// or a calendar has no working time.
/// </summary>
public static class ProductionEndEstimator
{
    public static DateTime? Estimate(ProductionData data, int orderId)
    {
        var directory = data.Availability;
        if (directory is null || !data.Orders.TryGetValue(orderId, out var order)) return null;
        if (order.Status is not (ProductionOrderStatus.Released or ProductionOrderStatus.InProgress)) return null;

        var orderIds = data.Subtree(orderId).Select(o => o.Id).ToHashSet();
        var open = data.WorkOrders.Values
            .Where(w => orderIds.Contains(w.ProductionOrderId) && w.Status is not (WorkOrderStatus.Ended or WorkOrderStatus.Aborted))
            .ToDictionary(w => w.Id);
        if (open.Count == 0) return null;

        var now = data.TimeProvider.GetUtcNow().UtcDateTime;
        var finish = new Dictionary<int, DateTime>();
        var visiting = new HashSet<int>();
        var failed = false;

        DateTime Finish(int id)
        {
            if (finish.TryGetValue(id, out var known)) return known;
            if (!visiting.Add(id)) return now; // defensive: validated routings are acyclic
            var start = now;
            foreach (var p in data.PredecessorIds(id).Where(open.ContainsKey))
            {
                var f = Finish(p);
                if (f > start) start = f;
            }
            visiting.Remove(id);

            var wo = open[id];
            var machineId = MachineOf(data, wo);
            var minutes = WorkingTimeCalculator.ApplyEfficiency(RemainingMinutes(data, wo, now), directory.EfficiencyOf(machineId));
            var end = directory.Calculator.AddWorkingMinutes(directory.ForEquipment(machineId), start, minutes);
            if (!end.IsSuccess) failed = true;
            return finish[id] = end.IsSuccess ? end.Value : start;
        }

        var last = open.Keys.Select(Finish).Max();
        return failed ? null : last;
    }

    private static int? MachineOf(ProductionData data, WorkOrder wo)
        => wo.Status == WorkOrderStatus.Running
            ? data.SessionsOf(wo.Id).LastOrDefault(s => s.EndedAt is null)?.EquipmentId ?? wo.AssignedEquipmentId
            : wo.AssignedEquipmentId;

    /// <summary>Standard minutes still to do: all of them, or the rest for work already started (never negative).</summary>
    public static decimal RemainingMinutes(ProductionData data, WorkOrder wo, DateTime now)
    {
        var planned = ProductionData.PlannedMinutes(wo);
        if (wo.Status is not (WorkOrderStatus.Running or WorkOrderStatus.Interrupted)) return planned;
        var worked = data.WorkedMinutes(wo.Id);
        var openSession = data.SessionsOf(wo.Id).LastOrDefault(s => s.EndedAt is null);
        if (openSession is not null) worked += Math.Max(0m, (decimal)(now - openSession.StartedAt).TotalMinutes);
        return Math.Max(0m, planned - worked);
    }
}
