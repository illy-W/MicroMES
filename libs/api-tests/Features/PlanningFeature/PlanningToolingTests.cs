using FluentAssertions;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using static MicroMES.ApiTests.Features.PlanningFeature.PlanningTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

/// <summary>REQ-5: tooling compatibility, quantity, sequence-dependent setup and machine choice.</summary>
public class PlanningToolingTests
{
    private static readonly JobTooling[] St = [Tl(1, "ST-01", 10)];

    // ---- compatibility ------------------------------------------------------------------------

    [Fact]
    public void Job_goes_only_on_a_machine_compatible_with_its_tooling()
    {
        var r = PlanTool([Tooled(1, 60, [1, 2], St, defaultMachine: 1)], [Machine(1), Machine(2)], Res(1, "ST-01", 1, true, 2));
        r.Of(1).EquipmentId.Should().Be(2);
    }

    [Fact]
    public void Job_needs_a_machine_compatible_with_all_its_tooling()
    {
        var both = new[] { Tl(1, "ST-01"), Tl(2, "ST-02") };
        var r = PlanTool([Tooled(1, 60, [1, 2], both)], [Machine(1), Machine(2)], Res(1, "ST-01", 1, true, 1, 2), Res(2, "ST-02", 1, true, 1));
        r.Of(1).EquipmentId.Should().Be(1);
    }

    [Fact]
    public void Inactive_tooling_makes_the_job_unplannable_naming_it()
    {
        var r = PlanTool([Tooled(1, 60, [1], St)], [Machine(1)], Res(1, "ST-01", 1, false));
        var u = r.UnplannedOf(1);
        u.Code.Should().Be(UnplannedReasonCode.ToolingInactive);
        u.Message.Should().Contain("ST-01");
    }

    [Fact]
    public void No_compatible_machine_makes_the_job_unplannable_naming_the_tooling()
    {
        var r = PlanTool([Tooled(1, 60, [2], St), Job(2, 30, [2], [1])], [Machine(1), Machine(2)], Res(1, "ST-01", 1, true, 1));
        r.UnplannedOf(1).Code.Should().Be(UnplannedReasonCode.ToolingIncompatible);
        r.UnplannedOf(1).Message.Should().Contain("Nessuna macchina compatibile con ST-01");
        r.UnplannedOf(2).Code.Should().Be(UnplannedReasonCode.PredecessorUnplanned);
    }

    [Fact]
    public void Assigned_incompatible_machine_is_not_kept()
    {
        var r = PlanTool([Tooled(1, 60, [1, 2], St, assigned: 1)], [Machine(1), Machine(2)], Res(1, "ST-01", 1, true, 2));
        r.Of(1).EquipmentId.Should().Be(2);
    }

    // ---- quantity -----------------------------------------------------------------------------

    [Fact]
    public void Single_tooling_is_never_used_on_two_machines_at_once_and_the_wait_is_explained()
    {
        var r = PlanTool([Tooled(1, 60, [1], St, full: 0), Tooled(2, 60, [2], St, full: 0)], [Machine(1), Machine(2)], Res(1, "ST-01"));
        r.Of(1).Start.Should().Be(N(2, 8));
        r.Of(2).Start.Should().Be(N(2, 9));
        var w = r.Of(2).Reasons.Single(x => x.Code == PlanningReasonCode.WaitingForTooling);
        w.RelatedWorkOrderId.Should().Be(1);
        w.RelatedEquipmentId.Should().Be(1);
        w.Until.Should().Be(N(2, 9));
        w.Message.Should().Contain("ST-01").And.Contain("M1");
    }

    [Fact]
    public void Quantity_two_allows_two_machines_at_once()
    {
        var r = PlanTool([Tooled(1, 60, [1], St, full: 0), Tooled(2, 60, [2], St, full: 0)], [Machine(1), Machine(2)], Res(1, "ST-01", 2));
        r.Of(2).Start.Should().Be(N(2, 8));
    }

    [Fact]
    public void Running_job_holds_its_tooling()
    {
        var running = Running(1, 60, 1, N(2, 8)) with { Tooling = St };
        var r = PlanTool([running, Tooled(2, 60, [2], St, full: 0)], [Machine(1), Machine(2)], Res(1, "ST-01"));
        r.Of(2).Start.Should().Be(N(2, 9));
    }

