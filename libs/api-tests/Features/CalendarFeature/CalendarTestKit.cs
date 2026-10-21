using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;

namespace MicroMES.ApiTests.Features.CalendarFeature;

/// <summary>Builders shared by the calendar tests. Company zone: Europe/Rome (UTC+1 in winter, UTC+2 in summer).</summary>
internal static class CalendarTestKit
{
    public static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    public static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public static TimeOnly T(int hour, int minute = 0) => new(hour, minute);

    public static DateOnly D(int year, int month, int day) => new(year, month, day);

    /// <summary>Local Rome wall-clock time as a UTC instant.</summary>
    public static DateTime Local(int year, int month, int day, int hour = 0, int minute = 0)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Rome);

    public static DateTime LocalTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, Rome);

    /// <summary>Mon-Fri 08-12 / 13-17.</summary>
    public static List<WeeklySlot> Standard()
        => Weekdays.SelectMany(d => new[] { new WeeklySlot(d, T(8), T(12)), new WeeklySlot(d, T(13), T(17)) }).ToList();

    public static EffectiveAvailability Source(IEnumerable<WeeklySlot>? weekly = null, params AvailabilityException[] exceptions)
        => new((weekly ?? Standard()).ToList(), exceptions);

    public static AvailabilityException Closed(DateOnly from, DateOnly to, AvailabilityLevel level = AvailabilityLevel.Calendar,
        bool recurring = false, int id = 1, DateTime? createdAt = null, string reason = "Chiuso")
        => new()
        {
            Id = id, FromDate = from, ToDate = to, Kind = CalendarExceptionKind.Closed, IsRecurringYearly = recurring,
            Level = level, CreatedAt = createdAt ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Reason = reason
        };

    public static AvailabilityException Custom(DateOnly from, DateOnly to, (TimeOnly, TimeOnly)[] slots,
        AvailabilityLevel level = AvailabilityLevel.Calendar, int id = 1, DateTime? createdAt = null, string reason = "Orario speciale")
        => new()
        {
            Id = id, FromDate = from, ToDate = to, Kind = CalendarExceptionKind.Custom, Level = level, Reason = reason,
            CreatedAt = createdAt ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Slots = slots.Select(s => new TimeSlot(s.Item1, s.Item2)).ToList()
        };

    public static WorkingTimeCalculator Calc(TimeSpan? horizon = null) => new(Rome, horizon);
}
