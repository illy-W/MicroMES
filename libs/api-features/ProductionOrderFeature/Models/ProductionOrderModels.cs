using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Models;

/// <summary>Production order as listed (also the header of the detail).</summary>
public class ProductionOrderModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required int MaterialId { get; set; }
    public required string MaterialCode { get; set; }
    public required string MaterialName { get; set; }
    public required UnitOfMeasure UnitOfMeasure { get; set; }
    public required decimal Quantity { get; set; }
    public required ProductionOrderStatus Status { get; set; }
    public required int Priority { get; set; }
    public DateTime? RequestedStart { get; set; }
    public DateTime? RequestedEnd { get; set; }
    public string? Notes { get; set; }
    public int? ParentOrderId { get; set; }
    public string? ParentOrderCode { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    /// <summary>Sum of the good quantity of the final work order(s) of this order.</summary>
    public required decimal ProducedQuantity { get; set; }
    /// <summary>Produced / quantity as a percentage, capped to 100.</summary>
    public required decimal ProgressPercent { get; set; }
    /// <summary>Commands the UI may offer now: Release, Cancel, Abort.</summary>
    public required List<string> AllowedActions { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
}

public class ProductionOrderSummaryModel
{
    /// <summary>Sum of the planned minutes of the work orders of the order and of its descendants.</summary>
    public required decimal WorkContentMinutes { get; set; }
    /// <summary>Longest path (planned minutes) across work orders and child orders.</summary>
    public required decimal MinimumDurationMinutes { get; set; }
    /// <summary>Sum of the closed sessions of the order and of its descendants.</summary>
    public required decimal WorkedMinutes { get; set; }
    /// <summary>Good quantity of the final work order(s) of this order.</summary>
    public required decimal ProducedQuantity { get; set; }
    /// <summary>Scrap registered on the work orders of this order (own work orders only).</summary>
    public required decimal ScrapQuantity { get; set; }
}

public class WorkOrderDependencyModel
{
    public required int PredecessorWorkOrderId { get; set; }
    public required int SuccessorWorkOrderId { get; set; }
}

public class WorkOrderMaterialModel
{
    public required int Id { get; set; }
    public required int WorkOrderId { get; set; }
    public required string WorkOrderCode { get; set; }
    public required int MaterialId { get; set; }
    public required string MaterialCode { get; set; }
    public required string MaterialName { get; set; }
    public required UnitOfMeasure UnitOfMeasure { get; set; }
    public required decimal RequiredQuantity { get; set; }
    public int? ChildProductionOrderId { get; set; }
    public string? ChildProductionOrderCode { get; set; }
}

/// <summary>Node of the child orders tree.</summary>
public class ProductionOrderTreeModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required int MaterialId { get; set; }
    public required string MaterialCode { get; set; }
    public required string MaterialName { get; set; }
    public required UnitOfMeasure UnitOfMeasure { get; set; }
    public required decimal Quantity { get; set; }
    public required ProductionOrderStatus Status { get; set; }
    public required decimal ProducedQuantity { get; set; }
    public required decimal ProgressPercent { get; set; }
    public required List<ProductionOrderTreeModel> Children { get; set; }
}

public class ProductionOrderDetailModel : ProductionOrderModel
{
    public required List<WorkOrderModel> WorkOrders { get; set; }
    /// <summary>Dependencies that involve a work order of this order (predecessors may belong to child orders).</summary>
    public required List<WorkOrderDependencyModel> Dependencies { get; set; }
    public required List<WorkOrderMaterialModel> Materials { get; set; }
    public required List<ProductionOrderTreeModel> Children { get; set; }
    public required ProductionOrderSummaryModel Summary { get; set; }
    /// <summary>Indicative end (UTC), not persisted: remaining work of the order and its children on the machine calendars and efficiency, ignoring capacity conflicts. Null when the order is not open or nothing is left.</summary>
    public DateTime? EstimatedEnd { get; set; }
}
