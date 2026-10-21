using FluentAssertions;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.PlanningFeature.Services;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;
using static MicroMES.ApiTests.Features.PlanningFeature.PlanningTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

public class TimelineCalculatorTests
{
    private static readonly WorkingTimeCalculator Calculator = MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit.Calc();

    // ---- lanes ------------------------------------------------------------------------------------

    [Fact]
    public void AssignLanes_SequentialBars_ShareLaneZero_EvenWhenTheyTouch()
    {
        var lanes = TimelineCalculator.AssignLanes([(1, N(2, 8), N(2, 10)), (2, N(2, 10), N(2, 12)), (3, N(2, 13), N(2, 14))]);
        lanes.Values.Should().OnlyContain(l => l == 0);
    }

    [Fact]
    public void AssignLanes_OverlappingBars_TakeTheLowestFreeLane()
    {
        var lanes = TimelineCalculator.AssignLanes([
            (1, N(2, 8), N(2, 12)), (2, N(2, 9), N(2, 10)), (3, N(2, 9, 30), N(2, 11)), (4, N(2, 10), N(2, 11))]);
        lanes[1].Should().Be(0);
        lanes[2].Should().Be(1);
        lanes[3].Should().Be(2);
        lanes[4].Should().Be(1); // lane 1 became free at 10:00
    }

    [Fact]
    public void AssignLanes_IsDeterministic_RegardlessOfInputOrder_AndTiesGoById()
    {
        var bars = new[] { (2, N(2, 8), N(2, 10)), (1, N(2, 8), N(2, 10)), (3, N(2, 8), N(2, 10)) };
        var a = TimelineCalculator.AssignLanes(bars);
        var b = TimelineCalculator.AssignLanes(bars.Reverse());
        a.Should().BeEquivalentTo(b);
        a[1].Should().Be(0); a[2].Should().Be(1); a[3].Should().Be(2);
    }

    [Fact]
    public void AssignLanes_NoBars_Empty() => TimelineCalculator.AssignLanes([]).Should().BeEmpty();

    // ---- period -----------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, TimelinePeriod.Day)]
    [InlineData(14, TimelinePeriod.Day)]
    [InlineData(15, TimelinePeriod.Week)]
    [InlineData(62, TimelinePeriod.Week)]
    public void PeriodFor_DaysUpToFourteenAreDaily_BeyondAreWeekly(int days, TimelinePeriod expected)
        => TimelineCalculator.PeriodFor(N(2, 0), N(2, 0).AddDays(days)).Should().Be(expected);

    // ---- non working ------------------------------------------------------------------------------

    [Fact]
    public void NonWorking_OneWeekday_NightAndLunchBreak()
    {
        var spans = TimelineCalculator.NonWorking(7, Source(), Calculator, N(2, 0), N(3, 0));
        spans.Select(s => (s.Start, s.End)).Should().Equal((N(2, 0), N(2, 8)), (N(2, 12), N(2, 13)), (N(2, 17), N(3, 0)));
        spans.Should().OnlyContain(s => s.EquipmentId == 7 && s.Kind == TimelineNonWorkingKind.Weekly && s.Reason == null);
    }

    [Fact]
    public void NonWorking_WeekendIsOneSpanFromFridayEveningToMondayMorning()
    {
        var spans = TimelineCalculator.NonWorking(null, Source(), Calculator, N(6, 0), N(10, 0));
        var weekend = spans.Single(s => s.Start == N(6, 17));
        weekend.End.Should().Be(N(9, 8));
        weekend.EquipmentId.Should().BeNull();
    }

    [Fact]
    public void NonWorking_IsClippedToTheWindow()
    {
        var spans = TimelineCalculator.NonWorking(1, Source(), Calculator, N(2, 9), N(2, 14));
        spans.Select(s => (s.Start, s.End)).Should().Equal((N(2, 12), N(2, 13)));
    }

    [Fact]
    public void NonWorking_CalendarClosure_IsTaggedWithItsReasonAndScope()
    {
        var source = Source(null, Closed(D(2026, 11, 3), D(2026, 11, 3), reason: "Ponte"));
        var spans = TimelineCalculator.NonWorking(1, source, Calculator, N(2, 0), N(4, 0));
        var closure = spans.Single(s => s.Kind == TimelineNonWorkingKind.CalendarException);
        closure.Reason.Should().Be("Ponte");
        closure.Start.Should().Be(N(3, 0));
        closure.End.Should().Be(N(4, 0));
        spans.Where(s => s.Kind == TimelineNonWorkingKind.Weekly).Should().HaveCount(3); // Mon: 0-8, 12-13, 17-24
    }

