using FluentAssertions;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;

namespace MicroMES.ApiTests.Features.CalendarFeature;

public class WorkingTimeCalculatorFixture
{
    // Monday 5 January 2026 (CET, UTC+1).
    private static DateTime Mon(int hour, int minute = 0) => Local(2026, 1, 5, hour, minute);

    private static DateTime AddOk(IAvailabilitySource s, DateTime start, decimal minutes)
    {
        var r = Calc().AddWorkingMinutes(s, start, minutes);
        r.IsSuccess.Should().BeTrue(r.Error);
        return r.Value;
    }

    // ---- AddWorkingMinutes ------------------------------------------------------------------------------

    [Fact]
    public void AddWorkingMinutes_100MinutesFromMonday8_Success_EndsAt940()
        => AddOk(Source(), Mon(8), 100).Should().Be(Mon(9, 40));

    [Fact]
    public void AddWorkingMinutes_ZeroMinutes_Success_ReturnsStart()
        => AddOk(Source(), Mon(12, 30), 0).Should().Be(Mon(12, 30));

    [Fact]
    public void AddWorkingMinutes_Negative_Failure()
        => Calc().AddWorkingMinutes(Source(), Mon(8), -1).IsSuccess.Should().BeFalse();

    [Fact]
    public void AddWorkingMinutes_ExactlyUntilSlotEnd_Success_EndsAtSlotEndNotAfterPause()
        => AddOk(Source(), Mon(8), 240).Should().Be(Mon(12));

    [Fact]
    public void AddWorkingMinutes_CrossesLunchPause_Success_PauseNotCounted()
        => AddOk(Source(), Mon(11), 120).Should().Be(Mon(14));

    [Fact]
    public void AddWorkingMinutes_StartInsidePause_Success_StartsAfterPause()
        => AddOk(Source(), Mon(12, 30), 30).Should().Be(Mon(13, 30));

    [Fact]
    public void AddWorkingMinutes_StartBeforeFirstSlot_Success_StartsAtSlot()
        => AddOk(Source(), Mon(6), 60).Should().Be(Mon(9));

    [Fact]
    public void AddWorkingMinutes_StartAfterLastSlot_Success_NextDay()
        => AddOk(Source(), Mon(18), 60).Should().Be(Local(2026, 1, 6, 9));

    [Fact]
    public void AddWorkingMinutes_FridayAfternoon_Success_SkipsWeekend()
        => AddOk(Source(), Local(2026, 1, 9, 16), 120).Should().Be(Local(2026, 1, 12, 9));

    [Fact]
    public void AddWorkingMinutes_StartOnSaturday_Success_MondayMorning()
        => AddOk(Source(), Local(2026, 1, 10, 10), 30).Should().Be(Local(2026, 1, 12, 8, 30));

    [Fact]
    public void AddWorkingMinutes_MoreThanAWeek_Success_CrossesWeeks()
    {
        // 40 working hours a week: 2 weeks + 1 hour from Monday 8:00 ends the third Monday at 9:00.
        AddOk(Source(), Mon(8), 2 * 2400 + 60).Should().Be(Local(2026, 1, 19, 9));
    }

    [Fact]
    public void AddWorkingMinutes_FractionalMinutes_Success()
        => AddOk(Source(), Mon(8), 1.5m).Should().Be(Mon(8).AddSeconds(90));

    [Fact]
    public void AddWorkingMinutes_ClosedDayInTheMiddle_Success_SkipsIt()
    {
        var source = Source(null, Closed(D(2026, 1, 6), D(2026, 1, 6)));
        AddOk(source, Mon(16), 120).Should().Be(Local(2026, 1, 7, 9));
    }

    [Fact]
    public void AddWorkingMinutes_LongHolidayPeriod_Success_JumpsOverIt()
    {
        var source = Source(null, Closed(D(2026, 1, 6), D(2026, 1, 16)));
        AddOk(source, Mon(16), 120).Should().Be(Local(2026, 1, 19, 9));
    }

    [Fact]
    public void AddWorkingMinutes_CustomHalfDay_Success_UsesExceptionSlots()
    {
        // Tuesday only 08-12 (no afternoon): 5 hours from Tuesday 8:00 spill to Wednesday.
        var source = Source(null, Custom(D(2026, 1, 6), D(2026, 1, 6), [(T(8), T(12))]));
        AddOk(source, Local(2026, 1, 6, 8), 300).Should().Be(Local(2026, 1, 7, 9));
    }

