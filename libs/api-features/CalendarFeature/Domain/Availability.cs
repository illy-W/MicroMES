using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.CalendarFeature.Domain;

/// <summary>Working slot in company local time (never crosses midnight).</summary>
public readonly record struct TimeSlot(TimeOnly Start, TimeOnly End);

public readonly record struct WeeklySlot(DayOfWeek DayOfWeek, TimeOnly Start, TimeOnly End);

/// <summary>Who defined the exception: the shared calendar or the resource itself (the resource is more specific).</summary>
public enum AvailabilityLevel
{
    Calendar = 0,
    Resource = 1
}

/// <summary>Exception period in local dates (inclusive), optionally repeated every year on month/day.</summary>
public sealed class AvailabilityException
{
    public int Id { get; init; }
    public required DateOnly FromDate { get; init; }
    public required DateOnly ToDate { get; init; }
    public required CalendarExceptionKind Kind { get; init; }
    public bool IsRecurringYearly { get; init; }
    public AvailabilityLevel Level { get; init; }
    public DateTime CreatedAt { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<TimeSlot> Slots { get; init; } = [];

    /// <summary>
    /// Recurring exceptions match month and day of <see cref="FromDate"/> from its year on; a 29 February matches
    /// only the leap years (it never shifts to 28 February or 1 March).
    /// </summary>
    public bool Covers(DateOnly date)
        => IsRecurringYearly
            ? date.Year >= FromDate.Year && date.Month == FromDate.Month && date.Day == FromDate.Day
            : date >= FromDate && date <= ToDate;
}

/// <summary>
/// What the time calculator needs from any resource: a weekly pattern plus exception periods.
/// Machines implement it now (<see cref="EffectiveAvailability"/>), people can implement it later.
/// </summary>
public interface IAvailabilitySource
{
    IReadOnlyList<WeeklySlot> WeeklySlots { get; }
    IReadOnlyList<AvailabilityException> Exceptions { get; }
}

public sealed class EffectiveAvailability(IReadOnlyList<WeeklySlot> weekly, IReadOnlyList<AvailabilityException> exceptions) : IAvailabilitySource
{
    public IReadOnlyList<WeeklySlot> WeeklySlots { get; } = weekly;
    public IReadOnlyList<AvailabilityException> Exceptions { get; } = exceptions;
}

/// <summary>Where the slots of a day come from.</summary>
public enum DaySource
{
    Weekly,
    CalendarException,
    ResourceException
}

public sealed record DayAvailability(DateOnly Date, IReadOnlyList<TimeSlot> Slots, DaySource Source, AvailabilityException? Exception);

public static class AvailabilityResolver
{
    /// <summary>
    /// The exception that rules the day: resource over calendar, then the most recent (creation time, then id).
    /// The winner replaces the weekly slots entirely.
    /// </summary>
    public static AvailabilityException? Winning(IAvailabilitySource source, DateOnly date)
        => source.Exceptions.Where(e => e.Covers(date))
            .OrderByDescending(e => e.Level).ThenByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .FirstOrDefault();

    public static DayAvailability Resolve(IAvailabilitySource source, DateOnly date)
    {
        var winner = Winning(source, date);
        if (winner is not null)
            return new DayAvailability(date,
                winner.Kind == CalendarExceptionKind.Closed ? [] : winner.Slots.OrderBy(s => s.Start).ToList(),
                winner.Level == AvailabilityLevel.Resource ? DaySource.ResourceException : DaySource.CalendarException, winner);
        var weekly = source.WeeklySlots.Where(s => s.DayOfWeek == date.DayOfWeek)
            .Select(s => new TimeSlot(s.Start, s.End)).OrderBy(s => s.Start).ToList();
        return new DayAvailability(date, weekly, DaySource.Weekly, null);
    }

    public static bool HasAnyWorkingTime(IAvailabilitySource source)
        => source.WeeklySlots.Count > 0 || source.Exceptions.Any(e => e.Kind == CalendarExceptionKind.Custom && e.Slots.Count > 0);
}