    [Fact]
    public void NonWorking_MachineException_WinsAndIsTaggedAsMachine_CustomHoursLeaveTheirSlotOpen()
    {
        var source = Source(null,
            Custom(D(2026, 11, 3), D(2026, 11, 3), [(T(8), T(12))], AvailabilityLevel.Resource, reason: "Mezza giornata"));
        var spans = TimelineCalculator.NonWorking(1, source, Calculator, N(3, 0), N(4, 0));
        spans.Select(s => (s.Start, s.End, s.Kind, s.Reason)).Should().Equal(
            (N(3, 0), N(3, 8), TimelineNonWorkingKind.MachineException, "Mezza giornata"),
            (N(3, 12), N(4, 0), TimelineNonWorkingKind.MachineException, "Mezza giornata"));
    }

    [Fact]
    public void NonWorking_CalendarWithoutSlots_IsNonWorkingAllWindow()
    {
        var spans = TimelineCalculator.NonWorking(1, Source([]), Calculator, N(2, 0), N(5, 0));
        spans.Should().ContainSingle().Which.Should().Match<TimelineNonWorkingModel>(s => s.Start == N(2, 0) && s.End == N(5, 0));
    }

    [Fact]
    public void NonWorking_SixtyTwoDays_StaysCompact()
    {
        var spans = TimelineCalculator.NonWorking(1, Source(), Calculator, N(2, 0), N(2, 0).AddDays(62));
        spans.Count.Should().BeLessThan(62 * 3);
    }

    // ---- load -------------------------------------------------------------------------------------

    [Fact]
    public void Load_Daily_IsBusyOverCapacityMinutes()
    {
        var load = TimelineCalculator.Load(1, 1, Source(), Calculator, [(N(2, 8), N(2, 12))], N(2, 0), N(4, 0), TimelinePeriod.Day);
        load.Should().HaveCount(2);
        load[0].BusyMinutes.Should().Be(240);
        load[0].CapacityMinutes.Should().Be(480);
        load[0].LoadPercent.Should().Be(50m);
        load[0].PeriodStart.Should().Be(N(2, 0));
        load[0].PeriodEnd.Should().Be(N(3, 0));
        load[1].LoadPercent.Should().Be(0m);
    }

    [Fact]
    public void Load_ABarOverTheBreakCountsOnlyWorkingMinutes()
    {
        var load = TimelineCalculator.Load(1, 1, Source(), Calculator, [(N(2, 8), N(2, 14))], N(2, 0), N(3, 0), TimelinePeriod.Day);
        load.Single().BusyMinutes.Should().Be(300); // 4 h + 1 h, the lunch break is not worked
    }

    [Fact]
    public void Load_CapacityTwo_DoublesTheCapacity_AndOverlapsBeyondItGoOverOneHundred()
    {
        var bar = (N(2, 8), N(2, 17));
        TimelineCalculator.Load(1, 2, Source(), Calculator, [bar], N(2, 0), N(3, 0), TimelinePeriod.Day).Single().LoadPercent.Should().Be(50m);
        TimelineCalculator.Load(1, 1, Source(), Calculator, [bar, bar], N(2, 0), N(3, 0), TimelinePeriod.Day).Single().LoadPercent.Should().Be(200m);
    }

    [Fact]
    public void Load_Weekly_PeriodsAreMondayToSunday_AndClippedToTheWindow()
    {
        var load = TimelineCalculator.Load(1, 1, Source(), Calculator, [(N(2, 8), N(2, 17))], N(4, 12), N(16, 0), TimelinePeriod.Week);
        // Window Wed 4 12:00 -> Mon 16: week of 2 Nov (from Wed), week of 9 Nov, week of 16 Nov (empty, window ends at 16 Nov 00:00 -> not included).
        load.Should().HaveCount(2);
        load[0].PeriodStart.Should().Be(N(4, 12));
        load[0].PeriodEnd.Should().Be(N(9, 0));
        load[0].BusyMinutes.Should().Be(0); // the bar is before the window
        load[1].PeriodStart.Should().Be(N(9, 0));
        load[1].CapacityMinutes.Should().Be(2400);
    }

    [Fact]
    public void Load_BarsAcrossPeriods_AreSplitByWorkingMinutes()
    {
        var load = TimelineCalculator.Load(1, 1, Source(), Calculator, [(N(2, 13), N(3, 10))], N(2, 0), N(4, 0), TimelinePeriod.Day);
        load[0].BusyMinutes.Should().Be(240); // Monday 13-17
        load[1].BusyMinutes.Should().Be(120); // Tuesday 8-10
    }

    [Fact]
    public void Load_NonWorkingDay_HasZeroCapacityAndZeroPercent()
    {
        var load = TimelineCalculator.Load(1, 1, Source(), Calculator, [(N(7, 8), N(7, 10))], N(7, 0), N(8, 0), TimelinePeriod.Day);
        load.Single().CapacityMinutes.Should().Be(0);
        load.Single().LoadPercent.Should().Be(0m);
    }
}
