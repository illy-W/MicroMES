using FluentAssertions;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;
using static MicroMES.ApiTests.Features.PlanningFeature.PlanningTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

public class ForwardFiniteCapacityEngineTests
{
    private static readonly int[] M1 = [1];
    private static readonly int[] M12 = [1, 2];

    // ---- chains, branches, orders -------------------------------------------------------------

    [Fact]
    public void Chain_runs_in_sequence_and_skips_the_lunch_break()
    {
        var r = Plan([Job(1, 120, M1), Job(2, 120, M1, [1]), Job(3, 120, M1, [2])], [Machine(1)]);

        r.Unplanned.Should().BeEmpty();
        r.Of(1).Start.Should().Be(N(2, 8)); r.Of(1).End.Should().Be(N(2, 10));
        r.Of(2).Start.Should().Be(N(2, 10)); r.Of(2).End.Should().Be(N(2, 12));
        r.Of(3).Start.Should().Be(N(2, 13)); r.Of(3).End.Should().Be(N(2, 15));
    }

    [Fact]
    public void Parallel_branches_use_two_machines_and_the_join_waits_for_both()
    {
        var r = Plan(
            [Job(1, 60, M12), Job(2, 120, M12), Job(3, 60, M12, [1, 2])],
            [Machine(1), Machine(2)]);

        r.Of(1).EquipmentId.Should().Be(1);
        r.Of(2).EquipmentId.Should().Be(2); // M1 is busy until 9, M2 is free: it finishes earlier
        r.Of(2).End.Should().Be(N(2, 10));
        r.Of(3).Start.Should().Be(N(2, 10));
        r.Of(3).Reasons.Should().Contain(x => x.Code == PlanningReasonCode.WaitsForPredecessor && x.RelatedWorkOrderId == 2);
    }

    [Fact]
    public void Child_order_last_step_precedes_the_parent_first_step_even_with_a_higher_parent_priority()
    {
        var child = Job(1, 60, M1, order: "CH", priority: 0);
        var parent = Job(2, 60, M1, [1], order: "PA", priority: 9);
        var r = Plan([parent, child], [Machine(1)]);

        r.Of(1).Start.Should().Be(N(2, 8));
        r.Of(2).Start.Should().Be(r.Of(1).End).And.Be(N(2, 9));
    }

    [Fact]
    public void Dependency_across_orders_on_different_machines_starts_at_the_predecessor_end()
    {
        var r = Plan([Job(1, 90, [1], order: "CH"), Job(2, 30, [2], [1], order: "PA")], [Machine(1), Machine(2)]);
        r.Of(2).Start.Should().Be(N(2, 9, 30));
        r.Of(2).End.Should().Be(N(2, 10));
    }

    // ---- processing order ---------------------------------------------------------------------

    [Fact]
    public void Higher_priority_is_planned_first()
    {
        var r = Plan([Job(1, 60, M1, order: "X", priority: 1), Job(2, 60, M1, order: "Y", priority: 5)], [Machine(1)]);
        r.Of(2).Start.Should().Be(N(2, 8));
        r.Of(1).Start.Should().Be(N(2, 9));
        r.Planned.Select(p => p.WorkOrderId).Should().Equal(2, 1);
    }

    [Fact]
    public void Same_priority_nearest_requested_end_goes_first_and_no_deadline_goes_last()
    {
        var r = Plan([
            Job(1, 60, M1, order: "X", reqEnd: N(6, 17)),
            Job(2, 60, M1, order: "Y", reqEnd: N(4, 17)),
            Job(3, 60, M1, order: "Z")], [Machine(1)]);
        r.Planned.Select(p => p.WorkOrderId).Should().Equal(2, 1, 3);
    }

    [Fact]
    public void Same_deadline_requested_start_then_order_code_then_operation_break_the_tie()
    {
        var byStart = Plan([
            Job(1, 60, M1, order: "B", reqStart: N(3, 8), reqEnd: N(6, 17)),
            Job(2, 60, M1, order: "A", reqStart: N(2, 8), reqEnd: N(6, 17))], [Machine(1)]);
        byStart.Planned.Select(p => p.WorkOrderId).Should().Equal(2, 1);

        var byCode = Plan([Job(1, 60, M1, order: "B"), Job(2, 60, M1, order: "A")], [Machine(1)]);
        byCode.Planned.Select(p => p.WorkOrderId).Should().Equal(2, 1);

        var byOp = Plan([Job(1, 60, M1, order: "A", op: 20), Job(2, 60, M1, order: "A", op: 10)], [Machine(1)]);
        byOp.Planned.Select(p => p.WorkOrderId).Should().Equal(2, 1);
    }

