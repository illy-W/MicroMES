using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Planning;

/// <summary>
/// Derived state of a plan (never stored). English codes for the requirement names
/// NonPianificato = <see cref="Unplanned"/>, Pianificato = <see cref="Planned"/>, InRitardo = <see cref="Late"/>, Obsoleto = <see cref="Stale"/>.
/// </summary>
public enum PlanStatus
{
    Unplanned,
    Planned,
    Late,
    Stale
}

/// <summary>Facts about one work order needed to judge its plan, all taken from the data already loaded.</summary>
/// <param name="PredecessorEnds">Real end (EndedAt) or else planned end of each predecessor that has one.</param>
/// <param name="MachineUsable">False when the assigned machine is inactive or no longer enabled for the phase (true when none is assigned).</param>
public sealed record WorkPlanFacts(
    WorkOrderStatus Status,
    DateTime? PlannedStartAt,
    DateTime? PlannedEndAt,
    DateTime? OrderRequestedEnd,
    IReadOnlyCollection<DateTime> PredecessorEnds,
    bool MachineUsable,
    DateTime Now,
    bool IsLocked = false);

/// <summary>
/// Pure plan status. Rules, in order:
/// <list type="number">
/// <item>No planned start or end: <see cref="PlanStatus.Unplanned"/>.</item>
/// <item>Ended/Aborted work orders: their plan is history, never judged: <see cref="PlanStatus.Planned"/>.</item>
/// <item><see cref="PlanStatus.Stale"/> when the plan is no longer coherent: a predecessor ends (real or planned) after the planned start;
/// the assigned machine is inactive or not enabled for the phase; the work order is Running past its planned end.</item>
/// <item><see cref="PlanStatus.Late"/> when the planned end is after the requested end of the order.</item>
/// <item>Otherwise <see cref="PlanStatus.Planned"/>.</item>
/// </list>
/// Not implemented yet: "the calendar changed after PlannedAt" (needs the change times of calendars and slots, not available in the loaded data).
/// </summary>
public static class PlanStatusCalculator
{
    /// <summary>Why a plan is stale (None = it is not).</summary>
    public enum StaleCause
    {
        None,
        PredecessorEndsAfterStart,
        MachineNotUsable,
        RunningPastPlannedEnd,
        /// <summary>Locked work order whose predecessor ends after its (fixed) start: the lock is the cause, not the engine.</summary>
        LockedPredecessorConflict,
        /// <summary>Locked work order on a machine that is inactive or no longer enabled for the phase.</summary>
        LockedMachineNotUsable
    }

    public static StaleCause CauseOf(WorkPlanFacts f)
    {
        if (f.PlannedStartAt is not { } start || f.PlannedEndAt is not { } end) return StaleCause.None;
        if (f.Status is WorkOrderStatus.Ended or WorkOrderStatus.Aborted) return StaleCause.None;
        if (f.PredecessorEnds.Any(p => p > start))
            return f.IsLocked ? StaleCause.LockedPredecessorConflict : StaleCause.PredecessorEndsAfterStart;
        if (!f.MachineUsable) return f.IsLocked ? StaleCause.LockedMachineNotUsable : StaleCause.MachineNotUsable;
        return f.Status == WorkOrderStatus.Running && f.Now > end ? StaleCause.RunningPastPlannedEnd : StaleCause.None;
    }

    public static PlanStatus Of(WorkPlanFacts f)
    {
        if (f.PlannedStartAt is not { } || f.PlannedEndAt is not { } end) return PlanStatus.Unplanned;
        if (f.Status is WorkOrderStatus.Ended or WorkOrderStatus.Aborted) return PlanStatus.Planned;
        if (CauseOf(f) != StaleCause.None) return PlanStatus.Stale;
        return f.OrderRequestedEnd is { } requested && end > requested ? PlanStatus.Late : PlanStatus.Planned;
    }

    /// <summary>Order level: Unplanned when no work order has a plan, otherwise the worst of Stale, Late, Planned (a partial plan counts as Planned).</summary>
    public static PlanStatus Aggregate(IEnumerable<PlanStatus> statuses)
    {
        var list = statuses.ToList();
        if (list.All(s => s == PlanStatus.Unplanned)) return PlanStatus.Unplanned;
        if (list.Contains(PlanStatus.Stale)) return PlanStatus.Stale;
        return list.Contains(PlanStatus.Late) ? PlanStatus.Late : PlanStatus.Planned;
    }
}
