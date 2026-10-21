using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;

namespace MicroMES.ApiFeatures.PlanningFeature.Models;

/// <summary>Granularity of the load: days for windows up to 14 days, weeks (Monday to Sunday, company time) beyond.</summary>
public enum TimelinePeriod
{
    Day,
    Week
}

/// <summary>Why a time span is not worked.</summary>
public enum TimelineNonWorkingKind
{
    /// <summary>Outside the weekly slots (night, lunch break, weekend).</summary>
    Weekly,
    /// <summary>Exception of the shared calendar (holiday, closure, special hours).</summary>
    CalendarException,
    /// <summary>Exception of the machine itself.</summary>
    MachineException
}

public class TimelineEquipmentModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required int Capacity { get; set; }
    /// <summary>False for a deactivated machine that still has work orders in the window.</summary>
    public required bool IsActive { get; set; }
}

public class TimelineWorkOrderModel
{
    public required int Id { get; set; }
    /// <summary>{order code}/{operation number}</summary>
    public required string Code { get; set; }
    public required int ProductionOrderId { get; set; }
    public required string ProductionOrderCode { get; set; }
    public required string MaterialName { get; set; }
    public required string ProcessSegmentCode { get; set; }
    public required string ProcessSegmentName { get; set; }
    /// <summary>Row of the machine; null = the virtual "no machine" row (phases without machines).</summary>
    public int? EquipmentId { get; set; }
    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }
    /// <summary>Real start (Running, Ended) and end (Ended, Aborted).</summary>
    public DateTime? ActualStartAt { get; set; }
    public DateTime? ActualEndAt { get; set; }
    public required WorkOrderStatus Status { get; set; }
    public required PlanStatus PlanStatus { get; set; }
    /// <summary>Planned end after the requested end of the order.</summary>
    public required bool IsLate { get; set; }
    /// <summary>Whole days of delay (rounded up); 0 when not late.</summary>
    public required int LateDays { get; set; }
    public required List<int> PredecessorIds { get; set; }
    /// <summary>
    /// Lane inside the machine row, from 0: overlapping bars take the lowest free lane (order: start, end, id), so it never
    /// exceeds capacity - 1 unless the plan overlaps beyond the capacity (an overload).
    /// </summary>
    public required int Lane { get; set; }
    public required int Priority { get; set; }
}

public class TimelineNonWorkingModel
{
    /// <summary>Machine; null = the default calendar (row without machine).</summary>
    public int? EquipmentId { get; set; }
    public required DateTime Start { get; set; }
    public required DateTime End { get; set; }
    public required TimelineNonWorkingKind Kind { get; set; }
    /// <summary>Reason of the exception; null for the weekly pattern.</summary>
    public string? Reason { get; set; }
}

public class TimelineLoadModel
{
    public required int EquipmentId { get; set; }
    public required DateTime PeriodStart { get; set; }
    public required DateTime PeriodEnd { get; set; }
    /// <summary>Working minutes of the period x capacity.</summary>
    public required decimal CapacityMinutes { get; set; }
    /// <summary>Working minutes spent by the work orders in the period.</summary>
    public required decimal BusyMinutes { get; set; }
    /// <summary>Busy / capacity minutes, as a percentage (0 when the machine does not work; may exceed 100).</summary>
    public required decimal LoadPercent { get; set; }
}

public class TimelineModel
{
    public required DateTime From { get; set; }
    public required DateTime To { get; set; }
    /// <summary>Company time zone (IANA id): the grid is drawn in it.</summary>
    public required string TimeZoneId { get; set; }
    public required TimelinePeriod LoadPeriod { get; set; }
    public required List<TimelineEquipmentModel> Equipment { get; set; }
    public required List<TimelineWorkOrderModel> WorkOrders { get; set; }
    public required List<TimelineNonWorkingModel> NonWorking { get; set; }
    public required List<TimelineLoadModel> Load { get; set; }
    public required DateTime GeneratedAt { get; set; }
}
