using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.RoutingFeature.Models;
using MicroMES.ApiFeatures.RoutingFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.RoutingFeature;

public class RoutingServiceFixture : BaseFeatureFixture
{
    private readonly int _material;
    private readonly int _segA;
    private readonly int _segB;

    public RoutingServiceFixture()
    {
        _material = AddMaterial("PF", MaterialType.FinishedGood);
        _segA = AddSegment("SEG-A", setup: 10, run: 2);
        _segB = AddSegment("SEG-B", setup: 5, run: 1);
    }

    private UpdateRoutingStepRequest Step(string key, int operation, int? segment = null, int? id = null,
        decimal? setup = null, decimal? run = null) => new()
    {
        Key = key, Id = id, OperationNumber = operation, ProcessSegmentId = segment ?? _segA,
        SetupMinutes = setup, RunMinutesPerUnit = run
    };

    private static UpdateRoutingDependencyRequest Dep(string from, string to)
        => new() { PredecessorKey = from, SuccessorKey = to };

    private static UpdateRoutingRequest Request(UpdateRoutingStepRequest[] steps, params UpdateRoutingDependencyRequest[] deps)
        => new() { Steps = [.. steps], Dependencies = [.. deps] };

    private Task<RoutingResult<RoutingModel>> Replace(UpdateRoutingRequest request, int? material = null)
        => Service<RoutingService>().ReplaceAsync(material ?? _material, request);

    private int StepCount() => Query(db => db.RoutingSteps.Count());

    // ---- valid ---------------------------------------------------------------------------------------

