using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.PlanningFeature.Alerts;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;
using static MicroMES.ApiTests.Features.PlanningFeature.PlanningTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

/// <summary>Tooling alerts and tooling conflicts of the manual move (pure). Nov 2026: Mon 2.</summary>
public class PlanToolingAlertsTests
{
    private static readonly WorkingTimeCalculator Calculator = MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit.Calc();

    private static AlertWorkOrder Wo(int id, DateTime? start, DateTime? end, int machine = 1, bool locked = true, params int[] tooling)
        => new(id, $"OP-1/{id}", 1, "OP-1", null, WorkOrderStatus.Ready, start, end, machine, $"M{machine}", true, [], locked, tooling);

    private static List<PlanAlertModel> Run(IEnumerable<AlertWorkOrder> wos, params AlertTooling[] tooling)
        => PlanAlertsCalculator.Calculate(wos.ToList(), [new AlertMachine(1, "M1", 1, Source()), new AlertMachine(2, "M2", 1, Source())],
            [], Calculator, Now, tooling);

    [Fact]
    public void Overbooked_LockedBeyondQuantity_Warning_NamesToolingAndWorkOrders()
    {
        var alerts = Run([Wo(1, N(3, 8), N(3, 12), 1, true, 7), Wo(2, N(3, 10), N(3, 14), 2, true, 7)], new AlertTooling(7, "ST-01", 1, true, []));
        var a = alerts.Single(x => x.Type == PlanAlertType.ToolingOverbooked);
        a.Severity.Should().Be(PlanAlertSeverity.Warning);
        a.ToolingCode.Should().Be("ST-01");
        a.WorkOrderIds.Should().Equal(1, 2);
        a.Message.Should().Contain("ST-01").And.Contain("OP-1/1").And.Contain("OP-1/2");
        a.Suggestion.Should().Be("Sposta uno dei due lavori o aumenta la quantità dell'attrezzatura.");
    }

    [Fact]
    public void Overbooked_WithinQuantityOrNotFixed_NoAlert()
    {
        Run([Wo(1, N(3, 8), N(3, 12), 1, true, 7), Wo(2, N(3, 10), N(3, 14), 2, true, 7)], new AlertTooling(7, "ST-01", 2, true, []))
            .Should().BeEmpty();
        Run([Wo(1, N(3, 8), N(3, 12), 1, true, 7), Wo(2, N(3, 10), N(3, 14), 2, false, 7)], new AlertTooling(7, "ST-01", 1, true, []))
            .Should().NotContain(a => a.Type == PlanAlertType.ToolingOverbooked);
    }

    [Fact]
    public void IncompatibleMachine_Critical_SuggestsCompatibleMachine()
    {
        var alerts = Run([Wo(1, N(3, 8), N(3, 12), 2, false, 7)], new AlertTooling(7, "ST-01", 1, true, [1]));
        var a = alerts.Single(x => x.Type == PlanAlertType.ToolingIncompatibleMachine);
        a.Severity.Should().Be(PlanAlertSeverity.Critical);
        a.EquipmentCode.Should().Be("M2");
        a.Suggestion.Should().Be("Scegli una macchina compatibile con ST-01.");
    }

    [Fact]
    public void Inactive_OneAlertPerTooling_ListsWorkOrders()
    {
        var alerts = Run([Wo(1, N(3, 8), N(3, 12), 1, false, 7), Wo(2, N(4, 8), N(4, 12), 1, false, 7)], new AlertTooling(7, "ST-01", 1, false, []));
        var a = alerts.Single(x => x.Type == PlanAlertType.ToolingInactive);
        a.Severity.Should().Be(PlanAlertSeverity.Warning);
        a.WorkOrderIds.Should().Equal(1, 2);
        a.Message.Should().Contain("ST-01").And.Contain("OP-1/1, OP-1/2");
        a.Suggestion.Should().Be("Riattiva l'attrezzatura o cambia il passo di ciclo.");
    }

    [Fact]
    public void Unplannable_ToolingReasons_BecomeUnplannableForTooling()
    {
        var alerts = PlanAlertsCalculator.Calculate([], [], [
            new AlertUnplannable(1, "OP-1/1", 1, "OP-1", UnplannedReasonCode.ToolingInactive, "L'attrezzatura ST-01 è disattivata.", null),
            new AlertUnplannable(2, "OP-1/2", 1, "OP-1", UnplannedReasonCode.NoCandidateMachine, "x", null)], Calculator, Now);
        var a = alerts.Single(x => x.Type == PlanAlertType.UnplannableForTooling);
        a.Severity.Should().Be(PlanAlertSeverity.Critical);
        a.WorkOrderId.Should().Be(1);
        a.Suggestion.Should().Be("Riattiva l'attrezzatura o cambia il passo di ciclo.");
        alerts.Should().ContainSingle(x => x.Type == PlanAlertType.Unplannable && x.WorkOrderId == 2);
    }

    // ---- manual move conflicts ----------------------------------------------------------------

    private static List<MoveConflictModel> Move(MoveToolingUse use, DateTime start, DateTime end)
        => ManualMoveCalculator.FindConflicts(Rome, new MoveJobRef(1, "OP-1/1", start, end), start, end, [], [], 9, "SAL-1", 1, [], null, [use]);

    [Fact]
    public void Move_ToolingOverCapacity_NamesOtherWorkOrders_NonBlockingData()
    {
        var c = Move(new MoveToolingUse(7, "ST-01", 1, false, [new MoveJobRef(2, "OP-2/1", N(3, 9), N(3, 11))]), N(3, 8), N(3, 10));
        var x = c.Single(k => k.Code == MoveConflictCode.ToolingOverCapacity);
        x.WorkOrderCodes.Should().Equal("OP-1/1", "OP-2/1");
        x.Minutes.Should().Be(60);
        x.Message.Should().Contain("ST-01").And.Contain("Sposta uno dei due lavori");
    }

    [Fact]
    public void Move_ToolingWithEnoughQuantityOrNoOverlap_NoConflict()
    {
        Move(new MoveToolingUse(7, "ST-01", 2, false, [new MoveJobRef(2, "OP-2/1", N(3, 9), N(3, 11))]), N(3, 8), N(3, 10)).Should().BeEmpty();
        Move(new MoveToolingUse(7, "ST-01", 1, false, [new MoveJobRef(2, "OP-2/1", N(3, 10), N(3, 11))]), N(3, 8), N(3, 10)).Should().BeEmpty();
    }

    [Fact]
    public void Move_ToolingIncompatible_NamesMachineAndTooling()
    {
        var c = Move(new MoveToolingUse(7, "ST-01", 1, true, []), N(3, 8), N(3, 10));
        var x = c.Single(k => k.Code == MoveConflictCode.ToolingIncompatible);
        x.Message.Should().Contain("SAL-1").And.Contain("ST-01");
    }
}
