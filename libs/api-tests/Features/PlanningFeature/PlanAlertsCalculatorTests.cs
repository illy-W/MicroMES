using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.PlanningFeature.Alerts;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;
using static MicroMES.ApiTests.Features.PlanningFeature.PlanningTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

public class PlanAlertsCalculatorTests
{
    private static readonly WorkingTimeCalculator Calculator = MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit.Calc();

    private static AlertWorkOrder Wo(int id, DateTime? start, DateTime? end, int order = 1, DateTime? due = null,
        WorkOrderStatus status = WorkOrderStatus.Waiting, int? machine = 1, bool usable = true, params AlertPredecessor[] preds)
        => new(id, $"OP-{order}/{id}", order, $"OP-{order}", due, status, start, end, machine, machine is null ? null : $"M{machine}", usable, preds);

    private static AlertWorkOrder Locked(int id, DateTime start, DateTime end, int order = 1, DateTime? due = null, int? machine = 1,
        bool usable = true, params AlertPredecessor[] preds)
        => Wo(id, start, end, order, due, WorkOrderStatus.Ready, machine, usable, preds) with { IsLocked = true };

    private static AlertMachine M(int id = 1, int capacity = 1) => new(id, $"M{id}", capacity, Source());

    private static List<PlanAlertModel> Run(IEnumerable<AlertWorkOrder> wos, IEnumerable<AlertMachine>? machines = null,
        IEnumerable<AlertUnplannable>? unplannable = null, DateTime? now = null)
        => PlanAlertsCalculator.Calculate(wos.ToList(), (machines ?? [M()]).ToList(), (unplannable ?? []).ToList(), Calculator, now ?? Now);

    [Fact]
    public void Healthy_plan_has_no_alerts()
        => Run([Wo(1, N(2, 8), N(2, 10), due: N(6, 17))]).Should().BeEmpty();

    // ---- Late ---------------------------------------------------------------------------------

    [Fact]
    public void Late_order_names_last_job_with_days_rounded_up()
    {
        var alerts = Run([Wo(1, N(2, 8), N(2, 10), due: N(3, 17)), Wo(2, N(4, 8), N(4, 12), due: N(3, 17))]);
        var late = alerts.Single(a => a.Type == PlanAlertType.Late);
        late.Severity.Should().Be(PlanAlertSeverity.Critical);
        late.LateDays.Should().Be(1); // 19 hours
        late.WorkOrderCode.Should().Be("OP-1/2");
        late.EquipmentCode.Should().Be("M1");
        late.Message.Should().Contain("OP-1").And.Contain("1 giorno").And.Contain("M1");
        late.Suggestion.Should().NotBeEmpty();
        late.Date.Should().Be(N(4, 12));
    }

    [Fact]
    public void Late_is_one_alert_per_order_and_not_raised_when_on_time_or_without_deadline()
    {
        var alerts = Run([
            Wo(1, N(2, 8), N(5, 10), 1, due: N(3, 17)), Wo(2, N(5, 10), N(6, 10), 1, due: N(3, 17)),
            Wo(3, N(2, 8), N(2, 9), 2, due: N(9, 17)), Wo(4, N(2, 8), N(9, 9), 3)]);
        alerts.Where(a => a.Type == PlanAlertType.Late).Should().ContainSingle(a => a.ProductionOrderId == 1);
        alerts.Single(a => a.Type == PlanAlertType.Late).LateDays.Should().Be(3); // Fri 6 10:00 vs Tue 3 17:00 = 2 d 17 h, rounded up
    }

    [Fact]
    public void Planned_end_exactly_on_the_deadline_is_not_late()
        => Run([Wo(1, N(2, 8), N(2, 10), due: N(2, 10))]).Should().BeEmpty();

    // ---- Unplannable --------------------------------------------------------------------------

    [Theory]
    [InlineData(UnplannedReasonCode.NoWorkingTime, "calendario")]
    [InlineData(UnplannedReasonCode.HorizonExceeded, "6 mesi")]
    [InlineData(UnplannedReasonCode.NoCandidateMachine, "macchina")]
    [InlineData(UnplannedReasonCode.CyclicDependency, "dipendenze")]
    [InlineData(UnplannedReasonCode.PredecessorUnplanned, "ripianifica")]
    public void Unplannable_alert_carries_the_engine_reason_and_a_suggestion_per_reason(UnplannedReasonCode code, string hint)
    {
        var alert = Run([], unplannable: [new AlertUnplannable(5, "OP-2/5", 2, "OP-2", code, "Motivo del motore.", 4)]).Single();
        alert.Type.Should().Be(PlanAlertType.Unplannable);
        alert.Severity.Should().Be(PlanAlertSeverity.Critical);
        alert.Message.Should().Contain("OP-2/5").And.Contain("Motivo del motore.");
        alert.Suggestion.ToLowerInvariant().Should().Contain(hint);
        alert.CauseWorkOrderId.Should().Be(4);
    }

    // ---- Stale / machine ----------------------------------------------------------------------

