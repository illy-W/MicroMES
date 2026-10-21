using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Services;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;

namespace MicroMES.ApiFeatures.PlanningFeature.Services;

/// <summary>Machines and machine-phase links the planning needs (all machines linked to any segment).</summary>
public sealed record PlanningCatalog(IReadOnlyDictionary<int, Equipment> Machines, IReadOnlyList<EquipmentProcessSegment> Links);

/// <summary>Input of the engine plus what the caller needs to read the output.</summary>
/// <param name="ScopeWorkOrderIds">Work orders the run plans (the others in the input are fixed).</param>
public sealed record PlanningBuild(PlanningInput Input, IReadOnlySet<int> ScopeWorkOrderIds);

/// <summary>
/// Pure translation of the loaded data into a <see cref="PlanningInput"/>. Rules:
/// <list type="bullet">
/// <item>Only orders Released/InProgress are planned. In scope: Waiting/Ready/Interrupted work orders of the scope
/// (<see cref="PlanningScope"/>); Interrupted ones count only the standard minutes left.</item>
/// <item>Running work orders (any open order) are always in the input as fixed jobs: real start, remaining minutes from now.</item>
/// <item>Open work orders outside the scope that have a plan occupy their assigned machine for [PlannedStartAt, PlannedEndAt].</item>
/// <item>A predecessor outside the planned set (Ended, Aborted, or planned out of scope) only delays the job through
/// <c>ClosedPredecessorsEnd</c> = its real end, else its planned end; an unplanned open one does not constrain.</item>
/// <item>Candidates of a phase = active machines linked to the segment; the default is the link flagged default.</item>
/// </list>
/// </summary>
public static class PlanningInputBuilder
{
    public static bool IsPlannableOrder(ProductionOrder o) => o.Status is ProductionOrderStatus.Released or ProductionOrderStatus.InProgress;

    public static PlanningBuild Build(ProductionData data, PlanningCatalog catalog, AvailabilityDirectory directory,
        PlanningScope scope, int? productionOrderId, DateTime now)
    {
        var openOrderIds = data.Orders.Values.Where(IsPlannableOrder).Select(o => o.Id).ToHashSet();
        var plannable = data.WorkOrders.Values
            .Where(w => openOrderIds.Contains(w.ProductionOrderId)
                        && w.Status is WorkOrderStatus.Waiting or WorkOrderStatus.Ready or WorkOrderStatus.Interrupted)
            .ToList();
        var running = data.WorkOrders.Values
            .Where(w => openOrderIds.Contains(w.ProductionOrderId) && w.Status == WorkOrderStatus.Running).ToList();

        HashSet<int>? subtree = scope == PlanningScope.Order && productionOrderId is { } oid && data.Orders.ContainsKey(oid)
            ? data.Subtree(oid).Select(o => o.Id).ToHashSet() : null;
        var inScope = plannable.Where(w => scope switch
        {
            PlanningScope.Order => subtree is not null && subtree.Contains(w.ProductionOrderId),
            PlanningScope.Unplanned => w.PlannedStartAt is null || w.PlannedEndAt is null,
            _ => true
        }).ToList();
        var scopeIds = inScope.Select(w => w.Id).ToHashSet();
        var runningIds = running.Select(w => w.Id).ToHashSet();

        var linksBySegment = catalog.Links.GroupBy(l => l.ProcessSegmentId).ToDictionary(g => g.Key, g => g.ToList());

        // Machines: candidates of the phases in scope plus the machines of the running jobs.
        var machineIds = new HashSet<int>();
        foreach (var w in inScope)
            foreach (var l in linksBySegment.GetValueOrDefault(w.ProcessSegmentId) ?? [])
                if (catalog.Machines.TryGetValue(l.EquipmentId, out var m) && m.IsActive) machineIds.Add(m.Id);
        var runningMachine = running.ToDictionary(w => w.Id, w => RunningMachineId(data, w));
        foreach (var id in runningMachine.Values.OfType<int>()) if (catalog.Machines.ContainsKey(id)) machineIds.Add(id);

        var busy = machineIds.ToDictionary(id => id, _ => new List<PlanningInterval>());
        foreach (var w in plannable.Where(w => !scopeIds.Contains(w.Id)))
            if (w.PlannedStartAt is { } s && w.PlannedEndAt is { } e && w.AssignedEquipmentId is { } eq && busy.TryGetValue(eq, out var list) && e > s)
                list.Add(new PlanningInterval(s, e));

        var equipment = machineIds.OrderBy(id => id).Select(id =>
        {
            var m = catalog.Machines[id];
            return new PlanningEquipment(m.Id, m.Code, Math.Max(1, m.Capacity), m.EfficiencyPercent, directory.ForEquipment(m.Id), busy[id]);
        }).ToList();

        var jobs = new List<PlanningJob>();
        foreach (var w in inScope)
        {
            var order = data.Orders[w.ProductionOrderId];
            var links = linksBySegment.GetValueOrDefault(w.ProcessSegmentId) ?? [];
            var candidates = links.Where(l => catalog.Machines.TryGetValue(l.EquipmentId, out var m) && m.IsActive)
                .Select(l => l.EquipmentId).OrderBy(i => i).ToList();
            var preds = data.PredecessorIds(w.Id).ToList();
            jobs.Add(new PlanningJob(w.Id, order.Id, order.Code, w.OperationNumber, order.Priority, order.RequestedStart, order.RequestedEnd,
                ProductionEndEstimator.RemainingMinutes(data, w, now),
                preds.Where(p => scopeIds.Contains(p) || runningIds.Contains(p)).ToList(),
                ClosedEnd(data, preds.Where(p => !scopeIds.Contains(p) && !runningIds.Contains(p))),
                links.Count > 0, candidates, links.FirstOrDefault(l => l.IsDefault)?.EquipmentId, w.AssignedEquipmentId));
        }
        foreach (var w in running)
        {
            var order = data.Orders[w.ProductionOrderId];
            var session = data.SessionsOf(w.Id).LastOrDefault(s => s.EndedAt is null);
            jobs.Add(new PlanningJob(w.Id, order.Id, order.Code, w.OperationNumber, order.Priority, order.RequestedStart, order.RequestedEnd,
                ProductionEndEstimator.RemainingMinutes(data, w, now), [], null, true, [], null, runningMachine[w.Id],
                IsFixed: true, FixedStart: session?.StartedAt ?? w.StartedAt ?? now));
        }

        var input = new PlanningInput(jobs, equipment, directory.ForEquipment(null), directory.Zone, now);
        return new PlanningBuild(input, scopeIds);
    }

    private static int? RunningMachineId(ProductionData data, WorkOrder w)
        => data.SessionsOf(w.Id).LastOrDefault(s => s.EndedAt is null)?.EquipmentId ?? w.AssignedEquipmentId;

    private static DateTime? ClosedEnd(ProductionData data, IEnumerable<int> predecessorIds)
    {
        DateTime? max = null;
        foreach (var p in predecessorIds)
        {
            var w = data.WorkOrders[p];
            var end = w.EndedAt ?? w.PlannedEndAt;
            if (end is { } e && (max is null || e > max)) max = e;
        }
        return max;
    }
}
