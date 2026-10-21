using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.BomFeature.Models;
using MicroMES.ApiFeatures.RoutingFeature.Models;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

public class BomStepLinkApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task AddBomItem_StepOfParentRouting_Success_ReturnsConsumedAndEffectiveStep()
    {
        var seg = (await Api.CreateSegmentAsync("SEG")).Id;
        var parent = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var raw = await Api.CreateMaterialAsync("RAW", MaterialType.RawMaterial);
        var routing = await Api.PutLinearRoutingAsync(parent.Id, seg, seg);

        var added = await Api.AddBomItemAsync(parent.Id, raw.Id, 2, routing.Steps[1].Id);

        added.Status.Should().Be(HttpStatusCode.Created);
        added.Ok.ConsumedInStepId.Should().Be(routing.Steps[1].Id);
        added.Ok.ConsumedInStep!.OperationNumber.Should().Be(20);
        added.Ok.EffectiveStep.Should().Match<BomEffectiveStepModel>(e => e.OperationNumber == 20 && !e.IsAutomatic);
    }

    [Fact]
    public async Task AddBomItem_InvalidStep_Failure_422AndNothingSaved()
    {
        var seg = (await Api.CreateSegmentAsync("SEG")).Id;
        var parent = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var other = await Api.CreateMaterialAsync("OT", MaterialType.FinishedGood);
        var raw = await Api.CreateMaterialAsync("RAW", MaterialType.RawMaterial);
        var otherRouting = await Api.PutLinearRoutingAsync(other.Id, seg);

        // parent without routing, then parent with a routing but a step of another material, then a missing step
        (await Api.AddBomItemAsync(parent.Id, raw.Id, 1, otherRouting.Steps[0].Id)).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        await Api.PutLinearRoutingAsync(parent.Id, seg);
        var foreign = await Api.AddBomItemAsync(parent.Id, raw.Id, 1, otherRouting.Steps[0].Id);
        foreign.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        foreign.Raw.Should().Contain("ciclo");
        (await Api.AddBomItemAsync(parent.Id, raw.Id, 1, 999999)).Status.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await Api.GetAsync<List<BomNodeModel>>($"/api/materials/{parent.Id}/bom")).Ok.Should().BeEmpty();
    }

    [Fact]
    public async Task PutRouting_RemovingLinkedStep_Success_ResetsLinkAndReportsComponent()
    {
        var seg = (await Api.CreateSegmentAsync("SEG")).Id;
        var parent = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var kept = await Api.CreateMaterialAsync("KEPT", MaterialType.RawMaterial);
        var lost = await Api.CreateMaterialAsync("LOST", MaterialType.RawMaterial);
        var routing = await Api.PutLinearRoutingAsync(parent.Id, seg, seg, seg);
        await Api.AddBomItemAsync(parent.Id, kept.Id, 1, routing.Steps[1].Id);
        await Api.AddBomItemAsync(parent.Id, lost.Id, 1, routing.Steps[2].Id);

        // GET: components per step
        var before = (await Api.GetAsync<RoutingModel>($"/api/materials/{parent.Id}/routing")).Ok;
        before.UnassignedComponents.Should().BeEmpty();
        before.Steps[1].ConsumedComponents.Should().ContainSingle().Which.Should()
            .Match<RoutingStepComponentModel>(c => c.MaterialCode == "KEPT" && !c.IsAutomatic);
        before.Steps[2].ConsumedComponents.Should().ContainSingle(c => c.MaterialCode == "LOST");
        before.Steps[0].ConsumedComponents.Should().BeEmpty();

        // PUT keeping steps 10 and 20 (by id, step 20 renumbered to 25) and dropping step 30.
        var saved = await Api.PutRoutingAsync(parent.Id, new UpdateRoutingRequest
        {
            Steps =
            [
                new() { Key = "a", Id = routing.Steps[0].Id, ProcessSegmentId = seg, OperationNumber = 10 },
                new() { Key = "b", Id = routing.Steps[1].Id, ProcessSegmentId = seg, OperationNumber = 25 }
            ],
            Dependencies = [new() { PredecessorKey = "a", SuccessorKey = "b" }]
        });

        saved.Status.Should().Be(HttpStatusCode.OK);
        saved.Ok.UnassignedComponents.Should().ContainSingle().Which.Should().Match<RoutingUnassignedComponentModel>(u =>
            u.MaterialCode == "LOST" && u.PreviousStepId == routing.Steps[2].Id && u.PreviousOperationNumber == 30);
        saved.Ok.Steps[1].Id.Should().Be(routing.Steps[1].Id, "steps keep their id across PUTs");
        saved.Ok.Steps[1].ConsumedComponents.Should().ContainSingle(c => c.MaterialCode == "KEPT" && !c.IsAutomatic);
        saved.Ok.Steps[0].ConsumedComponents.Should().ContainSingle(c => c.MaterialCode == "LOST" && c.IsAutomatic);

        var tree = (await Api.GetAsync<List<BomNodeModel>>($"/api/materials/{parent.Id}/bom")).Ok;
        tree.Single(n => n.Code == "LOST").ConsumedInStepId.Should().BeNull();
        tree.Single(n => n.Code == "KEPT").ConsumedInStepId.Should().Be(routing.Steps[1].Id);
    }

    [Fact]
    public async Task Release_IntermediateLinkedToThirdStep_Success_MaterialsAndChildDependencyOnConsumerOnly()
    {
        var seg = (await Api.CreateSegmentAsync("SEG", setup: 0, run: 1)).Id;
        var parent = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var semi = await Api.CreateMaterialAsync("SL", MaterialType.Intermediate);
        var rawFirst = await Api.CreateMaterialAsync("R1", MaterialType.RawMaterial);
        var rawMiddle = await Api.CreateMaterialAsync("R2", MaterialType.RawMaterial);
        var routing = await Api.PutLinearRoutingAsync(parent.Id, seg, seg, seg);
        await Api.PutLinearRoutingAsync(semi.Id, seg, seg);
        await Api.AddBomItemAsync(parent.Id, rawFirst.Id, 1);
        await Api.AddBomItemAsync(parent.Id, rawMiddle.Id, 3, routing.Steps[1].Id);
        await Api.AddBomItemAsync(parent.Id, semi.Id, 2, routing.Steps[2].Id);

        var order = await Api.CreateOrderAsync(parent.Id, 2);
        var released = (await Api.ReleaseAsync(order.Id)).Ok;

        var wo = released.WorkOrders.ToDictionary(w => w.OperationNumber);
        released.Materials.ToDictionary(m => m.MaterialCode, m => m.WorkOrderId).Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["R1"] = wo[10].Id, ["R2"] = wo[20].Id, ["SL"] = wo[30].Id
        });
        released.Materials.Single(m => m.MaterialCode == "SL").ChildProductionOrderId.Should().NotBeNull();

        var overview = (await Api.GetOrderOverviewAsync(order.Id)).Ok;
        var childOrderId = released.Children.Single().Id;
        var childLast = overview.WorkOrders.Single(w => w.ProductionOrderId == childOrderId && w.OperationNumber == 20).Id;
        overview.Dependencies.Where(d => d.PredecessorWorkOrderId == childLast).Select(d => d.SuccessorWorkOrderId)
            .Should().Equal(wo[30].Id);
        overview.Dependencies.Should().HaveCount(2 + 1 + 1, "two parent edges, one child edge, one cross-order edge");

        // Step 10 does not wait for the child; step 30 waits for step 20 and the child.
        overview.WorkOrders.Single(w => w.Id == wo[10].Id).Status.Should().Be(WorkOrderStatus.Ready);
        overview.WorkOrders.Single(w => w.Id == wo[30].Id).Status.Should().Be(WorkOrderStatus.Waiting);
        overview.Summary.Counts.Ready.Should().Be(2, "parent step 10 and the first child step");

        // Overview exposes the materials of each work order.
        overview.WorkOrders.Single(w => w.Id == wo[20].Id).Materials.Should().ContainSingle()
            .Which.Should().Match<MicroMES.ApiFeatures.ProductionOrderFeature.Models.WorkOrderMaterialModel>(m => m.MaterialCode == "R2" && m.RequiredQuantity == 6);
        var semiLine = overview.WorkOrders.Single(w => w.Id == wo[30].Id).Materials.Single();
        semiLine.ChildProductionOrderId.Should().Be(childOrderId);
        semiLine.RequiredQuantity.Should().Be(4);
        overview.WorkOrders.Where(w => w.ProductionOrderId == childOrderId).Should().OnlyContain(w => w.Materials.Count == 0);
    }

    [Fact]
    public async Task Release_UnlinkedBom_Success_BehavesLikeBefore()
    {
        var seg = (await Api.CreateSegmentAsync("SEG", setup: 0, run: 1)).Id;
        var parent = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var semi = await Api.CreateMaterialAsync("SL", MaterialType.Intermediate);
        var raw = await Api.CreateMaterialAsync("R1", MaterialType.RawMaterial);
        await Api.PutLinearRoutingAsync(parent.Id, seg, seg);
        await Api.PutLinearRoutingAsync(semi.Id, seg);
        await Api.AddBomItemAsync(parent.Id, semi.Id, 1);
        await Api.AddBomItemAsync(parent.Id, raw.Id, 1);

        var released = (await Api.ReleaseAsync((await Api.CreateOrderAsync(parent.Id, 1)).Id)).Ok;

        var first = released.WorkOrders.Single(w => w.OperationNumber == 10);
        released.Materials.Should().OnlyContain(m => m.WorkOrderId == first.Id);
        first.Status.Should().Be(WorkOrderStatus.Waiting, "the child gates the first start step");
        first.Predecessors.Should().ContainSingle();
    }
}