    [Fact]
    public void AddWorkingMinutes_CustomOvertimeOnSaturday_Success_WorksOnWeekend()
    {
        var source = Source(null, Custom(D(2026, 1, 10), D(2026, 1, 10), [(T(8), T(12))]));
        AddOk(source, Local(2026, 1, 9, 16, 30), 60).Should().Be(Local(2026, 1, 10, 8, 30));
    }

    [Fact]
    public void AddWorkingMinutes_CustomOnClosedDay_Success_CustomReplacesWeeklySlots()
    {
        var source = Source(null, Custom(D(2026, 1, 5), D(2026, 1, 5), [(T(14), T(15))]));
        AddOk(source, Mon(8), 30).Should().Be(Mon(14, 30));
    }

    [Fact]
    public void AddWorkingMinutes_RecurringHoliday_Success_AppliesEveryYear()
    {
        var epiphany = Closed(D(2025, 1, 6), D(2025, 1, 6), recurring: true);
        var source = Source(null, epiphany);
        AddOk(source, Local(2026, 1, 5, 16), 120).Should().Be(Local(2026, 1, 7, 9));
        AddOk(source, Local(2027, 1, 5, 16), 120).Should().Be(Local(2027, 1, 7, 9));
    }

    [Fact]
    public void AddWorkingMinutes_OutputIsUtc()
        => AddOk(Source(), Mon(8), 10).Kind.Should().Be(DateTimeKind.Utc);

    [Fact]
    public void AddWorkingMinutes_LocalKindInput_Success_ConvertedToUtc()
    {
        var local = Mon(8).ToLocalTime();
        AddOk(Source(), local, 100).Should().Be(Mon(9, 40));
    }

    // ---- efficiency -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(100, 100, 100)]
    [InlineData(100, 80, 125)]
    [InlineData(100, 200, 50)]
    [InlineData(60, 120, 50)]
    public void ApplyEfficiency_Percent_Success_StandardOverFactor(double standard, double efficiency, double expected)
        => WorkingTimeCalculator.ApplyEfficiency((decimal)standard, (decimal)efficiency).Should().Be((decimal)expected);

    [Fact]
    public void ApplyEfficiency_NonPositive_Success_LeavesMinutesUntouched()
        => WorkingTimeCalculator.ApplyEfficiency(100, 0).Should().Be(100);

    [Fact]
    public void AddWorkingMinutes_SlowMachine_Success_LongerThanStandard()
    {
        var minutes = WorkingTimeCalculator.ApplyEfficiency(100, 80); // 125
        AddOk(Source(), Mon(8), minutes).Should().Be(Mon(10, 5));
    }

    // ---- horizon / no working time -----------------------------------------------------------------------------

    [Fact]
    public void AddWorkingMinutes_CalendarWithoutSlots_Failure_ExplicitError()
    {
        var r = Calc().AddWorkingMinutes(Source([]), Mon(8), 10);
        r.IsSuccess.Should().BeFalse();
        r.Error.Should().Be(WorkingTimeCalculator.NoWorkingTimeMessage);
    }

    [Fact]
    public void AddWorkingMinutes_EverythingClosedBeyondHorizon_Failure_NoEndlessLoop()
    {
        var source = Source(null, Closed(D(2026, 1, 1), D(2035, 12, 31)));
        var r = Calc().AddWorkingMinutes(source, Mon(8), 10);
        r.IsSuccess.Should().BeFalse();
        r.Error.Should().Be(WorkingTimeCalculator.NoWorkingTimeMessage);
    }

