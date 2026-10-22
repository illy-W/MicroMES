using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Models;

/// <summary>Order of the family shown in the overview (the requested order and all its descendants).</summary>
public class ProductionOrderOverviewOrderModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public int? ParentOrderId { get; set; }
    public required int MaterialId { get; set; }
    public required string MaterialCode { get; set; }
    public required string MaterialName { get; set; }
    public required UnitOfMeasure UnitOfMeasure { get; set; }
    public required decimal Quantity { get; set; }
    public required ProductionOrderStatus Status { get; set; }
    public required decimal ProducedQuantity { get; set; }
    public required decimal ProgressPercent { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public required PlanStatus PlanStatus { get; set; }
}

/// <summary>Work order of the family shown in the overview.</summary>
public class ProductionOrderOverviewWorkOrderModel
{
    public required int Id { get; set; }
    public required int ProductionOrderId { get; set; }
    /// <summary>{order code}/{operation number}</summary>
    public required string Code { get; set; }
    public required int OperationNumber { get; set; }
    public required string ProcessSegmentCode { get; set; }
    public required string ProcessSegmentName { get; set; }
    public required WorkOrderStatus Status { get; set; }
    public WorkOrderEquipmentModel? AssignedEquipment { get; set; }
    public WorkOrderEquipmentModel? CurrentEquipment { get; set; }
    public required decimal PlannedQuantity { get; set; }
    public required decimal GoodQuantity { get; set; }
    public required decimal ScrapQuantity { get; set; }
    public required decimal PlannedMinutes { get; set; }
    public required decimal WorkedMinutes { get; set; }
    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }
    public required PlanStatus PlanStatus { get; set; }
    public required PlanStatusCalculator.StaleCause PlanStatusReason { get; set; }
    public required bool IsPlanLocked { get; set; }
    public DateTime? PlanLockedAt { get; set; }
    /// <summary>Commands the UI may offer now: Start, Pause, Resume, Stop, Abort.</summary>
    public required List<string> AllowedActions { get; set; }
    public List<WorkOrderToolingModel> Tooling { get; set; } = [];
    public List<WorkOrderSetupValueModel> SetupValues { get; set; } = [];
    /// <summary>Materials consumed by this work order (ChildProductionOrderId set for intermediates made by a child order).</summary>
    public required List<WorkOrderMaterialModel> Materials { get; set; }
}

/// <summary>Number of work orders of the family per status.</summary>
public class WorkOrderStatusCountsModel
{
    public required int Waiting { get; set; }
    public required int Ready { get; set; }
    public required int Running { get; set; }
    public required int Interrupted { get; set; }
    public required int Ended { get; set; }
    public required int Aborted { get; set; }
}

public class ProductionOrderOverviewSummaryModel
{
    public required int TotalWorkOrders { get; set; }
    public required WorkOrderStatusCountsModel Counts { get; set; }
    /// <summary>Longest path (planned minutes) across work orders and child orders.</summary>
    public required decimal MinimumDurationMinutes { get; set; }
    /// <summary>Work orders of the longest path, from the first to the last.</summary>
    public required List<int> CriticalPathWorkOrderIds { get; set; }
    /// <summary>Sum of the planned minutes of every work order of the family.</summary>
    public required decimal WorkContentMinutes { get; set; }
    /// <summary>Sum of the closed sessions of the family.</summary>
    public required decimal WorkedMinutes { get; set; }
    /// <summary>Planned end when the order has a plan, otherwise the indicative end (UTC) of the requested order and its children (same rule as the order detail); null when not available.</summary>
    public DateTime? EstimatedEnd { get; set; }
}

/// <summary>Whole family of an order (the order and all its descendants, flat) for the graphical overview.</summary>
public class ProductionOrderOverviewModel
{
    public required List<ProductionOrderOverviewOrderModel> Orders { get; set; }
    public required List<ProductionOrderOverviewWorkOrderModel> WorkOrders { get; set; }
    /// <summary>Every dependency among the work orders of the family, including child last step -> parent start steps.</summary>
    public required List<WorkOrderDependencyModel> Dependencies { get; set; }
    public required ProductionOrderOverviewSummaryModel Summary { get; set; }
    public required DateTime GeneratedAt { get; set; }
}