    [Fact]
    public void Locked_job_holds_its_tooling_and_the_search_moves_past_it()
    {
        var locked = LockedJob(1, 1, N(2, 10), N(2, 12)) with { Tooling = St };
        var r = PlanTool([locked, Tooled(2, 180, [2], St, full: 0)], [Machine(1), Machine(2)], Res(1, "ST-01"));
        r.Of(2).Start.Should().Be(N(2, 13)); // 8:00-11:00 overlaps the locked 10:00-12:00
        r.Of(2).Reasons.Should().Contain(x => x.Code == PlanningReasonCode.WaitingForTooling && x.RelatedWorkOrderId == 1);
    }

    [Fact]
    public void Tooling_interval_includes_the_setup_part()
    {
        // B on M2: full setup 30 + run 30 = 60 min; A uses the tooling 8:00-9:00 on M1.
        var r = PlanTool([Tooled(1, 30, [1], St, full: 30), Tooled(2, 30, [2], St, full: 30)], [Machine(1), Machine(2)], Res(1, "ST-01"));
        r.Of(1).End.Should().Be(N(2, 9));
        r.Of(2).Start.Should().Be(N(2, 9));
    }

    [Fact]
    public void Unknown_tooling_resource_is_unconstrained()
    {
        var r = PlanTool([Tooled(1, 60, [1], St, full: 0), Tooled(2, 60, [2], St, full: 0)], [Machine(1), Machine(2)]);
        r.Of(2).Start.Should().Be(N(2, 8));
    }

    // ---- sequence-dependent setup -------------------------------------------------------------

    [Fact]
    public void Setup_follows_the_job_before_on_the_machine()
    {
        var r = PlanTool(
            [Tooled(1, 60, [1], St, [Val("Rosso")]), Tooled(2, 60, [1], St, [Val("Blu")], after: [1]), Tooled(3, 60, [1], St, [Val("Blu")], after: [2])],
            [Machine(1)], Res(1, "ST-01"));
        r.Of(1).SetupMinutes.Should().Be(15); r.Of(1).IsFullSetup.Should().BeTrue(); // unknown state: full setup
        r.Of(1).End.Should().Be(N(2, 9, 15));
        r.Of(2).SetupMinutes.Should().Be(5); r.Of(2).IsFullSetup.Should().BeFalse();
        r.Of(2).SetupReasonList.Should().ContainSingle().Which.Should().Contain("COL");
        r.Of(2).End.Should().Be(N(2, 10, 20));
        r.Of(3).SetupMinutes.Should().Be(0);
        r.Of(3).End.Should().Be(N(2, 11, 20));
    }

    [Fact]
    public void First_job_uses_the_initial_state_of_the_machine()
    {
        var m = Machine(1).WithState(St, [Val("Rosso")]);
        var r = PlanTool([Tooled(1, 60, [1], St, [Val("Rosso")])], [m], Res(1, "ST-01"));
        r.Of(1).SetupMinutes.Should().Be(0);
        r.Of(1).End.Should().Be(N(2, 9));
    }

    [Fact]
    public void Running_job_is_the_starting_state_of_the_free_jobs_after_it()
    {
        var running = Running(1, 60, 1, N(2, 8)) with { Tooling = St, SetupValues = [Val("Rosso")] };
        var r = PlanTool([running, Tooled(2, 60, [1], St, [Val("Rosso")], after: [1])], [Machine(1)], Res(1, "ST-01"));
        r.Of(2).SetupMinutes.Should().Be(0);
        r.Of(2).Start.Should().Be(N(2, 9));
    }

    [Fact]
    public void Locked_job_is_the_starting_state_of_the_free_jobs_after_it()
    {
        var locked = LockedJob(1, 1, N(2, 8), N(2, 9)) with { Tooling = St, SetupValues = [Val("Rosso")] };
        var r = PlanTool([locked, Tooled(2, 60, [1], St, [Val("Blu")], after: [1])], [Machine(1)], Res(1, "ST-01"));
        r.Of(2).SetupMinutes.Should().Be(5);
        r.Of(2).Start.Should().Be(N(2, 9));
    }