    // ---- machine choice -----------------------------------------------------------------------

    [Fact]
    public void Picks_the_machine_that_finishes_first()
    {
        var r = Plan([Job(1, 120, M12, defaultMachine: 1)],
            [Machine(1, busy: new PlanningInterval(N(2, 8), N(2, 12))), Machine(2)]);
        r.Of(1).EquipmentId.Should().Be(2);
        r.Of(1).End.Should().Be(N(2, 10));
    }

    [Fact]
    public void Picks_the_machine_that_finishes_first_even_when_it_starts_later_or_is_slower_to_start()
    {
        // M1 free now but at 50% efficiency (finishes 12:00); M2 free from 09:00 at 100% (finishes 11:00)
        var r = Plan([Job(1, 120, M12, defaultMachine: 1)],
            [Machine(1, efficiency: 50m), Machine(2, busy: new PlanningInterval(N(2, 8), N(2, 9)))]);
        r.Of(1).EquipmentId.Should().Be(2);
        r.Of(1).End.Should().Be(N(2, 11));
    }

    [Fact]
    public void Tie_goes_to_the_default_machine_then_to_the_code()
    {
        Plan([Job(1, 60, M12, defaultMachine: 2)], [Machine(1), Machine(2)]).Of(1).EquipmentId.Should().Be(2);
        Plan([Job(1, 60, M12)], [Machine(2, "AAA"), Machine(1, "BBB")]).Of(1).EquipmentId.Should().Be(2);
        Plan([Job(1, 60, M12)], [Machine(1, "SAL-1"), Machine(2, "SAL-2")]).Of(1).EquipmentId.Should().Be(1);
    }

    [Fact]
    public void Assigned_machine_is_kept_even_when_another_would_finish_earlier()
    {
        var r = Plan([Job(1, 60, M12, assigned: 2)],
            [Machine(1), Machine(2, busy: new PlanningInterval(N(2, 8), N(2, 12)))]);
        r.Of(1).EquipmentId.Should().Be(2);
        r.Of(1).Start.Should().Be(N(2, 13)); // M2 is busy until 12:00, the lunch break follows
        r.Of(1).Reasons.Should().Contain(x => x.Code == PlanningReasonCode.MachineBusy && x.RelatedEquipmentId == 2);
    }

    [Fact]
    public void Assigned_machine_that_is_not_a_candidate_is_replaced_with_a_reason()
    {
        var r = Plan([Job(1, 60, [2], assigned: 1)], [Machine(1), Machine(2)]);
        r.Of(1).EquipmentId.Should().Be(2);
        r.Of(1).Reasons.Should().Contain(x => x.Code == PlanningReasonCode.AssignedMachineNotEnabled);
    }

    [Fact]
    public void Candidates_missing_from_the_equipment_list_are_ignored()
    {
        var r = Plan([Job(1, 60, [9, 1])], [Machine(1)]);
        r.Of(1).EquipmentId.Should().Be(1);
    }

    // ---- capacity -----------------------------------------------------------------------------

    [Fact]
    public void Capacity_one_never_overlaps()
    {
        var r = Plan([Job(1, 120, M1), Job(2, 120, M1, order: "B"), Job(3, 120, M1, order: "C")], [Machine(1)]);
        r.Of(1).End.Should().Be(N(2, 10));
        r.Of(2).Start.Should().Be(N(2, 10));
        r.Of(3).Start.Should().Be(N(2, 13));
    }

    [Fact]
    public void Capacity_two_allows_two_overlapping_jobs_and_queues_the_third()
    {
        var r = Plan([Job(1, 120, M1), Job(2, 120, M1, order: "B"), Job(3, 120, M1, order: "C")], [Machine(1, capacity: 2)]);
        r.Of(1).Start.Should().Be(N(2, 8));
        r.Of(2).Start.Should().Be(N(2, 8));
        r.Of(3).Start.Should().Be(N(2, 10));
        r.Of(3).Reasons.Should().Contain(x => x.Code == PlanningReasonCode.MachineBusy && x.Until == N(2, 10));
    }

