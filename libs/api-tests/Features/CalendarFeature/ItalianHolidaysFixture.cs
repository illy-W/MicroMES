using FluentAssertions;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.CalendarFeature.Services;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;

namespace MicroMES.ApiTests.Features.CalendarFeature;

public class ItalianHolidaysFixture
{
    [Theory]
    [InlineData(1999, 4, 4)]
    [InlineData(2000, 4, 23)]
    [InlineData(2008, 3, 23)]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2030, 4, 21)]
    [InlineData(2038, 4, 25)]
    public void EasterSunday_KnownYears_Success(int year, int month, int day)
        => ItalianHolidays.EasterSunday(year).Should().Be(D(year, month, day));

    [Fact]
    public void ForYear_2026_Success_TwelveHolidaysIncludingEasterMonday()
    {
        var list = ItalianHolidays.ForYear(2026);
        list.Should().HaveCount(12);
        list.Select(h => h.Date).Should().OnlyHaveUniqueItems();
        list.Select(h => h.Name).Should().OnlyHaveUniqueItems();
        list.Should().Contain(h => h.Date == D(2026, 4, 5) && h.Name == "Pasqua");
        list.Should().Contain(h => h.Date == D(2026, 4, 6));
        list.Select(h => h.Date).Should().Contain([
            D(2026, 1, 1), D(2026, 1, 6), D(2026, 4, 25), D(2026, 5, 1), D(2026, 6, 2),
            D(2026, 8, 15), D(2026, 11, 1), D(2026, 12, 8), D(2026, 12, 25), D(2026, 12, 26)]);
        list.Should().OnlyContain(h => h.Date.Year == 2026);
    }

    [Theory]
    [InlineData(2024)]
    [InlineData(2028)]
    [InlineData(2100)]
    public void ForYear_AnyYear_Success_AllValidDatesAndEasterMondayIsNextDay(int year)
    {
        var list = ItalianHolidays.ForYear(year);
        list.Should().HaveCount(12);
        var easter = list.Single(h => h.Name == "Pasqua").Date;
        easter.DayOfWeek.Should().Be(DayOfWeek.Sunday);
        list.Single(h => h.Name.StartsWith("Lunedì")).Date.Should().Be(easter.AddDays(1));
    }
}

public class SlotRulesFixture
{
    private static string? Check(params (DayOfWeek? Day, TimeOnly S, TimeOnly E)[] slots) => SlotRules.Validate(slots);

    [Fact]
    public void Validate_ValidSlotsWithPause_Success_Null()
        => Check((DayOfWeek.Monday, T(8), T(12)), (DayOfWeek.Monday, T(13), T(17)), (DayOfWeek.Tuesday, T(8), T(12))).Should().BeNull();

    [Fact]
    public void Validate_AdjacentSlots_Success_NotOverlapping()
        => Check((DayOfWeek.Monday, T(6), T(14)), (DayOfWeek.Monday, T(14), T(22))).Should().BeNull();

    [Fact]
    public void Validate_EndBeforeStart_Failure_MessageSaysWhatToDo()
        => Check((DayOfWeek.Monday, T(12), T(8))).Should().Contain("l'ora di fine deve essere dopo l'inizio").And.Contain("lunedì");

    [Fact]
    public void Validate_EqualStartAndEnd_Failure()
        => Check((DayOfWeek.Friday, T(8), T(8))).Should().NotBeNull();

    [Fact]
    public void Validate_EndAtMidnight_Failure_CrossesMidnight()
        => Check((DayOfWeek.Monday, T(22), T(0))).Should().Contain("mezzanotte");

    [Fact]
    public void Validate_OverlappingSameDay_Failure()
        => Check((DayOfWeek.Monday, T(8), T(12)), (DayOfWeek.Monday, T(11), T(15))).Should().Contain("si sovrappongono");

    [Fact]
    public void Validate_SameTimesDifferentDays_Success()
        => Check((DayOfWeek.Monday, T(8), T(12)), (DayOfWeek.Tuesday, T(8), T(12))).Should().BeNull();

    [Fact]
    public void Validate_ExceptionSlotsWithoutDay_Success_AndOverlapDetected()
    {
        Check((null, T(8), T(10)), (null, T(11), T(12))).Should().BeNull();
        Check((null, T(8), T(10)), (null, T(9), T(12))).Should().Contain("si sovrappongono");
    }

    [Fact]
    public void WeeklyHours_Slots_Success_SumInHours()
        => SlotRules.WeeklyHours(new[] { (T(8), T(12)), (T(13), T(17)), (T(8, 30), T(9)) }).Should().Be(8.5m);
}

public class ExceptionOverlapFixture
{
    private static bool Overlap(MicroMES.ApiFeatures.CalendarFeature.Domain.AvailabilityException a, MicroMES.ApiFeatures.CalendarFeature.Domain.AvailabilityException b)
        => CalendarExceptionService.Overlap(a, b);

    [Fact]
    public void Overlap_RangesTouchingOrApart_Success()
    {
        Overlap(Closed(D(2026, 8, 10), D(2026, 8, 20)), Closed(D(2026, 8, 20), D(2026, 8, 25))).Should().BeTrue();
        Overlap(Closed(D(2026, 8, 10), D(2026, 8, 19)), Closed(D(2026, 8, 20), D(2026, 8, 25))).Should().BeFalse();
    }

    [Fact]
    public void Overlap_RecurringAgainstRange_Success_ExpandedByYear()
    {
        var ferragosto = Closed(D(2025, 8, 15), D(2025, 8, 15), recurring: true);
        Overlap(ferragosto, Closed(D(2027, 8, 10), D(2027, 8, 20))).Should().BeTrue();
        Overlap(ferragosto, Closed(D(2027, 8, 16), D(2027, 8, 20))).Should().BeFalse();
        Overlap(ferragosto, Closed(D(2024, 8, 10), D(2024, 8, 20))).Should().BeFalse("the recurrence starts in 2025");
    }

    [Fact]
    public void Overlap_TwoRecurring_Success_SameDayOnly()
    {
        Overlap(Closed(D(2025, 5, 1), D(2025, 5, 1), recurring: true), Closed(D(2026, 5, 1), D(2026, 5, 1), recurring: true)).Should().BeTrue();
        Overlap(Closed(D(2025, 5, 1), D(2025, 5, 1), recurring: true), Closed(D(2026, 5, 2), D(2026, 5, 2), recurring: true)).Should().BeFalse();
    }

    [Fact]
    public void Overlap_LeapDayRecurring_Success_OnlyLeapYears()
    {
        var leap = Closed(D(2024, 2, 29), D(2024, 2, 29), recurring: true);
        Overlap(leap, Closed(D(2028, 2, 29), D(2028, 2, 29))).Should().BeTrue();
        Overlap(leap, Closed(D(2027, 2, 27), D(2027, 3, 2))).Should().BeFalse();
    }
}
