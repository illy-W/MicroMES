using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;

namespace MicroMES.ApiFeatures.PlanningFeature.Models;

/// <summary>What the machine is doing right now.</summary>
public enum EquipmentNowState
{
    /// <summary>Active, open now and no work order running on it.</summary>
    Free,
    /// <summary>At least one Running work order uses it.</summary>
    Working,
    /// <summary>Active but closed at this instant (outside the shifts, break, or an exception); see ClosedReason.</summary>
    ClosedNow,
    /// <summary>The machine is deactivated.</summary>
    Inactive
}

public class EquipmentRunningModel
{
    public required int WorkOrderId { get; set; }
    public required string WorkOrderCode { get; set; }
    public required int ProductionOrderId { get; set; }
    public required string ProductionOrderCode { get; set; }
    public required string ProcessSegmentName { get; set; }
    /// <summary>Start of the open session on this machine (UTC).</summary>
    public required DateTime ActualStartAt { get; set; }
    public required int ElapsedMinutes { get; set; }
}

public class EquipmentQueueItemModel
{
    public required int WorkOrderId { get; set; }
    public required string WorkOrderCode { get; set; }
    public required int ProductionOrderId { get; set; }
    public required string ProductionOrderCode { get; set; }
    public required string ProcessSegmentCode { get; set; }
    public required string ProcessSegmentName { get; set; }
    public required DateTime PlannedStartAt { get; set; }
    public required DateTime PlannedEndAt { get; set; }
    public int? PlannedSetupMinutes { get; set; }
    public required PlanStatus PlanStatus { get; set; }
    public required PlanStatusCalculator.StaleCause PlanStatusReason { get; set; }
    public required bool IsPlanLocked { get; set; }
}

public class EquipmentMountedToolingModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
}

public class EquipmentMountedSetupValueModel
{
    public required string AttributeCode { get; set; }
    public required string AttributeName { get; set; }
    public required string Value { get; set; }
}

/// <summary>State left on the machine by the last executed (or running) work order, as used by the setup calculation.</summary>
public class EquipmentMountedStateModel
{
    /// <summary>False when the machine has no history (unknown: the planner uses the full setup).</summary>
    public required bool Known { get; set; }
    public required List<EquipmentMountedToolingModel> Tooling { get; set; }
    public required List<EquipmentMountedSetupValueModel> SetupValues { get; set; }
    public string? FromWorkOrderCode { get; set; }
}

/// <summary>Load percentages (same definition as the timeline load); null = the machine has no available time in the window.</summary>
public class EquipmentLoadSummaryModel
{
    /// <summary>Today (company day).</summary>
    public decimal? LoadToday { get; set; }
    /// <summary>From the start of today for 7 days.</summary>
    public decimal? Load7 { get; set; }
    /// <summary>From the start of today for 30 days.</summary>
    public decimal? Load30 { get; set; }
}

public class EquipmentOverviewModel
{
    public required int EquipmentId { get; set; }
    public required EquipmentNowState State { get; set; }
    /// <summary>Why the machine is closed now ("fuori turno", "pausa" or the reason of the exception); only when State is ClosedNow.</summary>
    public string? ClosedReason { get; set; }
    public required List<EquipmentRunningModel> Running { get; set; }
    /// <summary>Up to 20 open (not running) work orders planned on the machine and not yet ended, by planned start.</summary>
    public required List<EquipmentQueueItemModel> Queue { get; set; }
    public required EquipmentMountedStateModel MountedState { get; set; }
    public required EquipmentLoadSummaryModel Load { get; set; }
    /// <summary>The plan alerts about this machine (same items as the alerts endpoint).</summary>
    public required List<PlanAlertModel> Alerts { get; set; }
    public required DateTime GeneratedAt { get; set; }
}
