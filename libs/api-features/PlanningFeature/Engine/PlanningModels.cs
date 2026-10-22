using MicroMES.ApiFeatures.CalendarFeature.Domain;

namespace MicroMES.ApiFeatures.PlanningFeature.Engine;

/// <summary>Half-open interval [Start, End) in UTC.</summary>
public readonly record struct PlanningInterval(DateTime Start, DateTime End);

/// <summary>
/// An open work order to plan. Plain data: no entities, no database, no HTTP.
/// </summary>
/// <param name="WorkOrderId">Id of the work order (unique in the input).</param>
/// <param name="ProductionOrderId">Id of its order.</param>
/// <param name="OrderCode">Code of its order (tie-break of the processing order).</param>
/// <param name="OperationNumber">Operation number (tie-break inside an order).</param>
/// <param name="Priority">Priority of the order: higher goes first.</param>
/// <param name="RequestedStart">Requested start of the order (the job never starts earlier); null = none.</param>
/// <param name="RequestedEnd">Requested end of the order (only used to sort: nearer first); null = last.</param>
/// <param name="StandardMinutes">Standard minutes still to do (setup + run, net of the time already worked for Interrupted/Running jobs), before efficiency.</param>
/// <param name="PredecessorIds">Work orders that must end first, also of other orders; ids not in the input are ignored (use <paramref name="ClosedPredecessorsEnd"/>).</param>
/// <param name="ClosedPredecessorsEnd">Latest real/planned end of the predecessors that are not in the input (e.g. Ended); null = none.</param>
/// <param name="PhaseHasMachines">True when the phase has linked machines (active or not): then a job needs one of the candidates.</param>
/// <param name="CandidateEquipmentIds">Active machines enabled for the phase.</param>
/// <param name="DefaultEquipmentId">Default machine of the phase (tie-break), if any.</param>
/// <param name="AssignedEquipmentId">Machine already assigned; kept when it is among the candidates.</param>
/// <param name="IsFixed">True for Running jobs: they do not move, they occupy the machine from <paramref name="FixedStart"/> to the end computed from the remaining minutes.</param>
/// <param name="FixedStart">Real start of a fixed job.</param>
/// <param name="Locked">True for a job whose plan is locked: it is a fixed interval [<paramref name="LockedStart"/>, <paramref name="LockedEnd"/>) on <paramref name="AssignedEquipmentId"/>, never moved. Its <paramref name="PredecessorIds"/> are only checked (conflicts), never waited for.</param>
/// <param name="LockedStart">Fixed planned start of a locked job.</param>
/// <param name="LockedEnd">Fixed planned end of a locked job.</param>
public sealed record PlanningJob(
    int WorkOrderId,
    int ProductionOrderId,
    string OrderCode,
    int OperationNumber,
    int Priority,
    DateTime? RequestedStart,
    DateTime? RequestedEnd,
    decimal StandardMinutes,
    IReadOnlyList<int> PredecessorIds,
    DateTime? ClosedPredecessorsEnd,
    bool PhaseHasMachines,
    IReadOnlyList<int> CandidateEquipmentIds,
    int? DefaultEquipmentId,
    int? AssignedEquipmentId,
    bool IsFixed = false,
    DateTime? FixedStart = null,
    bool Locked = false,
    DateTime? LockedStart = null,
    DateTime? LockedEnd = null);

/// <summary>A machine as the engine sees it.</summary>
/// <param name="Capacity">Simultaneous jobs (at least 1).</param>
/// <param name="EfficiencyPercent">100 = standard speed.</param>
/// <param name="Availability">Effective availability (weekly slots + exceptions) of the machine.</param>
/// <param name="BusyIntervals">Fixed occupancy other than the Running jobs of the input (each uses one unit of capacity).</param>
public sealed record PlanningEquipment(
    int Id,
    string Code,
    int Capacity,
    decimal EfficiencyPercent,
    IAvailabilitySource Availability,
    IReadOnlyList<PlanningInterval> BusyIntervals);

