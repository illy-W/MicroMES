using MicroMES.ApiFeatures.ProductionOrderFeature.Models;

namespace MicroMES.ApiFeatures.PlanningFeature.Models;

/// <summary>Manual move of a work order: new start (UTC instant) and optionally a new machine.</summary>
public class MoveWorkOrderRequest
{
    /// <summary>New planned start (UTC instant). The UI shows and edits it in the company time zone. Must be after now and a working instant of the machine.</summary>
    public required DateTime StartAt { get; set; }

    /// <summary>Machine to use (active and enabled for the phase); omitted = the one already assigned.</summary>
    public int? EquipmentId { get; set; }

    /// <summary>Optional, only for apply: <see cref="MovePreviewModel.ExpectedUpdatedAt"/> of the preview; a different current value gives 409.</summary>
    public DateTime? ExpectedUpdatedAt { get; set; }
}

public enum MoveConflictCode
{
    /// <summary>A predecessor ends after the chosen start.</summary>
    PredecessorEndsAfterStart,
    /// <summary>Successors start before the new end: their plan becomes stale.</summary>
    SuccessorsStartBeforeEnd,
    /// <summary>The machine is over capacity in the interval.</summary>
    MachineOverCapacity,
    /// <summary>The new end is after the requested end of the order.</summary>
    Late
}

/// <summary>A consequence of the move shown to the user; it never blocks the apply.</summary>
public class MoveConflictModel
{
    public required MoveConflictCode Code { get; set; }
    public required string Message { get; set; }
    /// <summary>Work orders involved (the moved one first for capacity conflicts; the predecessor; the successors).</summary>
    public required List<int> WorkOrderIds { get; set; }
    public required List<string> WorkOrderCodes { get; set; }
    public int? EquipmentId { get; set; }
    /// <summary>Minutes of conflict (predecessor overlap, capacity overlap).</summary>
    public int? Minutes { get; set; }
    /// <summary>Days of delay (Late only).</summary>
    public int? LateDays { get; set; }
    /// <summary>Interval of the problem (capacity overlap), UTC.</summary>
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class MovePreviewModel
{
    public required int WorkOrderId { get; set; }
    public required string Code { get; set; }
    /// <summary>Machine after the move; null when the phase has none.</summary>
    public WorkOrderEquipmentModel? Equipment { get; set; }
    public WorkOrderEquipmentModel? CurrentEquipment { get; set; }
    public DateTime? CurrentStart { get; set; }
    public DateTime? CurrentEnd { get; set; }
    public required DateTime NewStart { get; set; }
    public required DateTime NewEnd { get; set; }
    /// <summary>Working minutes between start and end: remaining standard minutes ÷ efficiency of the machine.</summary>
    public required decimal DurationMinutes { get; set; }
    /// <summary>Version of the work order the preview was computed on; send it back when applying.</summary>
    public required DateTime ExpectedUpdatedAt { get; set; }
    public required List<MoveConflictModel> Conflicts { get; set; }
}

public class MoveApplyModel
{
    /// <summary>The work order after the move (locked).</summary>
    public required WorkOrderDetailModel WorkOrder { get; set; }
    public required List<MoveConflictModel> Conflicts { get; set; }
}