    [Fact]
    public void Capacity_two_counts_a_partial_overlap_at_every_instant()
    {
        // two long jobs staggered: A 8-12 (240), B starts when? both cap 2 -> both 8; C needs a free unit for its whole interval
        var r = Plan([Job(1, 240, M1, order: "A"), Job(2, 60, M1, order: "B"), Job(3, 120, M1, order: "C")], [Machine(1, capacity: 2)]);
        r.Of(1).Start.Should().Be(N(2, 8));
        r.Of(2).Start.Should().Be(N(2, 8)); r.Of(2).End.Should().Be(N(2, 9));
        r.Of(3).Start.Should().Be(N(2, 9)); r.Of(3).End.Should().Be(N(2, 11));
    }

    [Fact]
    public void Bottleneck_builds_a_queue_with_machine_busy_reasons()
    {
        var jobs = Enumerable.Range(1, 5).Select(i => Job(i, 60, M1, order: $"O{i}")).ToList();
        var r = Plan(jobs, [Machine(1)]);
        r.Planned.Select(p => p.Start).Should().Equal(N(2, 8), N(2, 9), N(2, 10), N(2, 11), N(2, 13));
        r.Of(1).Reasons.Should().BeEmpty();
        r.Of(2).Reasons.Should().ContainSingle(x => x.Code == PlanningReasonCode.MachineBusy && x.RelatedEquipmentId == 1 && x.Until == N(2, 9));
        r.Of(5).Reasons.Select(x => x.Code).Should().Contain([PlanningReasonCode.MachineBusy]);
    }

    [Fact]
    public void A_job_that_does_not_fit_the_gap_before_a_busy_interval_goes_after_it()
    {
        // busy 09:00-10:00; a 120 min job cannot be split: 08:00-10:00 would overlap -> after 10:00
        var r = Plan([Job(1, 120, M1)], [Machine(1, busy: new PlanningInterval(N(2, 9), N(2, 10)))]);
        r.Of(1).Start.Should().Be(N(2, 10));
        r.Of(1).End.Should().Be(N(2, 12));
    }

    [Fact]
    public void A_short_job_fits_the_gap_before_a_busy_interval()
    {
        var r = Plan([Job(1, 60, M1)], [Machine(1, busy: new PlanningInterval(N(2, 9), N(2, 10)))]);
        r.Of(1).Start.Should().Be(N(2, 8));
        r.Of(1).End.Should().Be(N(2, 9));
    }

    // ---- calendars ----------------------------------------------------------------------------

    [Fact]
    public void A_job_is_one_continuous_interval_that_spans_the_lunch_break()
    {
        var r = Plan([Job(1, 300, M1), Job(2, 60, M1, order: "B")], [Machine(1)]);
        r.Of(1).Start.Should().Be(N(2, 8));
        r.Of(1).End.Should().Be(N(2, 14)); // 4 h + 1 h after the break
        r.Of(2).Start.Should().Be(N(2, 14)); // never slipped into the lunch break or before the end
    }

    [Fact]
    public void Closure_inside_a_job_is_skipped_by_the_calendar()
    {
        var calendar = Source(null, Closed(D(2026, 11, 3), D(2026, 11, 3)));
        var r = Plan([Job(1, 600, M1)], [Machine(1, availability: calendar)]);
        r.Of(1).Start.Should().Be(N(2, 8));
        r.Of(1).End.Should().Be(N(4, 10)); // Monday 8h, Tuesday closed, Wednesday 2h
    }

    [Fact]
    public void Weekend_is_skipped_and_the_reason_says_the_calendar_is_closed()
    {
        var r = Plan([Job(1, 60, M1)], [Machine(1)], now: N(7, 10));
        r.Of(1).Start.Should().Be(N(9, 8));
        r.Of(1).Reasons.Should().ContainSingle(x => x.Code == PlanningReasonCode.CalendarClosed && x.Until == N(9, 8));
    }