/// <summary>Whole input of the engine.</summary>
/// <param name="DefaultAvailability">Calendar of the jobs of phases without machines (no capacity limit).</param>
/// <param name="Zone">Company time zone (calendar slots are local times).</param>
/// <param name="Now">Nothing is planned before this instant (rounded up to the minute).</param>
/// <param name="Horizon">Nothing is planned ending after Now + Horizon (default: 6 months).</param>
public sealed record PlanningInput(
    IReadOnlyList<PlanningJob> Jobs,
    IReadOnlyList<PlanningEquipment> Equipment,
    IAvailabilitySource DefaultAvailability,
    TimeZoneInfo Zone,
    DateTime Now,
    TimeSpan? Horizon = null)
{
    public static readonly TimeSpan DefaultHorizon = TimeSpan.FromDays(183);
}

/// <summary>Why a job waits (or why a machine was changed).</summary>
public enum PlanningReasonCode
{
    /// <summary>Starts after a predecessor (RelatedWorkOrderId) that ends at Until.</summary>
    WaitsForPredecessor,
    /// <summary>Starts at the requested start of the order (Until).</summary>
    WaitsForRequestedStart,
    /// <summary>The calendar of the machine is closed until Until.</summary>
    CalendarClosed,
    /// <summary>The machine (RelatedEquipmentId) is at full capacity until Until.</summary>
    MachineBusy,
    /// <summary>The assigned machine is not among the candidates: another one was chosen.</summary>
    AssignedMachineNotEnabled,
    /// <summary>The job is locked: it keeps its plan and machine.</summary>
    Locked,
    /// <summary>Planned before its turn because it must end before the start of a locked job (RelatedWorkOrderId).</summary>
    AdvancedForLocked
}

public sealed record PlanningReason(
    PlanningReasonCode Code,
    string Message,
    DateTime? Until = null,
    int? RelatedWorkOrderId = null,
    int? RelatedEquipmentId = null);

/// <summary>Plan of one job. <c>EquipmentId</c> is null for phases without machines.</summary>
public sealed record PlannedJob(
    int WorkOrderId,
    int? EquipmentId,
    DateTime Start,
    DateTime End,
    bool IsFixed,
    IReadOnlyList<PlanningReason> Reasons,
    bool IsLocked = false);

public enum UnplannedReasonCode
{
    /// <summary>The calendar has no working time at all.</summary>
    NoWorkingTime,
    /// <summary>The job does not end within the horizon.</summary>
    HorizonExceeded,
    /// <summary>The phase has machines but none is active and enabled.</summary>
    NoCandidateMachine,
    /// <summary>The job is in a dependency cycle.</summary>
    CyclicDependency,
    /// <summary>A predecessor (RelatedWorkOrderId) could not be planned (or depends on a cycle).</summary>
    PredecessorUnplanned
}

public sealed record UnplannedJob(int WorkOrderId, UnplannedReasonCode Code, string Message, int? RelatedWorkOrderId = null);

public enum PlanningConflictCode
{
    /// <summary>A predecessor (RelatedWorkOrderId) ends after the start of the locked job (WorkOrderId).</summary>
    PredecessorAfterLockedStart,
    /// <summary>Locked (and running) jobs (WorkOrderIds) overlap beyond the capacity of the machine.</summary>
    LockedOverCapacity
}

/// <summary>
/// A problem the engine reports instead of fixing it (locked jobs never move). Data only: the caller writes the message.
/// For <see cref="PlanningConflictCode.PredecessorAfterLockedStart"/>: WorkOrderId = locked job, RelatedWorkOrderId = predecessor,
/// PredecessorEnd/LockedStart and Minutes of overlap. For <see cref="PlanningConflictCode.LockedOverCapacity"/>: EquipmentId,
/// WorkOrderIds (involved, ordered), the overlap [LockedStart, PredecessorEnd) = [At, Until) and its Minutes.
/// </summary>
public sealed record PlanningConflict(
    PlanningConflictCode Code,
    int WorkOrderId,
    int? RelatedWorkOrderId,
    int? EquipmentId,
    IReadOnlyList<int> WorkOrderIds,
    DateTime Start,
    DateTime End,
    int Minutes);

/// <summary>Output: planned jobs in the order they were processed, plus the jobs left unplanned (ordered by work order id) and the conflicts of the locked jobs.</summary>
public sealed record PlanningResult(IReadOnlyList<PlannedJob> Planned, IReadOnlyList<UnplannedJob> Unplanned, IReadOnlyList<PlanningConflict>? Conflicts = null)
{
    public IReadOnlyList<PlanningConflict> ConflictList => Conflicts ?? [];
    public int LockedCount => Planned.Count(p => p.IsLocked);
    public int ConflictCount => ConflictList.Count;
}
