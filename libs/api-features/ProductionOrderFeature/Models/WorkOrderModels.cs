using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Models;

public class WorkOrderLinkModel
{
    public required int Id { get; set; }
    /// <summary>{order code}/{operation number}</summary>
    public required string Code { get; set; }
    public required WorkOrderStatus Status { get; set; }
}

public class WorkOrderSessionModel
{
    public required int Id { get; set; }
    public required DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public required decimal GoodQuantity { get; set; }
    public required decimal ScrapQuantity { get; set; }
    public string? Notes { get; set; }
    /// <summary>Minutes of the session; null while it is still open.</summary>
    public decimal? Minutes { get; set; }
}

/// <summary>Work order as listed (also the header of the detail).</summary>
public class WorkOrderModel
{
    public required int Id { get; set; }
    /// <summary>{order code}/{operation number}</summary>
    public required string Code { get; set; }
    public required int ProductionOrderId { get; set; }
    public required string ProductionOrderCode { get; set; }
    public required ProductionOrderStatus ProductionOrderStatus { get; set; }
    public required int MaterialId { get; set; }
    public required string MaterialCode { get; set; }
    public required string MaterialName { get; set; }
    public required UnitOfMeasure UnitOfMeasure { get; set; }
    public required int OperationNumber { get; set; }
    public required int ProcessSegmentId { get; set; }
    public required string ProcessSegmentCode { get; set; }
    public required string ProcessSegmentName { get; set; }
    public required decimal SetupMinutes { get; set; }
    public required decimal RunMinutesPerUnit { get; set; }
    public required decimal PlannedQuantity { get; set; }
    /// <summary>Setup + run per unit x planned quantity.</summary>
    public required decimal PlannedMinutes { get; set; }
    /// <summary>Sum of the sessions.</summary>
    public required decimal WorkedMinutes { get; set; }
    public required decimal GoodQuantity { get; set; }
    public required decimal ScrapQuantity { get; set; }
    public string? Notes { get; set; }
    public required WorkOrderStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public required List<WorkOrderLinkModel> Predecessors { get; set; }
    public required List<WorkOrderLinkModel> Successors { get; set; }
    /// <summary>Commands the UI may offer now: Start, Pause, Resume, Stop, Abort.</summary>
    public required List<string> AllowedActions { get; set; }
}

public class WorkOrderDetailModel : WorkOrderModel
{
    public required List<WorkOrderSessionModel> Sessions { get; set; }
    public required List<WorkOrderMaterialModel> Materials { get; set; }
}
