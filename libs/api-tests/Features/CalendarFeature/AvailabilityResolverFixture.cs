using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.CalendarFeature.Models;
using MicroMES.ApiFeatures.CalendarFeature.Services;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;

namespace MicroMES.ApiTests.Features.CalendarFeature;

public class AvailabilityResolverFixture
{
    private static readonly DateOnly Monday = D(2026, 1, 5);
    private static readonly DateTime Early = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Late = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Resolve_NoException_Success_WeeklySlotsSorted()
    {
        var r = AvailabilityResolver.Resolve(Source(), Monday);
        r.Source.Should().Be(DaySource.Weekly);
        r.Slots.Should().Equal(new TimeSlot(T(8), T(12)), new TimeSlot(T(13), T(17)));
    }

    [Fact]
    public void Resolve_Weekend_Success_NoSlots()
        => AvailabilityResolver.Resolve(Source(), D(2026, 1, 10)).Slots.Should().BeEmpty();

    [Fact]
    public void Resolve_ClosedCalendarException_Success_NoSlots()
    {
        var r = AvailabilityResolver.Resolve(Source(null, Closed(Monday, Monday)), Monday);
        r.Slots.Should().BeEmpty();
        r.Source.Should().Be(DaySource.CalendarException);
    }

    [Fact]
    public void Resolve_MachineAgainstCalendar_Success_MachineWinsEvenIfOlder()
    {
        var calendarCustom = Custom(Monday, Monday, [(T(9), T(10))], AvailabilityLevel.Calendar, id: 1, createdAt: Late);
        var machineClosed = Closed(Monday, Monday, AvailabilityLevel.Resource, id: 2, createdAt: Early);
        var r = AvailabilityResolver.Resolve(Source(null, calendarCustom, machineClosed), Monday);
        r.Slots.Should().BeEmpty();
        r.Source.Should().Be(DaySource.ResourceException);
    }

    [Fact]
    public void Resolve_MachineCustomOnCompanyHoliday_Success_MachineWorks()
    {
        var holiday = Closed(Monday, Monday, AvailabilityLevel.Calendar);
        var overtime = Custom(Monday, Monday, [(T(8), T(10))], AvailabilityLevel.Resource, id: 2);
        AvailabilityResolver.Resolve(Source(null, holiday, overtime), Monday).Slots.Should().ContainSingle();
    }

    [Fact]
    public void Resolve_SameLevel_Success_MostRecentWins()
    {
        var old = Closed(Monday, Monday, id: 1, createdAt: Early);
        var recent = Custom(Monday, Monday, [(T(14), T(15))], id: 2, createdAt: Late);
        AvailabilityResolver.Resolve(Source(null, old, recent), Monday).Slots.Should().ContainSingle().Which.Start.Should().Be(T(14));
        AvailabilityResolver.Resolve(Source(null, recent, old), Monday).Slots.Should().ContainSingle().Which.Start.Should().Be(T(14));
    }

    [Fact]
    public void Resolve_SameLevelSameTimestamp_Success_HigherIdWins()
    {
        var a = Closed(Monday, Monday, id: 1, createdAt: Early);
        var b = Custom(Monday, Monday, [(T(10), T(11))], id: 2, createdAt: Early);
        AvailabilityResolver.Resolve(Source(null, a, b), Monday).Exception!.Id.Should().Be(2);
    }

    [Fact]
    public void Resolve_OutsideTheRange_Success_ExceptionIgnored()
    {
        var r = AvailabilityResolver.Resolve(Source(null, Closed(D(2026, 1, 6), D(2026, 1, 7))), Monday);
        r.Source.Should().Be(DaySource.Weekly);
    }

    [Fact]
    public void Covers_RecurringDay_Success_EveryYearFromTheStartYear()
    {
        var e = Closed(D(2025, 5, 1), D(2025, 5, 1), recurring: true);
        e.Covers(D(2025, 5, 1)).Should().BeTrue();
        e.Covers(D(2031, 5, 1)).Should().BeTrue();
        e.Covers(D(2024, 5, 1)).Should().BeFalse("it starts in 2025");
        e.Covers(D(2026, 5, 2)).Should().BeFalse();
    }

    [Fact]
    public void Covers_RecurringLeapDay_Success_OnlyInLeapYears()
    {
        var e = Closed(D(2024, 2, 29), D(2024, 2, 29), recurring: true);
        e.Covers(D(2028, 2, 29)).Should().BeTrue();
        e.Covers(D(2027, 2, 28)).Should().BeFalse();
        e.Covers(D(2027, 3, 1)).Should().BeFalse();
    }

    [Fact]
    public void HasAnyWorkingTime_Variants_Success()
    {
        AvailabilityResolver.HasAnyWorkingTime(Source()).Should().BeTrue();
        AvailabilityResolver.HasAnyWorkingTime(Source([])).Should().BeFalse();
        AvailabilityResolver.HasAnyWorkingTime(Source([], Closed(Monday, Monday))).Should().BeFalse();
        AvailabilityResolver.HasAnyWorkingTime(Source([], Custom(Monday, Monday, [(T(8), T(9))]))).Should().BeTrue();
    }

    // ---- month view and calculator use the same resolution ------------------------------------------------------

    [Fact]
    public void Days_MixedExceptions_Success_StatusSourceAndReason()
    {
        var source = Source(null,
            Closed(D(2026, 1, 6), D(2026, 1, 6), reason: "Epifania"),
            Custom(D(2026, 1, 10), D(2026, 1, 10), [(T(8), T(12))], reason: "Straordinario"),
            Closed(D(2026, 1, 7), D(2026, 1, 7), AvailabilityLevel.Resource, id: 5, reason: "Guasto"));

        var days = EffectiveCalendarService.Days(source, D(2026, 1, 5), D(2026, 1, 11));

        days.Select(d => d.Status).Should().Equal(
            CalendarDayStatus.Working, CalendarDayStatus.Closed, CalendarDayStatus.Closed, CalendarDayStatus.Working,
            CalendarDayStatus.Working, CalendarDayStatus.Custom, CalendarDayStatus.NonWorking);
        days[1].Reason.Should().Be("Epifania");
        days[1].ExceptionScope.Should().Be(ExceptionScope.Calendar);
        days[2].ExceptionScope.Should().Be(ExceptionScope.Equipment);
        days[5].Slots.Should().ContainSingle();
        days[0].ExceptionScope.Should().BeNull();
    }

    [Fact]
    public void Days_AndCalculator_Success_SameWorkingMinutesPerDay()
    {
        var source = Source(null,
            Closed(D(2026, 1, 6), D(2026, 1, 6)),
            Custom(D(2026, 1, 10), D(2026, 1, 10), [(T(8), T(12))]));
        var calc = Calc();

        foreach (var day in EffectiveCalendarService.Days(source, D(2026, 1, 5), D(2026, 1, 18)))
        {
            var fromSlots = day.Slots.Sum(s => (decimal)(s.EndTime - s.StartTime).TotalMinutes);
            var fromCalculator = calc.WorkingMinutesBetween(source, Local(day.Date.Year, day.Date.Month, day.Date.Day),
                Local(day.Date.Year, day.Date.Month, day.Date.Day).AddDays(1)).Value;
            fromCalculator.Should().Be(fromSlots, day.Date.ToString());
        }
    }
}
