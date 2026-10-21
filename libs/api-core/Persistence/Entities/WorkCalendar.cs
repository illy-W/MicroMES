using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>Weekly working calendar (soft-delete through IsActive). Exactly one active calendar is the company default.</summary>
public class WorkCalendar : IEntity<int>
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Weekly working slot in company local time; a pause is the gap between two slots of the same day.</summary>
public class WorkCalendarSlot : IEntity<int>
{
    public int Id { get; set; }
    public int WorkCalendarId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public enum CalendarExceptionKind
{
    /// <summary>Not working in the period.</summary>
    Closed,
    /// <summary>Working with its own slots in the period (half day, overtime).</summary>
    Custom
}

/// <summary>Exception of a calendar (company level): inclusive local date range, optionally repeated every year.</summary>
public class CalendarException : IEntity<int>
{
    public int Id { get; set; }
    public int WorkCalendarId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public CalendarExceptionKind Kind { get; set; }
    /// <summary>Fixed holiday: applies every year on the month/day of FromDate (single day only).</summary>
    public bool IsRecurringYearly { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CalendarExceptionSlot : IEntity<int>
{
    public int Id { get; set; }
    public int CalendarExceptionId { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

/// <summary>Exception specific to a machine (maintenance, breakdown): same shape as <see cref="CalendarException"/>.</summary>
public class EquipmentException : IEntity<int>
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public CalendarExceptionKind Kind { get; set; }
    public bool IsRecurringYearly { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class EquipmentExceptionSlot : IEntity<int>
{
    public int Id { get; set; }
    public int EquipmentExceptionId { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

/// <summary>Minimal key/value system setting (e.g. company time zone).</summary>
public class SystemSetting : IEntity<int>
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