    [Fact]
    public void Jobs_without_tooling_keep_their_standard_minutes()
    {
        var r = Plan([Job(1, 90, [1])], [Machine(1)]);
        r.Of(1).End.Should().Be(N(2, 9, 30));
        r.Of(1).SetupMinutes.Should().Be(0);
    }

    [Fact]
    public void Insertion_before_a_fixed_job_is_skipped_when_its_setup_would_grow()
    {
        // Machine left in Rosso; locked job (Rosso) at 10:00 has setup 0. A Blu job in the 8:00-10:00 gap would make it 5: skipped.
        var m = Machine(1).WithState(St, [Val("Rosso")]);
        var locked = LockedJob(1, 1, N(2, 10), N(2, 11)) with { Tooling = St, SetupValues = [Val("Rosso")] };
        var r = PlanTool([locked, Tooled(2, 60, [1], St, [Val("Blu")])], [m], Res(1, "ST-01"));
        r.Of(2).Start.Should().Be(N(2, 11));
        r.Of(2).SetupMinutes.Should().Be(5);
    }

    [Fact]
    public void Insertion_before_a_fixed_job_is_allowed_when_its_setup_does_not_grow()
    {
        var m = Machine(1).WithState(St, [Val("Rosso")]);
        var locked = LockedJob(1, 1, N(2, 10), N(2, 11)) with { Tooling = St, SetupValues = [Val("Blu")] };
        var r = PlanTool([locked, Tooled(2, 60, [1], St, [Val("Blu")])], [m], Res(1, "ST-01"));
        r.Of(2).Start.Should().Be(N(2, 8));
    }

    [Fact]
    public void Insertion_that_lowers_the_following_setup_shortens_the_following_job()
    {
        // S (Blu) is requested at 10:00, planned first: after Rosso setup 5, 10:00-11:05. Then I (Blu) fits before it: S gets setup 0.
        var m = Machine(1).WithState(St, [Val("Rosso")]);
        var s = Tooled(1, 60, [1], St, [Val("Blu")], priority: 5, reqStart: N(2, 10));
        var i = Tooled(2, 60, [1], St, [Val("Blu")]);
        var r = PlanTool([s, i], [m], Res(1, "ST-01"));
        r.Of(2).Start.Should().Be(N(2, 8)); r.Of(2).SetupMinutes.Should().Be(5);
        r.Of(1).Start.Should().Be(N(2, 10));
        r.Of(1).SetupMinutes.Should().Be(0);
        r.Of(1).End.Should().Be(N(2, 11));
    }

    // ---- machine choice -----------------------------------------------------------------------

    [Fact]
    public void Same_end_prefers_the_machine_with_the_smaller_setup()
    {
        // M1 free at 8:00, full setup 15 -> ends 9:15. M2 busy until 8:10, setup 5 -> ends 9:15. Default machine is M1.
        var m1 = Machine(1);
        var m2 = Machine(2, busy: new PlanningInterval(N(2, 8), N(2, 8, 10))).WithState(St, [Val("Rosso")]);
        var r = PlanTool([Tooled(1, 60, [1, 2], St, [Val("Blu")], defaultMachine: 1)], [m1, m2], Res(1, "ST-01"));
        r.Of(1).EquipmentId.Should().Be(2);
        r.Of(1).End.Should().Be(N(2, 9, 15));
    }

    // ---- deterministic and invariants ---------------------------------------------------------

    [Fact]
    public void Planning_is_deterministic()
    {
        var s = PlanningBench.SharedMould();
        var a = new ForwardFiniteCapacityEngine().Plan(s.Input);
        var b = new ForwardFiniteCapacityEngine().Plan(s.Input);
        a.Planned.Select(p => (p.WorkOrderId, p.EquipmentId, p.Start, p.End, p.SetupMinutes))
            .Should().Equal(b.Planned.Select(p => (p.WorkOrderId, p.EquipmentId, p.Start, p.End, p.SetupMinutes)));
    }

    public static IEnumerable<object[]> Seeds => Enumerable.Range(1, 25).Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Random_plans_respect_setup_formula_and_tooling_quantity(int seed)
        => AssertInvariants(RandomScenario(seed));

    [Fact]
    public void Bench_scenarios_respect_setup_formula_and_tooling_quantity()
    {
        foreach (var s in PlanningBench.All()) AssertInvariants(s.Input);
    }