    [Fact]
    public void Stale_when_a_predecessor_ends_after_the_planned_start_naming_it()
    {
        var alert = Run([Wo(2, N(2, 10), N(2, 12), preds: new AlertPredecessor(1, "OP-1/1", N(2, 11)))]).Single();
        alert.Type.Should().Be(PlanAlertType.StalePlan);
        alert.Severity.Should().Be(PlanAlertSeverity.Warning);
        alert.CauseWorkOrderId.Should().Be(1);
        alert.Message.Should().Contain("OP-1/1").And.Contain("OP-1/2");
    }

    [Fact]
    public void Predecessor_ending_exactly_at_the_start_is_coherent()
        => Run([Wo(2, N(2, 10), N(2, 12), preds: new AlertPredecessor(1, "OP-1/1", N(2, 10)))]).Should().BeEmpty();

    [Fact]
    public void Stale_when_a_running_job_is_past_its_planned_end()
    {
        var alert = Run([Wo(1, N(2, 8), N(2, 10), status: WorkOrderStatus.Running)], now: N(2, 11)).Single();
        alert.Type.Should().Be(PlanAlertType.StalePlan);
        alert.Message.Should().Contain("in corso");
        Run([Wo(1, N(2, 8), N(2, 10), status: WorkOrderStatus.Running)], now: N(2, 9)).Should().BeEmpty();
    }

    [Fact]
    public void Disabled_machine_with_open_work_is_critical_and_not_reported_twice_as_stale()
    {
        var alerts = Run([Wo(1, N(2, 8), N(2, 10), usable: false)]);
        var alert = alerts.Single();
        alert.Type.Should().Be(PlanAlertType.InactiveOrDisabledMachine);
        alert.Severity.Should().Be(PlanAlertSeverity.Critical);
        alert.EquipmentCode.Should().Be("M1");
        alert.Message.Should().Contain("M1");
    }

    [Fact]
    public void Disabled_machine_is_reported_also_for_unplanned_work_orders_but_not_for_running_ones()
    {
        Run([Wo(1, null, null, usable: false)]).Should().ContainSingle(a => a.Type == PlanAlertType.InactiveOrDisabledMachine);
        Run([Wo(1, N(2, 8), N(2, 10), status: WorkOrderStatus.Running, usable: false)], now: N(2, 9)).Should().BeEmpty();
    }

    [Fact]
    public void Work_order_without_machine_never_raises_a_machine_alert()
        => Run([Wo(1, N(2, 8), N(2, 10), machine: null, usable: true)]).Should().BeEmpty();

    // ---- Overload -----------------------------------------------------------------------------

    [Fact]
    public void Overloaded_machine_when_overlapping_plans_exceed_the_week_capacity()
    {
        // Mon-Fri 8h = 40h of capacity per week; 3 jobs of 16 working hours each overlap in the same week = 48h > 40h.
        var wos = new[]
        {
            Wo(1, N(2, 8), N(3, 17)), Wo(2, N(2, 8), N(3, 17)), Wo(3, N(2, 8), N(3, 17))
        };
        var alert = Run(wos).Single(a => a.Type == PlanAlertType.MachineOverloaded);
        alert.Severity.Should().Be(PlanAlertSeverity.Warning);
        alert.EquipmentCode.Should().Be("M1");
        alert.LoadPercent.Should().Be(120m);
        alert.Date.Should().Be(N(2, 0));
        alert.Message.Should().Contain("M1").And.Contain("120%");
    }

    [Fact]
    public void Exactly_full_week_is_not_overloaded()
        => Run([Wo(1, N(2, 8), N(6, 17))]).Should().BeEmpty();

    [Fact]
    public void Capacity_two_doubles_the_threshold()
    {
        var wos = new[] { Wo(1, N(2, 8), N(6, 17)), Wo(2, N(2, 8), N(6, 17)) };
        Run(wos, [M(1, 2)]).Should().BeEmpty();
        Run(wos.Append(Wo(3, N(2, 8), N(6, 17))), [M(1, 2)]).Should().ContainSingle(a => a.Type == PlanAlertType.MachineOverloaded);
    }

    [Fact]
    public void Overload_is_evaluated_per_week_and_ignores_the_load_of_other_weeks()
    {
        // week 1 is full (one job), week 2 has an extra overlapping job only on Monday: 8h + 8h in a 40h week = fine
        var wos = new[] { Wo(1, N(2, 8), N(6, 17)), Wo(2, N(9, 8), N(9, 17)), Wo(3, N(9, 8), N(9, 17)) };
        Run(wos).Should().BeEmpty();
    }

    // ---- Order --------------------------------------------------------------------------------

    [Fact]
    public void Alerts_are_ordered_critical_first_then_by_date()
    {
        var alerts = Run([
            Wo(1, N(4, 10), N(4, 12), 1, preds: new AlertPredecessor(9, "OP-9/9", N(4, 11))),          // stale (warning), date Wed 10
            Wo(2, N(2, 8), N(2, 10), 2, due: N(2, 9)),                                                  // late (critical), date Mon 10
            Wo(3, N(3, 8), N(3, 10), 3, usable: false)],                                                // disabled (critical), date Tue 8
            unplannable: [new AlertUnplannable(7, "OP-7/7", 7, "OP-7", UnplannedReasonCode.NoWorkingTime, "x", null)]); // critical, no date
        alerts.Select(a => a.Type).Should().Equal(
            PlanAlertType.Late, PlanAlertType.InactiveOrDisabledMachine, PlanAlertType.Unplannable, PlanAlertType.StalePlan);
    }

