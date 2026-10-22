using FluentAssertions;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using Xunit.Abstractions;

namespace MicroMES.ApiTests.Features.PlanningFeature;

/// <summary>Regression of the metrics of the current heuristic: a future engine is compared against these baselines.</summary>
public class PlanningBenchTests(ITestOutputHelper output)
{
    private static PlanMetrics Run(PlanningScenario s) => PlanningBench.Measure(s, new ForwardFiniteCapacityEngine());

    // Baselines of ForwardFiniteCapacityEngine (11/2026, "now" = Monday 2 Nov 08:00 Rome). A better engine must not be worse
    // on lateness and makespan; update these numbers only when the heuristic changes on purpose.
    public static TheoryData<string, decimal, string, decimal> Baselines => new()
    {
        { "Officina con code", 350m, "2026-11-03T15:30:00.0000000Z", 0.358m },
        { "Due ordini concorrenti con scadenze diverse", 960m, "2026-11-03T10:00:00.0000000Z", 0.636m },
        { "Colli di bottiglia con alternative", 652m, "2026-11-03T10:52:00.0000000Z", 0.542m },
        { "Calendari diversi", 1020m, "2026-11-04T09:00:00.0000000Z", 0.550m },
        { "Lavori fissati", 0m, "2026-11-03T10:30:00.0000000Z", 0.464m },
        { "Stampo condiviso su due macchine", 0m, "2026-11-03T07:10:00.0000000Z", 0.5m },
        { "Cambio ridotto", 0m, "2026-11-02T10:00:00.0000000Z", 1m },
        { "Compatibilità parziale", 0m, "2026-11-02T12:50:00.0000000Z", 0.983m }
    };

    [Theory]
    [MemberData(nameof(Baselines))]
    public void Heuristic_metrics_match_the_baseline(string name, decimal lateness, string makespanUtc, decimal utilization)
    {
        var m = Run(PlanningBench.All().Single(s => s.Name == name));
        output.WriteLine($"{name}: late={m.TotalLatenessMinutes} makespan={m.Makespan:O} util={m.AverageUtilization}");
        m.TotalLatenessMinutes.Should().Be(lateness);
        m.Makespan.Should().Be(DateTime.Parse(makespanUtc, null, System.Globalization.DateTimeStyles.RoundtripKind));
        m.AverageUtilization.Should().Be(utilization);
    }

    [Fact]
    public void Bench_covers_at_least_four_scenarios() => PlanningBench.All().Count.Should().BeGreaterThanOrEqualTo(5);

    [Fact]
    public void Scenarios_are_planned_without_unplanned_jobs()
    {
        foreach (var s in PlanningBench.All()) Run(s).Unplanned.Should().Be(0, s.Name);
    }

    [Fact]
    public void Metrics_are_deterministic()
    {
        foreach (var s in PlanningBench.All()) Run(s).Should().Be(Run(s));
    }
}