    private static PlanningInput RandomScenario(int seed)
    {
        var rnd = new Random(seed);
        var tools = new[] { Tl(1, "ST-01", 10), Tl(2, "ST-02", 20), Tl(3, "ST-03", 7) };
        var colours = new[] { "Rosso", "Blu", "Verde" };
        var machines = Enumerable.Range(1, 3).Select(i => Machine(i)).ToList();
        if (seed % 3 == 0) machines[1] = machines[1].WithState([tools[0]], [Val("Blu")]);
        var resources = new List<PlanningTooling>
        {
            Res(1, "ST-01", 1), Res(2, "ST-02", 2, true, 1, 3), Res(3, "ST-03", 1, true, 2)
        };
        var jobs = new List<PlanningJob>();
        var n = 8 + rnd.Next(8);
        for (var id = 1; id <= n; id++)
        {
            var useTool = rnd.Next(4) != 0;
            var tl = useTool ? new[] { tools[rnd.Next(3)] } : [];
            var vals = useTool && rnd.Next(3) != 0 ? new[] { Val(colours[rnd.Next(3)], rnd.Next(2) == 0 ? 5 : 8) } : [];
            var cands = new[] { 1, 2, 3 }.Where(_ => rnd.Next(3) != 0).DefaultIfEmpty(1).ToArray();
            var after = id > 1 && rnd.Next(4) == 0 ? new[] { rnd.Next(1, id) } : null;
            jobs.Add(Tooled(id, 30 + rnd.Next(120), cands, tl, vals, full: rnd.Next(2) == 0 ? 15 : 0, after: after,
                priority: rnd.Next(3), reqStart: rnd.Next(5) == 0 ? N(2, 8).AddHours(rnd.Next(1, 30)) : null,
                reqEnd: N(3, 12).AddHours(rnd.Next(10))));
        }
        if (seed % 2 == 0) jobs.Add(Running(100, 45, 1, N(2, 8)) with { Tooling = [tools[0]], SetupValues = [Val("Rosso")] });
        if (seed % 5 == 0) jobs.Add(LockedJob(101, 3, N(2, 13), N(2, 14)) with { Tooling = [tools[1]], SetupValues = [Val("Verde")] });
        return ToolInput(jobs, machines, resources);
    }

    private static void AssertInvariants(PlanningInput input)
    {
        var result = new ForwardFiniteCapacityEngine().Plan(input);
        var jobs = input.Jobs.ToDictionary(j => j.WorkOrderId);
        var machines = input.Equipment.ToDictionary(m => m.Id);

        foreach (var g in result.Planned.Where(p => p.EquipmentId is not null && machines[p.EquipmentId.Value].Capacity == 1)
                     .GroupBy(p => p.EquipmentId!.Value))
        {
            var ordered = g.OrderBy(p => p.Start).ThenBy(p => p.End).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var cur = ordered[i];
                if (cur.IsFixed || cur.IsLocked) continue;
                var prev = i == 0 ? machines[g.Key].InitialState : MachineSetupState.After(jobs[ordered[i - 1].WorkOrderId].Setup);
                var expected = SetupCalculator.Compute(prev, jobs[cur.WorkOrderId].Setup);
                cur.SetupMinutes.Should().Be(expected.Minutes, $"job {cur.WorkOrderId} on machine {g.Key}");
                cur.SetupReasonList.Should().Equal(expected.Reasons);
                cur.IsFullSetup.Should().Be(expected.IsFull);
            }
        }

        foreach (var res in input.ToolingResources ?? [])
        {
            var events = result.Planned.Where(p => p.End > p.Start && jobs[p.WorkOrderId].Tooling?.Any(t => t.ToolingId == res.Id) == true)
                .SelectMany(p => new[] { (p.Start, 1), (p.End, -1) }).OrderBy(e => e.Item1).ThenBy(e => e.Item2).ToList();
            var depth = 0;
            foreach (var (_, d) in events)
            {
                depth += d;
                depth.Should().BeLessThanOrEqualTo(res.Quantity, $"tooling {res.Code}");
            }
        }

        foreach (var p in result.Planned.Where(p => !p.IsFixed && !p.IsLocked && p.EquipmentId is not null && p.End > p.Start))
            jobs[p.WorkOrderId].CandidateEquipmentIds.Should().Contain(p.EquipmentId!.Value);
    }
}
