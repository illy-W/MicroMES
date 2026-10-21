using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>Persisted leaf states only: superstates exist just inside the state machine.</summary>
public enum WorkOrderStatus
{
    Waiting,
    Ready,
    Running,
    Interrupted,
    Ended,
    Aborted
}

public class WorkOrder : IEntity<int>
{
    public int Id { get; set; }
    public int ProductionOrderId { get; set; }
    public int OperationNumber { get; set; }
    public int ProcessSegmentId { get; set; }
    public string ProcessSegmentCode { get; set; } = string.Empty;
    public string ProcessSegmentName { get; set; } = string.Empty;
    public decimal SetupMinutes { get; set; }
    public decimal RunMinutesPerUnit { get; set; }
    public decimal PlannedQuantity { get; set; }
    public string? Notes { get; set; }
    /// <summary>Planned machine (ISA-95 equipment requirement); null = not assigned. SetNull when the machine goes away.</summary>
    public int? AssignedEquipmentId { get; set; }
    public WorkOrderStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