    [Fact]
    public async Task ReplaceAsync_EmptyRouting_Success()
    {
        var result = await Replace(Request([]));

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Steps.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_LinearRouting_Success_ReturnsEffectiveTimes()
    {
        var result = await Replace(Request(
            [Step("a", 10, _segA), Step("b", 20, _segB, setup: 99, run: 7)],
            Dep("a", "b")));

        result.IsSuccessful.Should().BeTrue();
        var steps = result.Data!.Steps;
        steps.Select(s => s.OperationNumber).Should().Equal(10, 20);
        steps[0].EffectiveSetupMinutes.Should().Be(10, "no override: the segment standard applies");
        steps[0].EffectiveRunMinutesPerUnit.Should().Be(2);
        steps[1].EffectiveSetupMinutes.Should().Be(99, "the step overrides the segment");
        steps[1].EffectiveRunMinutesPerUnit.Should().Be(7);
        result.Data.Dependencies.Should().ContainSingle();
    }

    [Fact]
    public async Task ReplaceAsync_ParallelBranchesConverging_Success()
    {
        var result = await Replace(Request(
            [Step("s", 10), Step("x", 20), Step("y", 30), Step("e", 40)],
            Dep("s", "x"), Dep("s", "y"), Dep("x", "e"), Dep("y", "e")));

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Dependencies.Should().HaveCount(4);
    }

    [Fact]
    public async Task ReplaceAsync_SecondCallWithIds_Success_UpdatesInPlaceAndSupportsSwappingOperationNumbers()
    {
        var first = await Replace(Request([Step("a", 10), Step("b", 20)], Dep("a", "b")));
        var a = first.Data!.Steps[0].Id;
        var b = first.Data.Steps[1].Id;

        var result = await Replace(Request(
            [Step("a", 20, id: a), Step("b", 10, id: b)], Dep("b", "a")));

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Steps.Select(s => s.Id).Should().Equal(b, a);
        result.Data.Dependencies.Should().ContainSingle().Which.PredecessorStepId.Should().Be(b);
    }

    [Fact]
    public async Task ReplaceAsync_RemovedStepsAreDeleted_Success()
    {
        var first = await Replace(Request([Step("a", 10), Step("b", 20)], Dep("a", "b")));
        var a = first.Data!.Steps[0].Id;

        var result = await Replace(Request([Step("a", 10, id: a)]));

        result.IsSuccessful.Should().BeTrue();
        StepCount().Should().Be(1);
        Query(db => db.RoutingStepDependencies.Count()).Should().Be(0);
    }

    [Fact]
    public async Task ReplaceAsync_ExistingStepKeepingItsInactiveSegment_Success()
    {
        var first = await Replace(Request([Step("a", 10, _segB)]));
        var id = first.Data!.Steps[0].Id;
        Query(db => { var s = db.ProcessSegments.Single(x => x.Id == _segB); s.IsActive = false; db.SaveChanges(); return 0; });

        var result = await Replace(Request([Step("a", 10, _segB, id: id, setup: 3)]));

        result.IsSuccessful.Should().BeTrue("only new or changed segments must be active");
    }

    // ---- graph rules ---------------------------------------------------------------------------------

    [Fact]
    public async Task ReplaceAsync_Cycle_Failure_InvalidAndNothingPersisted()
    {
        var result = await Replace(Request(
            [Step("a", 10), Step("b", 20), Step("c", 30)],
            Dep("a", "b"), Dep("b", "c"), Dep("c", "a")));

        result.Failure.Should().Be(RoutingFailure.Invalid);
        result.Errors.Should().Contain(e => e.Contains("cycle"));
        StepCount().Should().Be(0);
    }

    [Fact]
    public async Task ReplaceAsync_DisconnectedSteps_Failure_Invalid()
    {
        // a -> b and c -> d: two components, even though each has one final step
        var result = await Replace(Request(
            [Step("a", 10), Step("b", 20), Step("c", 30), Step("d", 40)],
            Dep("a", "b"), Dep("c", "d")));

        result.Failure.Should().Be(RoutingFailure.Invalid);
        result.Errors.Should().Contain(e => e.Contains("connected"));
    }

    [Fact]
    public async Task ReplaceAsync_TwoFinalSteps_Failure_Invalid()
    {
        var result = await Replace(Request(
            [Step("a", 10), Step("b", 20), Step("c", 30)],
            Dep("a", "b"), Dep("a", "c")));

        result.Failure.Should().Be(RoutingFailure.Invalid);
        result.Errors.Should().Contain(e => e.Contains("exactly one final step"));
    }

    [Fact]
    public async Task ReplaceAsync_TwoStepsWithoutDependencies_Failure_Invalid()
    {
        var result = await Replace(Request([Step("a", 10), Step("b", 20)]));

        result.Failure.Should().Be(RoutingFailure.Invalid);
    }

    [Fact]
    public async Task ReplaceAsync_SingleStepWithoutDependencies_Success()
    {
        (await Replace(Request([Step("a", 10)]))).IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task ReplaceAsync_SelfDependency_Failure_Invalid()
    {
        var result = await Replace(Request([Step("a", 10)], Dep("a", "a")));

        result.Failure.Should().Be(RoutingFailure.Invalid);
    }

    [Fact]
    public async Task ReplaceAsync_DuplicateDependency_Failure_Invalid()
    {
        var result = await Replace(Request([Step("a", 10), Step("b", 20)], Dep("a", "b"), Dep("a", "b")));

        result.Failure.Should().Be(RoutingFailure.Invalid);
        result.Errors.Should().Contain(e => e.Contains("Duplicate dependency"));
    }

    [Fact]
    public async Task ReplaceAsync_DependencyOnUnknownKey_Failure_Invalid()
    {
        var result = await Replace(Request([Step("a", 10)], Dep("a", "zzz")));

        result.Failure.Should().Be(RoutingFailure.Invalid);
        result.Errors.Should().Contain(e => e.Contains("zzz"));
    }

    // ---- shape rules ---------------------------------------------------------------------------------

    [Fact]
    public async Task ReplaceAsync_DuplicateOperationNumber_Failure_Invalid()
    {
        var result = await Replace(Request([Step("a", 10), Step("b", 10)], Dep("a", "b")));

        result.Failure.Should().Be(RoutingFailure.Invalid);
        result.Errors.Should().Contain(e => e.Contains("Duplicate operation number"));
        StepCount().Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ReplaceAsync_NonPositiveOperationNumber_Failure_Invalid(int operation)
    {
        var result = await Replace(Request([Step("a", operation)]));

        result.Failure.Should().Be(RoutingFailure.Invalid);
    }

    [Fact]
    public async Task ReplaceAsync_DuplicateKey_Failure_Invalid()
    {
        var result = await Replace(Request([Step("a", 10), Step("a", 20)], Dep("a", "a")));

        result.Failure.Should().Be(RoutingFailure.Invalid);
        result.Errors.Should().Contain(e => e.Contains("Duplicate step key"));
    }

    [Fact]
    public async Task ReplaceAsync_NewStepWithInactiveSegment_Failure_Invalid()
    {
        var inactive = AddSegment("OLD", active: false);

        var result = await Replace(Request([Step("a", 10, inactive)]));

        result.Failure.Should().Be(RoutingFailure.Invalid);
        result.Errors.Should().Contain(e => e.Contains("not active"));
    }

    [Fact]
    public async Task ReplaceAsync_ExistingStepSwitchedToInactiveSegment_Failure_Invalid()
    {
        var first = await Replace(Request([Step("a", 10)]));
        var inactive = AddSegment("OLD", active: false);

        var result = await Replace(Request([Step("a", 10, inactive, id: first.Data!.Steps[0].Id)]));

        result.Failure.Should().Be(RoutingFailure.Invalid);
        Query(db => db.RoutingSteps.Single().ProcessSegmentId).Should().Be(_segA, "the failed change is not applied");
    }

    [Fact]
    public async Task ReplaceAsync_UnknownSegment_Failure_NotFound()
    {
        var result = await Replace(Request([Step("a", 10, segment: 9999)]));

        result.Failure.Should().Be(RoutingFailure.NotFound);
    }

    [Fact]
    public async Task ReplaceAsync_UnknownStepId_Failure_NotFound()
    {
        var result = await Replace(Request([Step("a", 10, id: 12345)]));

        result.Failure.Should().Be(RoutingFailure.NotFound);
    }

    // ---- material rules ------------------------------------------------------------------------------

    [Theory]
    [InlineData(MaterialType.RawMaterial)]
    [InlineData(MaterialType.Consumable)]
    public async Task ReplaceAsync_WrongMaterialType_Failure_Invalid(MaterialType type)
    {
        var material = AddMaterial("WRONG", type);

        var result = await Replace(Request([Step("a", 10)]), material);

        result.Failure.Should().Be(RoutingFailure.Invalid);
        StepCount().Should().Be(0);
    }

    [Fact]
    public async Task ReplaceAsync_UnknownMaterial_Failure_NotFound()
    {
        var result = await Replace(Request([]), material: 9999);

        result.Failure.Should().Be(RoutingFailure.NotFound);
    }

    // ---- invalid replacement leaves data unchanged ---------------------------------------------------

    [Fact]
    public async Task ReplaceAsync_InvalidSecondRequest_Failure_LeavesExistingRoutingUntouched()
    {
        await Replace(Request([Step("a", 10), Step("b", 20)], Dep("a", "b")));

        var result = await Replace(Request([Step("a", 10), Step("b", 20)], Dep("a", "b"), Dep("b", "a")));

        result.IsSuccessful.Should().BeFalse();
        var after = await Service<RoutingService>().GetAsync(_material);
        after.Data!.Steps.Should().HaveCount(2);
        after.Data.Dependencies.Should().ContainSingle();
    }
}