    [Fact]
    public void Calculation_is_deterministic()
    {
        WorkOrder_Set().Should().BeEquivalentTo(WorkOrder_Set(), o => o.WithStrictOrdering());
    }

    private static List<PlanAlertModel> WorkOrder_Set() => Run([
        Wo(1, N(2, 8), N(5, 10), 1, due: N(3, 17)), Wo(2, N(3, 8), N(3, 10), 2, usable: false), Wo(3, N(2, 8), N(2, 9), 3)]);

    // ---- Locked work orders -------------------------------------------------------------------

    [Fact]
    public void Locked_predecessor_conflict_is_critical_names_predecessor_and_minutes_and_replaces_stale()
    {
        var alerts = Run([Locked(2, N(2, 10), N(2, 12), preds: new AlertPredecessor(1, "OP-1/1", N(2, 11)))]);
        var a = alerts.Single();
        a.Type.Should().Be(PlanAlertType.LockedPredecessorConflict);
        a.Severity.Should().Be(PlanAlertSeverity.Critical);
        a.CauseWorkOrderId.Should().Be(1);
        a.Message.Should().Contain("OP-1/1").And.Contain("OP-1/2").And.Contain("60 min");
        a.Suggestion.Should().Be("Sblocca il lavoro o anticipa il predecessore.");
    }

    [Fact]
    public void Locked_on_inactive_machine_is_critical_and_not_reported_as_generic_machine_alert()
    {
        var a = Run([Locked(1, N(2, 8), N(2, 10), usable: false)]).Single();
        a.Type.Should().Be(PlanAlertType.LockedMachineUnavailable);
        a.Severity.Should().Be(PlanAlertSeverity.Critical);
        a.Message.Should().Contain("M1").And.Contain("disattivata");
        a.Suggestion.Should().Be("Assegna un'altra macchina o sblocca il lavoro.");
    }

    [Fact]
    public void Locked_start_on_closed_time_of_the_machine_calendar_names_the_reason()
    {
        // Saturday 7: no shifts on the weekend.
        var a = Run([Locked(1, N(7, 10), N(7, 12))]).Single();
        a.Type.Should().Be(PlanAlertType.LockedMachineUnavailable);
        a.Message.Should().Contain("M1").And.Contain("giorno senza turni");
    }

    [Fact]
    public void Locked_jobs_beyond_capacity_name_the_other_jobs_as_warning()
    {
        var alerts = Run([Locked(1, N(2, 8), N(2, 11)), Locked(2, N(2, 9), N(2, 12), order: 2)]);
        var a = alerts.Single(x => x.Type == PlanAlertType.LockedOverCapacity);
        a.Severity.Should().Be(PlanAlertSeverity.Warning);
        a.Message.Should().Contain("OP-1/1").And.Contain("OP-2/2");
        a.Suggestion.Should().Be("Sposta uno dei lavori fissati o aumenta la capacità.");
        Run([Locked(1, N(2, 8), N(2, 11)), Locked(2, N(2, 9), N(2, 12), order: 2)], [M(capacity: 2)])
            .Should().NotContain(x => x.Type == PlanAlertType.LockedOverCapacity);
    }

    [Fact]
    public void Locked_job_ending_after_the_requested_end_is_a_warning_with_days()
    {
        var a = Run([Locked(1, N(2, 8), N(4, 12), due: N(3, 17))]).Single(x => x.Type == PlanAlertType.LockedLate);
        a.Severity.Should().Be(PlanAlertSeverity.Warning);
        a.LateDays.Should().Be(1);
        a.Suggestion.Should().Be("Anticipa l'inizio o sposta la fine richiesta.");
    }

    [Fact]
    public void Healthy_locked_job_has_no_alerts()
        => Run([Locked(1, N(2, 8), N(2, 10), due: N(6, 17))]).Should().BeEmpty();

    [Fact]
    public void Plan_status_reason_is_distinct_for_locked_work_orders()
    {
        var facts = new MicroMES.ApiFeatures.ProductionOrderFeature.Planning.WorkPlanFacts(WorkOrderStatus.Ready, N(2, 10), N(2, 12), null,
            [N(2, 11)], true, N(2, 0), IsLocked: true);
        MicroMES.ApiFeatures.ProductionOrderFeature.Planning.PlanStatusCalculator.CauseOf(facts)
            .Should().Be(MicroMES.ApiFeatures.ProductionOrderFeature.Planning.PlanStatusCalculator.StaleCause.LockedPredecessorConflict);
        MicroMES.ApiFeatures.ProductionOrderFeature.Planning.PlanStatusCalculator.CauseOf(facts with { IsLocked = false })
            .Should().Be(MicroMES.ApiFeatures.ProductionOrderFeature.Planning.PlanStatusCalculator.StaleCause.PredecessorEndsAfterStart);
    }
}