    [Fact]
    public void Each_machine_follows_its_own_calendar()
    {
        var morningOnly = Source(Weekdays.Select(d => new WeeklySlot(d, T(8), T(12))).ToList());
        // 300 min: standard calendar ends Monday 14:00; morning-only machine ends Tuesday 09:00
        var r = Plan([Job(1, 300, M12, defaultMachine: 2)], [Machine(1), Machine(2, availability: morningOnly)]);
        r.Of(1).EquipmentId.Should().Be(1);
        r.Of(1).End.Should().Be(N(2, 14));

        // M1 closed on Monday: M2 (morning only) now finishes first
        var closedMonday = Source(null, Closed(D(2026, 11, 2), D(2026, 11, 2)));
        var r2 = Plan([Job(1, 300, M12)], [Machine(1, availability: closedMonday), Machine(2, availability: morningOnly)]);
        r2.Of(1).EquipmentId.Should().Be(2);
        r2.Of(1).End.Should().Be(N(3, 9));
    }

    [Fact]
    public void Efficiency_stretches_or_shrinks_the_duration()
    {
        Plan([Job(1, 60, M1)], [Machine(1, efficiency: 50m)]).Of(1).End.Should().Be(N(2, 10));
        Plan([Job(1, 60, M1)], [Machine(1, efficiency: 200m)]).Of(1).End.Should().Be(N(2, 8, 30));
    }

    [Fact]
    public void Instants_are_whole_minutes_and_now_is_rounded_up()
    {
        var r = Plan([Job(1, 60, M1)], [Machine(1)], now: N(2, 8) + TimeSpan.FromSeconds(30));
        r.Of(1).Start.Should().Be(N(2, 8, 1));
        r.Of(1).End.Should().Be(N(2, 9, 1));
        // 20 minutes at 75% = 26.67 min -> the end is rounded up to the minute
        var r2 = Plan([Job(1, 20, M1)], [Machine(1, efficiency: 75m)]);
        r2.Of(1).End.Should().Be(N(2, 8, 27));
    }

    [Fact]
    public void A_zero_minute_job_does_not_occupy_the_machine()
    {
        var r = Plan([Job(1, 0, M1), Job(2, 60, M1, order: "B")], [Machine(1)]);
        r.Of(1).Start.Should().Be(r.Of(1).End);
        r.Of(2).Start.Should().Be(N(2, 8));
    }

    // ---- fixed jobs ---------------------------------------------------------------------------

    [Fact]
    public void Running_job_is_fixed_and_occupies_the_machine_until_its_estimated_end()
    {
        var r = Plan([Running(1, 120, 1, N(2, 7)), Job(2, 60, M1, order: "B")], [Machine(1)]);
        r.Of(1).IsFixed.Should().BeTrue();
        r.Of(1).Start.Should().Be(N(2, 7));
        r.Of(1).End.Should().Be(N(2, 10)); // remaining 120 min from now (08:00)
        r.Of(1).EquipmentId.Should().Be(1);
        r.Of(2).Start.Should().Be(N(2, 10));
    }

    [Fact]
    public void Successor_of_a_running_job_waits_for_its_estimated_end()
    {
        var r = Plan([Running(1, 90, 1, N(2, 8)), Job(2, 60, [2], [1], order: "B")], [Machine(1), Machine(2)]);
        r.Of(2).Start.Should().Be(N(2, 9, 30));
    }

    [Fact]
    public void Running_job_uses_one_unit_of_capacity_only()
    {
        var r = Plan([Running(1, 240, 1, N(2, 8)), Job(2, 60, M1, order: "B")], [Machine(1, capacity: 2)]);
        r.Of(2).Start.Should().Be(N(2, 8));
    }

    [Fact]
    public void Running_job_is_never_moved_by_a_higher_priority_job()
    {
        var r = Plan([Running(1, 120, 1, N(2, 8)), Job(2, 60, M1, order: "B", priority: 99)], [Machine(1)]);
        r.Of(1).Start.Should().Be(N(2, 8));
        r.Of(2).Start.Should().Be(N(2, 10));
    }

    [Fact]
    public void Fixed_busy_intervals_block_the_machine()
    {
        var r = Plan([Job(1, 60, M1)], [Machine(1, busy: new PlanningInterval(N(2, 8), N(2, 11)))]);
        r.Of(1).Start.Should().Be(N(2, 11));
    }

