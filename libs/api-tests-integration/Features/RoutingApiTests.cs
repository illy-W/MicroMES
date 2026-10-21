using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.RoutingFeature.Models;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

public class RoutingApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static UpdateRoutingStepRequest Step(string key, int operation, int segment, int? id = null) => new()
    {
        Key = key, Id = id, OperationNumber = operation, ProcessSegmentId = segment
    };

    private static UpdateRoutingDependencyRequest Dep(string from, string to) => new() { PredecessorKey = from, SuccessorKey = to };

    [Fact]
    public async Task Routing_ParallelBranchesThenUpdate_Success_PersistsGraphAndReplacesIt()
    {
        var segment = await Api.CreateSegmentAsync("SEG", setup: 10, run: 2);
        var material = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);

        var saved = await Api.PutRoutingAsync(material.Id, new UpdateRoutingRequest
        {
            Steps = [Step("a", 10, segment.Id), Step("b", 20, segment.Id), Step("c", 30, segment.Id), Step("d", 40, segment.Id)],
            Dependencies = [Dep("a", "b"), Dep("a", "c"), Dep("b", "d"), Dep("c", "d")]
        });

        saved.Status.Should().Be(HttpStatusCode.OK);
        saved.Ok.Steps.Select(s => s.OperationNumber).Should().Equal(10, 20, 30, 40);
        saved.Ok.Steps.Should().OnlyContain(s => s.EffectiveSetupMinutes == 10 && s.EffectiveRunMinutesPerUnit == 2);
        saved.Ok.Dependencies.Should().HaveCount(4);

        // Second PUT keeps step "a" (by id) and drops the others: dependencies go away first, no FK violation.
        var aId = saved.Ok.Steps[0].Id;
        var trimmed = await Api.PutRoutingAsync(material.Id, new UpdateRoutingRequest
        {
            Steps = [Step("a", 10, segment.Id, aId)], Dependencies = []
        });
        trimmed.Ok.Steps.Should().ContainSingle().Which.Id.Should().Be(aId);
        trimmed.Ok.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public async Task Routing_Cycle_Failure_422AndStoredRoutingUnchanged()
    {
        var segment = await Api.CreateSegmentAsync("SEG");
        var material = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var original = await Api.PutLinearRoutingAsync(material.Id, segment.Id, segment.Id);

        var cyclic = await Api.PutRoutingAsync(material.Id, new UpdateRoutingRequest
        {
            Steps = [Step("a", 10, segment.Id), Step("b", 20, segment.Id), Step("c", 30, segment.Id)],
            Dependencies = [Dep("a", "b"), Dep("b", "c"), Dep("c", "a")]
        });

        cyclic.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        cyclic.Raw.Should().Contain("cycle");
        var stored = (await Api.GetAsync<RoutingModel>($"/api/materials/{material.Id}/routing")).Ok;
        stored.Steps.Select(s => s.Id).Should().Equal(original.Steps.Select(s => s.Id));
        stored.Dependencies.Select(d => d.Id).Should().Equal(original.Dependencies.Select(d => d.Id));
    }
}