    [Fact]
    public void AddWorkingMinutes_ReachableWithinHorizon_Success()
    {
        var source = Source(null, Closed(D(2026, 1, 5), D(2027, 6, 30)));
        Calc().AddWorkingMinutes(source, Mon(8), 10).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AddWorkingMinutes_TooMuchWorkForTheHorizon_Failure()
    {
        // 40 h per week for 10 days of horizon: 100 hours do not fit.
        var r = Calc(TimeSpan.FromDays(10)).AddWorkingMinutes(Source(), Mon(8), 6000);
        r.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void AddWorkingMinutes_OnlyCustomSlotsExist_Success()
    {
        var source = Source([], Custom(D(2026, 2, 3), D(2026, 2, 3), [(T(9), T(10))]));
        AddOk(source, Mon(8), 30).Should().Be(Local(2026, 2, 3, 9, 30));
    }

    // ---- WorkingMinutesBetween -----------------------------------------------------------------------------------

    [Fact]
    public void WorkingMinutesBetween_FullWeek_Success_2400()
        => Calc().WorkingMinutesBetween(Source(), Mon(8), Local(2026, 1, 12, 8)).Value.Should().Be(2400);

    [Fact]
    public void WorkingMinutesBetween_InsidePause_Success_Zero()
        => Calc().WorkingMinutesBetween(Source(), Mon(12), Mon(13)).Value.Should().Be(0);

    [Fact]
    public void WorkingMinutesBetween_PartialSlots_Success()
        => Calc().WorkingMinutesBetween(Source(), Mon(11), Mon(14)).Value.Should().Be(120);

    [Fact]
    public void WorkingMinutesBetween_ReversedOrEmpty_Success_Zero()
    {
        Calc().WorkingMinutesBetween(Source(), Mon(14), Mon(11)).Value.Should().Be(0);
        Calc().WorkingMinutesBetween(Source(), Mon(9), Mon(9)).Value.Should().Be(0);
    }

    [Fact]
    public void WorkingMinutesBetween_WithClosedAndCustomDays_Success()
    {
        var source = Source(null,
            Closed(D(2026, 1, 6), D(2026, 1, 6)),
            Custom(D(2026, 1, 7), D(2026, 1, 7), [(T(8), T(10))]));
        // Mon 480 + Tue 0 + Wed 120 + Thu 480 + Fri 480
        Calc().WorkingMinutesBetween(source, Mon(0), Local(2026, 1, 10)).Value.Should().Be(480 + 120 + 480 + 480);
    }

    [Fact]
    public void WorkingMinutesBetween_BeyondHorizon_Failure()
        => Calc(TimeSpan.FromDays(30)).WorkingMinutesBetween(Source(), Mon(8), Mon(8).AddDays(60)).IsSuccess.Should().BeFalse();

    [Fact]
    public void WorkingMinutes_AddThenBetween_Success_RoundTrip()
    {
        var source = Source(null, Closed(D(2026, 1, 6), D(2026, 1, 8)));
        var end = AddOk(source, Mon(10), 1000);
        Calc().WorkingMinutesBetween(source, Mon(10), end).Value.Should().Be(1000);
    }

    // ---- NextWorkingInstant -----------------------------------------------------------------------------------------

    [Fact]
    public void NextWorkingInstant_InsideSlot_Success_SameInstant()
        => Calc().NextWorkingInstant(Source(), Mon(9, 30)).Value.Should().Be(Mon(9, 30));

    [Fact]
    public void NextWorkingInstant_InPause_Success_PauseEnd()
        => Calc().NextWorkingInstant(Source(), Mon(12, 15)).Value.Should().Be(Mon(13));

    [Fact]
    public void NextWorkingInstant_ExactlyAtSlotEnd_Success_NextSlot()
        => Calc().NextWorkingInstant(Source(), Mon(12)).Value.Should().Be(Mon(13));

    [Fact]
    public void NextWorkingInstant_FridayEvening_Success_MondayMorning()
        => Calc().NextWorkingInstant(Source(), Local(2026, 1, 9, 18)).Value.Should().Be(Local(2026, 1, 12, 8));

    [Fact]
    public void NextWorkingInstant_NoSlots_Failure()
        => Calc().NextWorkingInstant(Source([]), Mon(8)).IsSuccess.Should().BeFalse();

    // ---- DST (Europe/Rome: 29 Mar 2026 02:00->03:00, 25 Oct 2026 03:00->02:00) ----------------------------------------------

    private static List<WeeklySlot> SundaySlot(int fromH, int fromM, int toH, int toM)
        => [new WeeklySlot(DayOfWeek.Sunday, T(fromH, fromM), T(toH, toM))];

    [Fact]
    public void ToUtc_NonExistentLocalTime_Success_ShiftsForwardByTheGap()
    {
        // 02:30 does not exist on 29 March 2026: it means 03:30 CEST = 01:30Z.
        Calc().ToUtc(D(2026, 3, 29), T(2, 30)).Should().Be(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToUtc_RepeatedLocalTime_Success_FirstOccurrence()
    {
        // 02:30 happens twice on 25 October 2026: the first one is CEST (00:30Z).
        Calc().ToUtc(D(2026, 10, 25), T(2, 30)).Should().Be(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void WorkingMinutesBetween_SpringForwardDay_Success_SlotAcrossGapIsShorter()
    {
        // 01:00-04:00 local on the night with 23 hours: only 2 real hours.
        var source = Source(SundaySlot(1, 0, 4, 0));
        Calc().WorkingMinutesBetween(source, Local(2026, 3, 29, 0), Local(2026, 3, 30, 0)).Value.Should().Be(120);
    }

    [Fact]
    public void WorkingMinutesBetween_NormalSunday_Success_ThreeHours()
    {
        var source = Source(SundaySlot(1, 0, 4, 0));
        Calc().WorkingMinutesBetween(source, Local(2026, 3, 22, 0), Local(2026, 3, 23, 0)).Value.Should().Be(180);
    }

    [Fact]
    public void WorkingMinutesBetween_SlotInsideTheGap_Success_Zero()
    {
        var source = Source(SundaySlot(2, 30, 2, 45));
        Calc().WorkingMinutesBetween(source, Local(2026, 3, 29, 0), Local(2026, 3, 30, 0)).Value.Should().Be(0);
    }

    [Fact]
    public void WorkingMinutesBetween_SlotStartingInsideTheGap_Success_StartsAtGapEnd()
    {
        // 02:00-05:00: 02:00 does not exist -> 03:00 CEST; ends 05:00 CEST: 2 real hours.
        var source = Source(SundaySlot(2, 0, 5, 0));
        Calc().WorkingMinutesBetween(source, Local(2026, 3, 29, 0), Local(2026, 3, 30, 0)).Value.Should().Be(120);
    }

    [Fact]
    public void AddWorkingMinutes_SpringForward_Success_ContinuesAfterTheGap()
    {
        var source = Source(SundaySlot(1, 0, 4, 0));
        var start = Local(2026, 3, 29, 1); // 00:00Z
        // 90 real minutes: 00:00Z -> 01:30Z = 03:30 CEST.
        AddOk(source, start, 90).Should().Be(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void WorkingMinutesBetween_FallBackDay_Success_RepeatedHourIsWorkedTwice()
    {
        // 01:00 CEST (23:00Z) to 04:00 CET (03:00Z): 4 real hours.
        var source = Source(SundaySlot(1, 0, 4, 0));
        Calc().WorkingMinutesBetween(source, new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc)).Value.Should().Be(240);
    }

    [Fact]
    public void WorkingMinutesBetween_FallBackSlotInsideRepeatedHour_Success_FirstOccurrenceBothEnds()
    {
        // 02:30 -> 03:30: 02:30 = first occurrence (00:30Z), 03:30 = CET (02:30Z): 120 real minutes.
        var source = Source(SundaySlot(2, 30, 3, 30));
        Calc().WorkingMinutesBetween(source, new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc)).Value.Should().Be(120);
    }

    [Fact]
    public void AddWorkingMinutes_AcrossSpringForwardWeekend_Success_WallClockPreserved()
    {
        // Friday 27 March 16:00 CET + 120 min -> Monday 30 March 09:00 CEST (07:00Z).
        AddOk(Source(), Local(2026, 3, 27, 16), 120).Should().Be(new DateTime(2026, 3, 30, 7, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void AddWorkingMinutes_AcrossFallBackWeekend_Success_WallClockPreserved()
    {
        // Friday 23 October 16:00 CEST + 120 min -> Monday 26 October 09:00 CET (08:00Z).
        AddOk(Source(), Local(2026, 10, 23, 16), 120).Should().Be(new DateTime(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void AddWorkingMinutes_UtcZone_Success_NoOffset()
    {
        var calc = new WorkingTimeCalculator(TimeZoneInfo.Utc);
        var start = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);
        calc.AddWorkingMinutes(Source(), start, 100).Value.Should().Be(start.AddMinutes(100));
    }

    [Fact]
    public void LocalDate_NearMidnight_Success_UsesCompanyZone()
        => Calc().LocalDate(new DateTime(2026, 1, 5, 23, 30, 0, DateTimeKind.Utc)).Should().Be(D(2026, 1, 6));
}
