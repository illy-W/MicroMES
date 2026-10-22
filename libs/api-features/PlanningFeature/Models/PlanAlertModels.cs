namespace MicroMES.ApiFeatures.PlanningFeature.Models;

public enum PlanAlertType
{
    /// <summary>The planned end of an order is after its requested end.</summary>
    Late,
    /// <summary>The engine cannot plan the work order (horizon, calendar without hours, no active enabled machine, dependency cycle...).</summary>
    Unplannable,
    /// <summary>The plan is no longer coherent (a predecessor ends after the planned start, a running job is past its planned end).</summary>
    StalePlan,
    /// <summary>The planned load of a machine in a week exceeds 100% of its working capacity.</summary>
    MachineOverloaded,
    /// <summary>An open work order is assigned to a machine that was deactivated or is no longer enabled for the phase.</summary>
    InactiveOrDisabledMachine,
    /// <summary>(Critical) A locked work order starts before the (planned or real) end of a predecessor.</summary>
    LockedPredecessorConflict,
    /// <summary>(Critical) A locked work order is on a machine that is deactivated, no longer enabled for the phase, or closed at the locked interval.</summary>
    LockedMachineUnavailable,
    /// <summary>(Warning) The locked intervals on a machine exceed its capacity.</summary>
    LockedOverCapacity,
    /// <summary>(Warning) A locked work order ends after the requested end of its order.</summary>
    LockedLate,
    /// <summary>(Warning) Locked or running work orders use a tooling beyond its quantity at some instant.</summary>
    ToolingOverbooked,
    /// <summary>(Critical) A work order is assigned (or locked) on a machine not compatible with a tooling it requires.</summary>
    ToolingIncompatibleMachine,
    /// <summary>(Warning) Open work orders require a deactivated tooling (one alert per tooling).</summary>
    ToolingInactive,
    /// <summary>(Critical) An open work order cannot be planned because of tooling: inactive, or no compatible active machine.</summary>
    UnplannableForTooling
}

public enum PlanAlertSeverity
{
    Warning,
    Critical
}

public class PlanAlertModel
{
    public required PlanAlertType Type { get; set; }
    public required PlanAlertSeverity Severity { get; set; }
    public int? ProductionOrderId { get; set; }
    public string? ProductionOrderCode { get; set; }
    public int? WorkOrderId { get; set; }
    public string? WorkOrderCode { get; set; }
    public int? EquipmentId { get; set; }
    public string? EquipmentCode { get; set; }
    /// <summary>Tooling the alert is about (tooling alerts only).</summary>
    public int? ToolingId { get; set; }
    public string? ToolingCode { get; set; }
    /// <summary>Work orders involved (tooling alerts: those using the tooling).</summary>
    public List<int> WorkOrderIds { get; set; } = [];
    /// <summary>Work order that holds this one back (Unplannable behind an unplanned predecessor, StalePlan behind a late predecessor).</summary>
    public int? CauseWorkOrderId { get; set; }
    /// <summary>Date the alert refers to (UTC): planned end for Late, planned start for StalePlan, week start for MachineOverloaded.</summary>
    public DateTime? Date { get; set; }
    /// <summary>Days of delay (Late only).</summary>
    public int? LateDays { get; set; }
    /// <summary>Load as a percentage of the capacity (MachineOverloaded only).</summary>
    public decimal? LoadPercent { get; set; }
    /// <summary>What is wrong and who causes it, in Italian.</summary>
    public required string Message { get; set; }
    /// <summary>What to do about it, in Italian.</summary>
    public required string Suggestion { get; set; }
}

public class PlanAlertsModel
{
    /// <summary>Ordered by severity (critical first), then by date.</summary>
    public required List<PlanAlertModel> Alerts { get; set; }
    public required int CriticalCount { get; set; }
    public required int WarningCount { get; set; }
    public required DateTime GeneratedAt { get; set; }
}
