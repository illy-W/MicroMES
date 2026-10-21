using System.ComponentModel.DataAnnotations;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;

namespace MicroMES.ApiFeatures.CalendarFeature.Models;

/// <summary>Weekly slot, local company time ("HH:mm:ss"). A pause is the gap between two slots of the same day.</summary>
public class WorkCalendarSlotModel
{
    public required DayOfWeek DayOfWeek { get; set; }
    public required TimeOnly StartTime { get; set; }
    public required TimeOnly EndTime { get; set; }
}

public class WorkCalendarSlotRequest
{
    [Required] public DayOfWeek? DayOfWeek { get; set; }
    [Required] public TimeOnly? StartTime { get; set; }
    [Required] public TimeOnly? EndTime { get; set; }
}

public class WorkCalendarModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required bool IsActive { get; set; }
    /// <summary>The company default calendar (exactly one, always active).</summary>
    public required bool IsDefault { get; set; }
    /// <summary>Sum of the weekly slots, in hours.</summary>
    public required decimal WeeklyHours { get; set; }
    public required List<WorkCalendarSlotModel> Slots { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
}

public class CreateWorkCalendarRequest
{
    [Required, StringLength(50, MinimumLength = 1)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(200, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Required] public List<WorkCalendarSlotRequest>? Slots { get; set; }
}

public class UpdateWorkCalendarRequest
{
    [Required, StringLength(50, MinimumLength = 1)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(200, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Required] public bool? IsActive { get; set; }
    /// <summary>Replaces the whole weekly pattern.</summary>
    [Required] public List<WorkCalendarSlotRequest>? Slots { get; set; }
}

public class DuplicateWorkCalendarRequest
{
    [Required, StringLength(50, MinimumLength = 1)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(200, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
}

public class CompanyTimeZoneModel
{
    public required string TimeZoneId { get; set; }
}

public class UpdateCompanyTimeZoneRequest
{
    /// <summary>IANA identifier, e.g. Europe/Rome.</summary>
    [Required, StringLength(100, MinimumLength = 1)] public string TimeZoneId { get; set; } = string.Empty;
}

/// <summary>Answer of the preview endpoints: where a job of the given minutes ends.</summary>
public class CalendarPreviewModel
{
    public int? CalendarId { get; set; }
    public int? EquipmentId { get; set; }
    public required string TimeZoneId { get; set; }
    public required DateTime Start { get; set; }
    /// <summary>First instant in working time at or after the start.</summary>
    public required DateTime FirstWorkingInstant { get; set; }
    public required decimal StandardMinutes { get; set; }
    public required decimal EfficiencyPercent { get; set; }
    /// <summary>Minutes actually worked: standard minutes ÷ (efficiency / 100).</summary>
    public required decimal WorkingMinutes { get; set; }
    public required DateTime End { get; set; }
}

public class TimeSlotModel
{
    public required TimeOnly StartTime { get; set; }
    public required TimeOnly EndTime { get; set; }
}

public class TimeSlotRequest
{
    [Required] public TimeOnly? StartTime { get; set; }
    [Required] public TimeOnly? EndTime { get; set; }
}

/// <summary>Exception of a calendar or of a machine (maintenance, breakdown).</summary>
public class CalendarExceptionModel
{
    public required int Id { get; set; }
    /// <summary>Calendar id or machine id, depending on the endpoint.</summary>
    public required int OwnerId { get; set; }
    public required DateOnly FromDate { get; set; }
    public required DateOnly ToDate { get; set; }
    public required CalendarExceptionKind Kind { get; set; }
    /// <summary>Fixed holiday repeated every year (single day).</summary>
    public required bool IsRecurringYearly { get; set; }
    public required string Reason { get; set; }
    /// <summary>Slots of a Custom exception (empty for Closed).</summary>
    public required List<TimeSlotModel> Slots { get; set; }
    /// <summary>Other exceptions of the same owner that overlap this one (a warning, saving is not blocked).</summary>
    public required List<ExceptionOverlapModel> Overlaps { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
}

public class ExceptionOverlapModel
{
    public required int Id { get; set; }
    public required DateOnly FromDate { get; set; }
    public required DateOnly ToDate { get; set; }
    public required CalendarExceptionKind Kind { get; set; }
    public required string Reason { get; set; }
}

public class SaveCalendarExceptionRequest
{
    [Required] public DateOnly? FromDate { get; set; }
    [Required] public DateOnly? ToDate { get; set; }
    [Required] public CalendarExceptionKind? Kind { get; set; }
    /// <summary>Only for a single day (FromDate = ToDate). Feb 29 applies in leap years only.</summary>
    public bool IsRecurringYearly { get; set; }
    [Required, StringLength(200, MinimumLength = 1)] public string Reason { get; set; } = string.Empty;
    /// <summary>Required for Custom (at least one slot), ignored for Closed.</summary>
    public List<TimeSlotRequest>? Slots { get; set; }
}

public class ItalianHolidayModel
{
    public required DateOnly Date { get; set; }
    public required string Name { get; set; }
    /// <summary>The calendar already has an exception covering that day.</summary>
    public required bool AlreadyPresent { get; set; }
}

public class ItalianHolidaysPreviewModel
{
    public required int Year { get; set; }
    public required List<ItalianHolidayModel> Holidays { get; set; }
}

public class ImportItalianHolidaysRequest
{
    [Range(1970, 2200)] public int Year { get; set; }
    /// <summary>Dates to import (the others are deselected); missing = all the holidays not already present.</summary>
    public List<DateOnly>? Dates { get; set; }
}

public class ImportItalianHolidaysResultModel
{
    public required int Year { get; set; }
    public required List<CalendarExceptionModel> Created { get; set; }
    /// <summary>Selected holidays skipped because the calendar already had an exception on that day.</summary>
    public required int Skipped { get; set; }
}

public enum CalendarDayStatus
{
    /// <summary>Weekly slots apply.</summary>
    Working,
    /// <summary>No weekly slot (e.g. weekend).</summary>
    NonWorking,
    Closed,
    Custom
}

public enum ExceptionScope
{
    Calendar,
    Equipment
}

public class CalendarDayModel
{
    public required DateOnly Date { get; set; }
    public required DayOfWeek DayOfWeek { get; set; }
    public required CalendarDayStatus Status { get; set; }
    /// <summary>Set when an exception rules the day: shared calendar or the machine.</summary>
    public ExceptionScope? ExceptionScope { get; set; }
    public string? Reason { get; set; }
    public required List<TimeSlotModel> Slots { get; set; }
}

public class UpcomingClosureModel
{
    public required DateOnly FromDate { get; set; }
    public required DateOnly ToDate { get; set; }
    public required string Reason { get; set; }
    public required ExceptionScope Scope { get; set; }
}

/// <summary>The calendar a machine really follows: its own or the company default, with the next closures.</summary>
public class EffectiveCalendarModel
{
    public required int CalendarId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    /// <summary>True when the machine has no own calendar and follows the company default.</summary>
    public required bool IsInherited { get; set; }
    public required decimal WeeklyHours { get; set; }
    public required List<UpcomingClosureModel> UpcomingClosures { get; set; }
}