    [Fact]
    public void Instants_of_unspecified_kind_are_taken_as_utc_and_never_loop()
    {
        // SQLite returns DateTimeKind.Unspecified: two adjacent busy intervals must not send the search backwards.
        static DateTime Unspecified(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
        var busy = new[]
        {
            new PlanningInterval(Unspecified(N(2, 8)), Unspecified(N(2, 10))),
            new PlanningInterval(Unspecified(N(2, 10)), Unspecified(N(2, 11)))
        };
        var r = Plan([Job(1, 120, M1, reqStart: Unspecified(N(2, 8)), closedEnd: Unspecified(N(2, 8)))], [Machine(1, busy: busy)]);
        r.Of(1).Start.Should().Be(N(2, 11));
        r.Of(1).End.Should().Be(N(2, 14)); // 11-12 and 13-14
    }

    // ---- phases without machines --------------------------------------------------------------

    [Fact]
    public void Phase_without_machines_uses_the_default_calendar_with_no_capacity_limit()
    {
        var r = Plan([Job(1, 120, phaseHasMachines: false), Job(2, 120, phaseHasMachines: false, order: "B")], []);
        r.Of(1).EquipmentId.Should().BeNull();
        r.Of(1).Start.Should().Be(N(2, 8)); r.Of(1).End.Should().Be(N(2, 10));
        r.Of(2).Start.Should().Be(N(2, 8)); r.Of(2).End.Should().Be(N(2, 10));
    }

    [Fact]
    public void Phase_without_machines_follows_the_default_calendar_and_dependencies()
    {
        var input = new PlanningInput(
            [Job(1, 60, M1), Job(2, 60, phaseHasMachines: false, after: [1])], [Machine(1)],
            Source(Weekdays.Select(d => new WeeklySlot(d, T(14), T(18))).ToList()), Rome, Now);
        var r = new ForwardFiniteCapacityEngine().Plan(input);
        r.Of(2).Start.Should().Be(N(2, 14));
        r.Of(2).Reasons.Should().Contain(x => x.Code == PlanningReasonCode.CalendarClosed);
    }

    [Fact]
    public void Phase_with_machines_but_no_candidate_is_unplanned_and_its_successor_too()
    {
        var r = Plan([Job(1, 60, [], phaseHasMachines: true), Job(2, 60, M1, [1])], [Machine(1)]);
        r.Planned.Should().BeEmpty();
        r.UnplannedOf(1).Code.Should().Be(UnplannedReasonCode.NoCandidateMachine);
        r.UnplannedOf(2).Code.Should().Be(UnplannedReasonCode.PredecessorUnplanned);
        r.UnplannedOf(2).RelatedWorkOrderId.Should().Be(1);
    }

    // ---- horizon, working time, cycles --------------------------------------------------------

    [Fact]
    public void Job_that_does_not_end_within_the_horizon_is_unplanned()
    {
        var r = Plan([Job(1, 3000, M1), Job(2, 60, M1, order: "B")], [Machine(1)], horizon: TimeSpan.FromDays(3));
        r.UnplannedOf(1).Code.Should().Be(UnplannedReasonCode.HorizonExceeded);
        r.Of(2).Start.Should().Be(N(2, 8)); // a failed job leaves no occupancy
    }

    [Fact]
    public void Job_that_just_fits_the_horizon_is_planned()
    {
        // horizon = 3 days from Mon 08:00 -> Thu 08:00; 16 working hours end Tuesday 17:00
        var r = Plan([Job(1, 960, M1)], [Machine(1)], horizon: TimeSpan.FromDays(3));
        r.Of(1).End.Should().Be(N(3, 17));
    }

    [Fact]
    public void Six_months_horizon_is_the_default()
    {
        // 183 days * ~40 h/week working: a very long job is cut by the horizon, a normal one is not
        var r = Plan([Job(1, 60 * 2000, M1), Job(2, 60, M1, order: "B")], [Machine(1)]);
        r.UnplannedOf(1).Code.Should().Be(UnplannedReasonCode.HorizonExceeded);
        r.Of(2).Start.Should().Be(N(2, 8));
    }

    [Fact]
    public void Calendar_without_working_time_is_unplanned_with_its_own_code()
    {
        var empty = Source([]);
        var r = Plan([Job(1, 60, M1)], [Machine(1, availability: empty)]);
        r.UnplannedOf(1).Code.Should().Be(UnplannedReasonCode.NoWorkingTime);
    }

    [Fact]
    public void A_machine_without_working_time_is_skipped_when_another_one_works()
    {
        var r = Plan([Job(1, 60, M12)], [Machine(1, availability: Source([])), Machine(2)]);
        r.Of(1).EquipmentId.Should().Be(2);
    }

    [Fact]
    public void Phase_without_machines_and_empty_default_calendar_is_unplanned()
    {
        var r = new ForwardFiniteCapacityEngine().Plan(Input([Job(1, 60, phaseHasMachines: false)], [], defaultAvailability: Source([])));
        r.UnplannedOf(1).Code.Should().Be(UnplannedReasonCode.NoWorkingTime);
    }

    [Fact]
    public void Dependency_cycle_is_unplanned_and_dependents_say_why_while_the_rest_is_planned()
    {
        var r = Plan([
            Job(1, 60, M1, [2]), Job(2, 60, M1, [1]), Job(3, 60, M1, [2], order: "C"), Job(4, 60, M1, order: "D")], [Machine(1)]);
        r.UnplannedOf(1).Code.Should().Be(UnplannedReasonCode.CyclicDependency);
        r.UnplannedOf(2).Code.Should().Be(UnplannedReasonCode.CyclicDependency);
        r.UnplannedOf(3).Code.Should().Be(UnplannedReasonCode.PredecessorUnplanned);
        r.Of(4).Start.Should().Be(N(2, 8));
    }

    [Fact]
    public void Self_dependency_and_unknown_predecessors_are_ignored()
    {
        var r = Plan([Job(1, 60, M1, [1, 77])], [Machine(1)]);
        r.Of(1).Start.Should().Be(N(2, 8));
    }

    [Fact]
    public void Unplanned_predecessor_propagates_along_the_chain()
    {
        var r = Plan([Job(1, 60, [], phaseHasMachines: true), Job(2, 60, M1, [1]), Job(3, 60, M1, [2])], [Machine(1)]);
        r.Unplanned.Select(u => u.WorkOrderId).Should().Equal(1, 2, 3);
        r.UnplannedOf(3).RelatedWorkOrderId.Should().Be(2);
    }

    // ---- closed predecessors and reasons ------------------------------------------------------

    [Fact]
    public void Closed_predecessors_end_delays_the_start()
    {
        var r = Plan([Job(1, 60, M1, closedEnd: N(3, 10))], [Machine(1)]);
        r.Of(1).Start.Should().Be(N(3, 10));
        r.Of(1).Reasons.Should().ContainSingle(x => x.Code == PlanningReasonCode.WaitsForPredecessor && x.Until == N(3, 10) && x.RelatedWorkOrderId == null);
    }

    [Fact]
    public void Requested_start_delays_the_job_and_is_the_reason()
    {
        var r = Plan([Job(1, 60, M1, reqStart: N(4, 9))], [Machine(1)]);
        r.Of(1).Start.Should().Be(N(4, 9));
        r.Of(1).Reasons.Should().ContainSingle(x => x.Code == PlanningReasonCode.WaitsForRequestedStart && x.Until == N(4, 9));
    }

    [Fact]
    public void Requested_start_in_the_past_is_ignored()
    {
        var r = Plan([Job(1, 60, M1, reqStart: N(2, 8) - TimeSpan.FromDays(5))], [Machine(1)]);
        r.Of(1).Start.Should().Be(N(2, 8));
        r.Of(1).Reasons.Should().BeEmpty();
    }

    [Fact]
    public void Requested_start_during_lunch_gives_two_reasons_in_order()
    {
        var r = Plan([Job(1, 60, M1, reqStart: N(2, 12, 30))], [Machine(1)]);
        r.Of(1).Start.Should().Be(N(2, 13));
        r.Of(1).Reasons.Select(x => x.Code).Should().Equal(PlanningReasonCode.WaitsForRequestedStart, PlanningReasonCode.CalendarClosed);
    }

    [Fact]
    public void Predecessor_reason_names_the_binding_predecessor_with_its_order_code()
    {
        var r = Plan([Job(1, 60, [1]), Job(2, 180, [2], order: "B", op: 30), Job(3, 60, [3], [1, 2], order: "C")],
            [Machine(1), Machine(2), Machine(3)]);
        var reason = r.Of(3).Reasons.Single(x => x.Code == PlanningReasonCode.WaitsForPredecessor);
        reason.RelatedWorkOrderId.Should().Be(2);
        reason.Message.Should().Contain("B/30");
    }

    [Fact]
    public void A_job_that_starts_right_away_has_no_reasons()
    {
        Plan([Job(1, 60, M1)], [Machine(1)]).Of(1).Reasons.Should().BeEmpty();
    }

    // ---- determinism and performance ----------------------------------------------------------

    private static string Fingerprint(PlanningResult r)
        => string.Join("|", r.Planned.OrderBy(p => p.WorkOrderId).Select(p => $"{p.WorkOrderId}:{p.EquipmentId}:{p.Start:O}:{p.End:O}:{p.Reasons.Count}"))
           + "#" + string.Join("|", r.Unplanned.Select(u => $"{u.WorkOrderId}:{u.Code}"));

    [Fact]
    public void Same_input_gives_the_same_output_even_with_jobs_and_machines_in_another_order()
    {
        var (jobs, machines) = Synthetic(60, 4);
        var engine = new ForwardFiniteCapacityEngine();
        var a = engine.Plan(Input(jobs, machines));
        var b = engine.Plan(Input(jobs, machines));
        var rnd = new Random(7);
        var c = engine.Plan(Input(jobs.OrderBy(_ => rnd.Next()), machines.OrderBy(_ => rnd.Next())));

        Fingerprint(b).Should().Be(Fingerprint(a));
        Fingerprint(c).Should().Be(Fingerprint(a));
        c.Planned.Select(p => p.WorkOrderId).Should().Equal(a.Planned.Select(p => p.WorkOrderId));
    }

    [Fact]
    public void Five_hundred_jobs_on_ten_machines_are_planned_in_under_a_second()
    {
        var (jobs, machines) = Synthetic(500, 10);
        var engine = new ForwardFiniteCapacityEngine();
        engine.Plan(Input(jobs.Take(20), machines)); // warm-up (JIT)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = engine.Plan(Input(jobs, machines));
        sw.Stop();

        (r.Planned.Count + r.Unplanned.Count).Should().Be(500);
        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
        AssertNoCapacityViolation(r, machines);
    }

    private static void AssertNoCapacityViolation(PlanningResult r, IEnumerable<PlanningEquipment> machines)
    {
        foreach (var m in machines)
        {
            var mine = r.Planned.Where(p => p.EquipmentId == m.Id && p.End > p.Start).ToList();
            // at every start instant, the intervals active at that instant never exceed the capacity
            foreach (var p in mine)
                mine.Count(q => q.Start <= p.Start && q.End > p.Start).Should().BeLessThanOrEqualTo(m.Capacity);
        }
    }

    /// <summary>Orders of 5 chained operations rotating over the machines, in 2 phases with alternatives.</summary>
    internal static (List<PlanningJob>, List<PlanningEquipment>) Synthetic(int jobCount, int machineCount)
    {
        var machines = Enumerable.Range(1, machineCount).Select(i => Machine(i, capacity: i % 4 == 0 ? 2 : 1)).ToList();
        var jobs = new List<PlanningJob>();
        for (var i = 1; i <= jobCount; i++)
        {
            var orderIndex = (i - 1) / 5;
            var step = (i - 1) % 5;
            var a = 1 + (i % machineCount);
            var b = 1 + ((i + 3) % machineCount);
            jobs.Add(Job(i, 30 + (i * 7) % 90, a == b ? [a] : [a, b], step == 0 ? null : [i - 1], order: $"O{orderIndex:000}",
                op: (step + 1) * 10, priority: orderIndex % 3, reqEnd: N(2, 17).AddDays(orderIndex % 20), orderId: orderIndex));
        }
        return (jobs, machines);
    }

    // ---- locked jobs --------------------------------------------------------------------------

    [Fact]
    public void Locked_job_is_not_moved_and_the_others_are_planned_around_it()
    {
        var r = Plan([LockedJob(1, 1, N(2, 9), N(2, 11)), Job(2, 120, M1), Job(3, 60, M1)], [Machine(1)]);

        r.Of(1).IsLocked.Should().BeTrue();
        r.Of(1).IsFixed.Should().BeTrue();
        (r.Of(1).Start, r.Of(1).End, r.Of(1).EquipmentId).Should().Be((N(2, 9), N(2, 11), 1));
        r.Of(1).Reasons.Should().ContainSingle(x => x.Code == PlanningReasonCode.Locked && x.Message == "Fissato: non spostato");
        // 120 min do not fit in 08:00-09:00: they go after the locked job.
        r.Of(2).Start.Should().Be(N(2, 11));
        r.Of(3).Start.Should().Be(N(2, 8)); // 60 min fit before the locked job
        r.LockedCount.Should().Be(1);
        r.ConflictCount.Should().Be(0);
    }

    [Fact]
    public void Free_job_that_fits_before_the_locked_one_is_planned_there()
    {
        var r = Plan([LockedJob(1, 1, N(2, 10), N(2, 12)), Job(2, 60, M1)], [Machine(1)]);
        r.Of(2).Start.Should().Be(N(2, 8));
        r.Of(2).End.Should().Be(N(2, 9));
    }

    [Fact]
    public void Successor_of_a_locked_job_starts_after_its_planned_end()
    {
        var r = Plan([LockedJob(1, 1, N(2, 9), N(2, 11)), Job(2, 60, [2], [1])], [Machine(1), Machine(2)]);
        r.Of(2).Start.Should().Be(N(2, 11));
        r.Of(2).Reasons.Should().Contain(x => x.Code == PlanningReasonCode.WaitsForPredecessor && x.RelatedWorkOrderId == 1);
    }

    [Fact]
    public void Predecessor_that_cannot_finish_before_the_locked_start_is_a_conflict_and_the_locked_job_stays()
    {
        // The predecessor needs 180 min from 08:00 (ends 12:00 after the lunch break rules: 08-12 = 240 min, so 08:00-11:00).
        var r = Plan([Job(1, 180, M1), LockedJob(2, 2, N(2, 10), N(2, 12), after: [1])], [Machine(1), Machine(2)]);

        (r.Of(2).Start, r.Of(2).End).Should().Be((N(2, 10), N(2, 12)));
        var c = r.ConflictList.Should().ContainSingle().Subject;
        c.Code.Should().Be(PlanningConflictCode.PredecessorAfterLockedStart);
        (c.WorkOrderId, c.RelatedWorkOrderId, c.Minutes).Should().Be((2, 1, 60));
        r.ConflictCount.Should().Be(1);
    }

    [Fact]
    public void Predecessor_finishing_in_time_is_not_a_conflict()
    {
        var r = Plan([Job(1, 60, M1), LockedJob(2, 2, N(2, 10), N(2, 12), after: [1])], [Machine(1), Machine(2)]);
        r.ConflictList.Should().BeEmpty();
    }

    [Fact]
    public void Capacity_two_machine_hosts_a_locked_job_and_one_free_job_at_the_same_time()
    {
        var r = Plan([LockedJob(1, 1, N(2, 8), N(2, 12)), Job(2, 120, M1), Job(3, 120, M1)], [Machine(1, capacity: 2)]);
        r.Of(2).Start.Should().Be(N(2, 8));
        r.Of(3).Start.Should().Be(N(2, 10)); // both slots of the capacity: locked + job 2, then job 3 when job 2 ends
        r.ConflictList.Should().BeEmpty();
    }

    [Fact]
    public void Locked_jobs_overlapping_beyond_capacity_are_reported_not_fixed()
    {
        var r = Plan([LockedJob(1, 1, N(2, 8), N(2, 11)), LockedJob(2, 1, N(2, 9), N(2, 12))], [Machine(1)]);

        (r.Of(1).Start, r.Of(2).Start).Should().Be((N(2, 8), N(2, 9)));
        var c = r.ConflictList.Should().ContainSingle().Subject;
        c.Code.Should().Be(PlanningConflictCode.LockedOverCapacity);
        c.EquipmentId.Should().Be(1);
        c.WorkOrderIds.Should().Equal(1, 2);
        c.Minutes.Should().Be(120);
    }

    [Fact]
    public void Locked_plan_is_deterministic()
    {
        PlanningResult Run() => Plan([Job(1, 180, M1), LockedJob(2, 1, N(2, 10), N(2, 12), after: [1]), Job(3, 60, M1, [2])], [Machine(1)]);
        var a = Run();
        var b = Run();
        a.Planned.Should().BeEquivalentTo(b.Planned, o => o.WithStrictOrdering());
        a.ConflictList.Should().BeEquivalentTo(b.ConflictList, o => o.WithStrictOrdering());
    }
}
