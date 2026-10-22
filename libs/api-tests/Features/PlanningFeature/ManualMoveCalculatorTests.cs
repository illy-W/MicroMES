using FluentAssertions;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

/// <summary>Start validity, end computation and conflicts of the manual move (pure). Nov 2026: Mon 2, Fri 6, Sat 7.</summary>
public class ManualMoveCalculatorTests
{
    private static MoveTiming Eval(DateTime start, decimal minutes, decimal eff = 100m, IAvailabilitySource? src = null)
        => ManualMoveCalculator.Evaluate(Calc(), src ?? Source(), "SAL-1", start, minutes, eff);

    [Fact]
    public void Evaluate_WorkingStart_Success_EndOnCalendar()
    {
        var t = Eval(Local(2026, 11, 3, 10), 120);
        t.Error.Should().BeNull();
        t.End.Should().Be(Local(2026, 11, 3, 12));
    }

    [Fact]
    public void Evaluate_AcrossBreak_Success_SkipsLunch()
    {
        Eval(Local(2026, 11, 3, 11), 120).End.Should().Be(Local(2026, 11, 3, 14));
    }

    [Fact]
    public void Evaluate_Efficiency50_Success_DoublesDuration()
    {
        var t = Eval(Local(2026, 11, 3, 8), 120, 50m);
        t.WorkMinutes.Should().Be(240m);
        t.End.Should().Be(Local(2026, 11, 3, 12));
    }

    [Fact]
    public void Evaluate_OutOfShift_Failure_NamesShiftsAndSuggestsNext()
    {
        var t = Eval(Local(2026, 11, 3, 12, 30), 60);
        t.End.Should().BeNull();
        t.Error.Should().Be("SAL-1 è fuori turno il 03/11: turni 08:00-12:00, 13:00-17:00. Prossimo orario disponibile: 03/11 13:00");
    }

    [Fact]
    public void Evaluate_Weekend_Failure_NoShiftsDay()
    {
        var t = Eval(Local(2026, 11, 7, 10), 60);
        t.Error.Should().Be("SAL-1 non lavora il 07/11: giorno senza turni. Prossimo orario disponibile: 09/11 08:00");
    }

    [Fact]
    public void Evaluate_ClosedDay_Failure_NamesReasonAndNextWorkingInstant()
    {
        var src = Source(null, Closed(D(2026, 11, 3), D(2026, 11, 3), reason: "chiusura aziendale"));
        var t = Eval(Local(2026, 11, 3, 10), 60, src: src);
        t.Error.Should().Be("SAL-1 è chiusa il 03/11: chiusura aziendale. Prossimo orario disponibile: 04/11 08:00");
    }

    [Fact]
    public void Evaluate_NoWorkingTime_Failure()
    {
        var t = Eval(Local(2026, 11, 3, 10), 60, src: Source([]));
        t.Error.Should().NotBeNull();
        t.End.Should().BeNull();
    }

    private static MoveJobRef J(int id, DateTime? s, DateTime? e) => new(id, $"OP-{id}/10", s, e);

    private static List<MoveConflictModel> Conflicts(DateTime start, DateTime end, MoveJobRef[]? pred = null, MoveJobRef[]? succ = null,
        MoveJobRef[]? occ = null, int cap = 1, DateTime? due = null)
        => ManualMoveCalculator.FindConflicts(Rome, J(1, start, end), start, end, pred ?? [], succ ?? [], 9, "SAL-1", cap, occ ?? [], due);

    [Fact]
    public void Conflicts_PredecessorEndsAfterStart_Success_MinutesInMessage()
    {
        var c = Conflicts(Local(2026, 11, 3, 10), Local(2026, 11, 3, 12), pred: [J(2, null, Local(2026, 11, 3, 11))]);
        var x = c.Should().ContainSingle().Subject;
        x.Code.Should().Be(MoveConflictCode.PredecessorEndsAfterStart);
        x.Minutes.Should().Be(60);
        x.Message.Should().Contain("OP-2/10").And.Contain("60 min");
    }

    [Fact]
    public void Conflicts_PredecessorBeforeStart_Success_None()
        => Conflicts(Local(2026, 11, 3, 10), Local(2026, 11, 3, 12), pred: [J(2, null, Local(2026, 11, 3, 10))]).Should().BeEmpty();

    [Fact]
    public void Conflicts_SuccessorsStartBeforeEnd_Success_ListsCodes()
    {
        var c = Conflicts(Local(2026, 11, 3, 10), Local(2026, 11, 3, 14),
            succ: [J(3, Local(2026, 11, 3, 13), Local(2026, 11, 3, 15)), J(4, Local(2026, 11, 3, 14), null), J(5, null, null)]);
        var x = c.Should().ContainSingle().Subject;
        x.Code.Should().Be(MoveConflictCode.SuccessorsStartBeforeEnd);
        x.WorkOrderCodes.Should().Equal("OP-3/10");
    }

    [Fact]
    public void Conflicts_OverCapacity_Success_InvolvedAndInterval()
    {
        var c = Conflicts(Local(2026, 11, 3, 10), Local(2026, 11, 3, 12),
            occ: [J(7, Local(2026, 11, 3, 9), Local(2026, 11, 3, 11)), J(8, Local(2026, 11, 3, 15), Local(2026, 11, 3, 16))]);
        var x = c.Should().ContainSingle().Subject;
        x.Code.Should().Be(MoveConflictCode.MachineOverCapacity);
        x.WorkOrderIds.Should().Equal(1, 7);
        (x.From, x.To).Should().Be((Local(2026, 11, 3, 10), Local(2026, 11, 3, 11)));
        x.Minutes.Should().Be(60);
        x.Message.Should().Contain("capacità (1)");
    }

    [Fact]
    public void Conflicts_CapacityTwo_Success_OneOccupantIsFine()
        => Conflicts(Local(2026, 11, 3, 10), Local(2026, 11, 3, 12),
            occ: [J(7, Local(2026, 11, 3, 9), Local(2026, 11, 3, 11))], cap: 2).Should().BeEmpty();

    [Fact]
    public void Conflicts_Late_Success_Days()
    {
        var c = Conflicts(Local(2026, 11, 3, 10), Local(2026, 11, 5, 12), due: Local(2026, 11, 4, 12));
        var x = c.Should().ContainSingle().Subject;
        x.Code.Should().Be(MoveConflictCode.Late);
        x.LateDays.Should().Be(1);
    }
}
