using FluentAssertions;
using MicroMES.ApiFeatures.PlanningFeature.Engine;

namespace MicroMES.ApiTests.Features.PlanningFeature;

public class SetupCalculatorTests
{
    private static JobTooling Die(int id = 1, int minutes = 10) => new(id, $"ST-0{id}", minutes);
    private static JobSetupValue Color(string value, int minutes = 5, int attr = 1) => new(attr, "COLORE", "Colore", minutes, value);

    private static SetupRequirements Job(IEnumerable<JobTooling>? tooling = null, IEnumerable<JobSetupValue>? values = null, decimal full = 100m)
        => new((tooling ?? []).ToList(), (values ?? []).ToList(), full);

    private static MachineSetupState State(int[]? tooling = null, params (int Attr, string Value)[] values)
        => new((tooling ?? []).ToHashSet(), values.ToDictionary(v => v.Attr, v => v.Value));

    [Fact]
    public void BothDifferent_SumsToolingAndColor()
    {
        var r = SetupCalculator.Compute(State([2], (1, "rosso")), Job([Die()], [Color("blu")]));
        r.Minutes.Should().Be(15);
        r.IsFull.Should().BeFalse();
        r.Reasons.Should().Equal("cambio attrezzatura ST-01: 10 min", "cambio COLORE (rosso → blu): 5 min");
    }

    [Fact]
    public void SameToolingDifferentColor_OnlyColor()
        => SetupCalculator.Compute(State([1], (1, "rosso")), Job([Die()], [Color("blu")])).Minutes.Should().Be(5);

    [Fact]
    public void AllSame_IsZero()
    {
        var r = SetupCalculator.Compute(State([1], (1, "rosso")), Job([Die()], [Color("rosso")]));
        r.Minutes.Should().Be(0);
        r.Reasons.Should().BeEmpty();
        r.IsFull.Should().BeFalse();
    }

    [Fact]
    public void CappedByTheFullSetupOfTheStep()
    {
        var r = SetupCalculator.Compute(State([2], (1, "rosso")), Job([Die()], [Color("blu")], full: 12));
        r.Minutes.Should().Be(12);
        r.IsFull.Should().BeTrue();
        r.Reasons.Should().Contain("limitato all'attrezzaggio completo: 12 min");
    }

    [Fact]
    public void FullSetupZero_MeansNoCap()
        => SetupCalculator.Compute(State([2]), Job([Die()], [Color("blu")], full: 0)).Minutes.Should().Be(15);

    [Fact]
    public void AttributeAbsentInPrevious_IsDifferent()
        => SetupCalculator.Compute(State([1]), Job([Die()], [Color("blu")])).Minutes.Should().Be(5);

    [Fact]
    public void ToolingAlreadyMounted_CostsNothing_AmongSeveral()
        => SetupCalculator.Compute(State([1, 3]), Job([Die(1, 10), Die(2, 7), Die(3, 4)])).Minutes.Should().Be(7);

    [Fact]
    public void JobWithoutAttributes_OnlyTooling()
        => SetupCalculator.Compute(State([2], (1, "rosso")), Job([Die()])).Minutes.Should().Be(10);

    [Fact]
    public void StateNull_UsesFullSetup()
    {
        var r = SetupCalculator.Compute(null, Job([Die()], [Color("blu")], full: 12));
        r.Minutes.Should().Be(12);
        r.IsFull.Should().BeTrue();
        r.Reasons.Should().Equal("attrezzaggio completo: 12 min");
    }

    [Fact]
    public void JobWithoutToolingAndValues_UsesFullSetupEvenWithKnownState()
        => SetupCalculator.Compute(State([1]), Job(full: 30)).Should().Match<SetupResult>(r => r.Minutes == 30 && r.IsFull);

    [Fact]
    public void Comparison_IsCaseInsensitiveAndTrimmed()
        => SetupCalculator.Compute(State([], (1, " Rosso ")), Job(values: [Color("rosso  ")])).Minutes.Should().Be(0);

    [Fact]
    public void IsDeterministic_AndNextStateIsTheJobs()
    {
        var job = Job([Die(2), Die(1)], [Color("blu")]);
        var prev = State([9]);
        SetupCalculator.Compute(prev, job).Should().BeEquivalentTo(SetupCalculator.Compute(prev, job));
        var next = MachineSetupState.After(job);
        next.ToolingIds.Should().BeEquivalentTo([1, 2]);
        next.Values.Should().Equal(new Dictionary<int, string> { [1] = "blu" });
        SetupCalculator.Compute(next, job).Minutes.Should().Be(0);
    }

    [Fact]
    public void WithSetup_ReplacesTheFullSetupInTheStandardMinutes()
    {
        var setup = new SetupResult(5, [], false);
        SetupCalculator.WithSetup(100, 12, setup).Should().Be(93);
        SetupCalculator.WithSetup(3, 12, setup).Should().Be(0);
    }
}
