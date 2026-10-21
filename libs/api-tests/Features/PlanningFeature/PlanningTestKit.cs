using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

/// <summary>
/// Readable builders for engine inputs. Dates are Rome local times in November 2026 (winter time, no DST):
/// 2 Nov is a Monday, 6 Nov a Friday, 7-8 Nov the weekend. "Now" is Monday 2 Nov 08:00.
/// </summary>
internal static class PlanningTestKit
{
    public static readonly DateTime Now = Local(2026, 11, 2, 8);

    /// <summary>Rome local time of November 2026 as UTC.</summary>
    public static DateTime N(int day, int hour, int minute = 0) => Local(2026, 11, day, hour, minute);

    public static PlanningEquipment Machine(int id, string? code = null, int capacity = 1, decimal efficiency = 100m,
        IAvailabilitySource? availability = null, params PlanningInterval[] busy)
        => new(id, code ?? $"M{id}", capacity, efficiency, availability ?? Source(), busy);

    /// <summary>Job with sensible defaults: order "A", operation 10, priority 0, candidates = the given machines.</summary>
    public static PlanningJob Job(int id, decimal minutes, int[]? machines = null, int[]? after = null, string order = "A",
        int? op = null, int priority = 0, DateTime? reqStart = null, DateTime? reqEnd = null, int? assigned = null,
        int? defaultMachine = null, bool? phaseHasMachines = null, DateTime? closedEnd = null, int? orderId = null)
    {
        machines ??= [];
        return new PlanningJob(id, orderId ?? order.GetHashCode(), order, op ?? id * 10, priority, reqStart, reqEnd, minutes,
            after ?? [], closedEnd, phaseHasMachines ?? machines.Length > 0, machines, defaultMachine, assigned);
    }

    public static PlanningJob Running(int id, decimal remainingMinutes, int machine, DateTime startedAt, string order = "R")
        => new(id, order.GetHashCode(), order, id * 10, 0, null, null, remainingMinutes, [], null, true, [machine], null, machine, true, startedAt);

    public static PlanningInput Input(IEnumerable<PlanningJob> jobs, IEnumerable<PlanningEquipment> machines,
        DateTime? now = null, TimeSpan? horizon = null, IAvailabilitySource? defaultAvailability = null)
        => new(jobs.ToList(), machines.ToList(), defaultAvailability ?? Source(), Rome, now ?? Now, horizon);

    public static PlanningResult Plan(IEnumerable<PlanningJob> jobs, IEnumerable<PlanningEquipment> machines,
        DateTime? now = null, TimeSpan? horizon = null)
        => new ForwardFiniteCapacityEngine().Plan(Input(jobs, machines, now, horizon));

    public static PlannedJob Of(this PlanningResult r, int id) => r.Planned.Single(p => p.WorkOrderId == id);

    public static UnplannedJob UnplannedOf(this PlanningResult r, int id) => r.Unplanned.Single(p => p.WorkOrderId == id);
}
