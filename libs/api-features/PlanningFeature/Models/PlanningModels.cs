using MicroMES.ApiFeatures.PlanningFeature.Engine;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;

namespace MicroMES.ApiFeatures.PlanningFeature.Models;

/// <summary>
/// What a planning run covers. <c>Order</c>: the open work orders of an order and its descendants (re-planned, whatever their plan).
/// <c>Unplanned</c>: only the open work orders without a plan; the plan of the others stays and occupies the machines.
/// <c>All</c>: every open work order of the open orders (full re-plan). In every scope Running work orders are fixed (they only
/// occupy their machine), Ended/Aborted ones are history, and the work orders left out of the scope keep their plan as fixed occupancy.
/// </summary>
public enum PlanningScope
{
    Order,
    Unplanned,
    All
}

public class PlanningRequest
{
    public required PlanningScope Scope { get; set; }
    /// <summary>Required for <c>Order</c>; ignored for the other scopes.</summary>
    public int? ProductionOrderId { get; set; }
    /// <summary>Apply only: token of the preview the user saw. When the new plan differs, the answer says so (it is still applied).</summary>
    public string? PreviewToken { get; set; }
}

/// <summary>How the proposed plan compares with the current one.</summary>
public enum PlanChangeKind
{
    /// <summary>The work order has no plan yet.</summary>
    New,
    /// <summary>Start, end or machine change.</summary>
    Moved,
    Unchanged
}

public class PlanningReasonModel
{
    public required PlanningReasonCode Code { get; set; }
    public required string Message { get; set; }
    public DateTime? Until { get; set; }
    public int? RelatedWorkOrderId { get; set; }
    public int? RelatedEquipmentId { get; set; }
}

public class PlanningItemModel
{
    public required int WorkOrderId { get; set; }
    /// <summary>{order code}/{operation number}</summary>
    public required string Code { get; set; }
    public required int ProductionOrderId { get; set; }
    public required string ProductionOrderCode { get; set; }
    public required string ProcessSegmentCode { get; set; }
    public required string ProcessSegmentName { get; set; }
    public required PlanChangeKind Change { get; set; }
    /// <summary>Proposed machine; null for phases without machines.</summary>
    public WorkOrderEquipmentModel? ProposedEquipment { get; set; }
    public required DateTime ProposedStart { get; set; }
    public required DateTime ProposedEnd { get; set; }
    /// <summary>Machine assigned now (null = none).</summary>
    public WorkOrderEquipmentModel? CurrentEquipment { get; set; }
    public DateTime? CurrentStart { get; set; }
    public DateTime? CurrentEnd { get; set; }
    /// <summary>True when applying writes the machine (it was missing or no longer valid).</summary>
    public required bool AssignsEquipment { get; set; }
    /// <summary>Requested end of the order, when set.</summary>
    public DateTime? RequestedEnd { get; set; }
    /// <summary>The proposed end is after the requested end of the order.</summary>
    public required bool IsLate { get; set; }
    /// <summary>Whole days of delay (rounded up); 0 when not late.</summary>
    public required int LateDays { get; set; }
    public required List<PlanningReasonModel> Reasons { get; set; }
}

public class PlanningUnplannedModel
{
    public required int WorkOrderId { get; set; }
    public required string Code { get; set; }
    public required int ProductionOrderId { get; set; }
    public required string ProductionOrderCode { get; set; }
    public required UnplannedReasonCode Reason { get; set; }
    public required string Message { get; set; }
    public int? RelatedWorkOrderId { get; set; }
    /// <summary>True when the work order has a plan now that applying will remove.</summary>
    public required bool HadPlan { get; set; }
}

public class PlanningSummaryModel
{
    /// <summary>Work orders the run tried to plan.</summary>
    public required int Considered { get; set; }
    public required int Planned { get; set; }
    public required int New { get; set; }
    public required int Moved { get; set; }
    public required int Unchanged { get; set; }
    /// <summary>Orders whose last proposed end is after their requested end.</summary>
    public required int LateOrders { get; set; }
    public required int MaxLateDays { get; set; }
    public required int Unplanned { get; set; }
    /// <summary>Latest proposed end; null when nothing was planned.</summary>
    public DateTime? LastEnd { get; set; }
}

public class PlanningPreviewModel
{
    public required PlanningScope Scope { get; set; }
    public int? ProductionOrderId { get; set; }
    public required List<PlanningItemModel> Items { get; set; }
    public required List<PlanningUnplannedModel> Unplanned { get; set; }
    public required PlanningSummaryModel Summary { get; set; }
    /// <summary>Fingerprint of the proposed plan: send it back to apply to learn whether the plan changed in between.</summary>
    public required string Token { get; set; }
    public required DateTime GeneratedAt { get; set; }
}

public class PlanningApplyModel
{
    public required PlanningScope Scope { get; set; }
    public int? ProductionOrderId { get; set; }
    public required List<PlanningItemModel> Items { get; set; }
    public required List<PlanningUnplannedModel> Unplanned { get; set; }
    public required PlanningSummaryModel Summary { get; set; }
    /// <summary>Token of the plan that was applied.</summary>
    public required string Token { get; set; }
    /// <summary>Null when no preview token was sent; true when the applied plan differs from the previewed one.</summary>
    public bool? DiffersFromPreview { get; set; }
    public required DateTime AppliedAt { get; set; }
}

public class RemovePlanModel
{
    public required int ProductionOrderId { get; set; }
    /// <summary>Work orders whose plan was cleared (the machine assignment is left as it is).</summary>
    public required int ClearedWorkOrders { get; set; }
}
