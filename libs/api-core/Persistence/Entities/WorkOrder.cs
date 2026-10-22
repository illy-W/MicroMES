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
    /// <summary>Planned start/end (UTC) written by the planning; null = not planned. The planned machine is <see cref="AssignedEquipmentId"/>.</summary>
    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }
    /// <summary>When the plan was written (UTC); null = not planned.</summary>
    public DateTime? PlannedAt { get; set; }
    /// <summary>Setup minutes computed by the planning (sequence-dependent) or by the manual move; null = not planned or unknown. Cleared with the plan.</summary>
    public int? PlannedSetupMinutes { get; set; }
    /// <summary>Why the planned setup lasts <see cref="PlannedSetupMinutes"/> (Italian, one entry per change); null = none.</summary>
    public List<string>? PlannedSetupReasons { get; set; }
    /// <summary>True when the planned setup is the full setup of the step.</summary>
    public bool PlannedSetupIsFull { get; set; }
    /// <summary>Manual lock of the plan: the engine never moves a locked work order. Cleared when the work order starts or its order is closed.</summary>
    public bool IsPlanLocked { get; set; }
    /// <summary>When the plan was locked (UTC); null when not locked.</summary>
    public DateTime? PlanLockedAt { get; set; }
    public WorkOrderStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
