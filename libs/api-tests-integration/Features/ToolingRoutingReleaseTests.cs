using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.RoutingFeature.Models;
using MicroMES.ApiFeatures.ToolingFeature.Models;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

public class ToolingRoutingReleaseTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private async Task<ToolingDetailModel> ToolAsync(string code, int setup = 10)
        => (await Api.PostAsync<ToolingDetailModel>("/api/tooling", new CreateToolingRequest { Code = code, Name = "Stampo " + code, SetupMinutes = setup })).Ok;

    private async Task<SetupAttributeModel> AttrAsync(string code, int minutes = 5)
        => (await Api.PostAsync<SetupAttributeModel>("/api/setup-attributes", new CreateSetupAttributeRequest { Code = code, Name = "Attr " + code, ChangeMinutes = minutes })).Ok;

    private static UpdateRoutingRequest Routing(int segment, List<int>? tooling, List<UpdateRoutingSetupValueRequest>? values, int? id = null)
        => new()
        {
            Steps = [new UpdateRoutingStepRequest { Key = "a", Id = id, OperationNumber = 10, ProcessSegmentId = segment, ToolingIds = tooling, SetupValues = values }],
            Dependencies = []
        };

    [Fact]
    public async Task Routing_ToolingAndValues_Success_NormalizedAndRemovable()
    {
        var seg = await Api.CreateSegmentAsync("S1");
        var mat = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var t = await ToolAsync("ST-1");
        var a = await AttrAsync("COL");

        var saved = await Api.PutRoutingAsync(mat.Id, Routing(seg.Id, [t.Id, t.Id], [new() { AttributeId = a.Id, Value = "  Rosso   scuro " }]));

        var step = saved.Ok.Steps.Single();
        step.Tooling.Should().ContainSingle().Which.Code.Should().Be("ST-1");
        step.SetupValues.Should().ContainSingle().Which.Value.Should().Be("Rosso scuro");

        var detail = (await Api.GetAsync<ToolingDetailModel>($"/api/tooling/{t.Id}")).Ok;
        detail.RoutingSteps.Should().ContainSingle().Which.Code.Should().Be("PF / op 10");

        var cleared = await Api.PutRoutingAsync(mat.Id, Routing(seg.Id, [], [new() { AttributeId = a.Id, Value = " " }], step.Id));
        cleared.Ok.Steps.Single().Tooling.Should().BeEmpty();
        cleared.Ok.Steps.Single().SetupValues.Should().BeEmpty();
    }

    [Fact]
    public async Task Routing_InactiveOrUnknown_Failure_422And404()
    {
        var seg = await Api.CreateSegmentAsync("S1");
        var mat = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var t = await ToolAsync("ST-1");
        await Api.DeleteAsync($"/api/tooling/{t.Id}");

        var inactive = await Api.PutRoutingAsync(mat.Id, Routing(seg.Id, [t.Id], null));
        inactive.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        inactive.Raw.Should().Contain("ST-1");
        (await Api.PutRoutingAsync(mat.Id, Routing(seg.Id, [999], null))).Status.Should().Be(HttpStatusCode.NotFound);
        (await Api.PutRoutingAsync(mat.Id, Routing(seg.Id, null, [new() { AttributeId = 999, Value = "x" }]))).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Release_CopiesToolingAndActiveValues_AlsoForChildOrders()
    {
        var seg = await Api.CreateSegmentAsync("S1");
        var parent = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var child = await Api.CreateMaterialAsync("SL", MaterialType.Intermediate);
        var t = await ToolAsync("ST-1", 12);
        var col = await AttrAsync("COL");
        var old = await AttrAsync("OLD");
        await Api.PutRoutingAsync(parent.Id, Routing(seg.Id, [], null));
        await Api.PutRoutingAsync(child.Id, Routing(seg.Id, [t.Id], [new() { AttributeId = col.Id, Value = "blu" }, new() { AttributeId = old.Id, Value = "x" }]));
        await Api.AddBomItemAsync(parent.Id, child.Id, 1);
        await Api.PutAsync<SetupAttributeModel>($"/api/setup-attributes/{old.Id}", new UpdateSetupAttributeRequest { Code = "OLD", Name = "Attr OLD", ChangeMinutes = 5, IsActive = false });

        var order = await Api.CreateOrderAsync(parent.Id, 1);
        (await Api.ReleaseAsync(order.Id)).Ok.Status.Should().Be(ProductionOrderStatus.Released);

        var childWo = (await Api.GetWorkOrdersAsync()).Ok.Single(w => w.ProductionOrderCode != order.Code);
        var detail = (await Api.GetWorkOrderAsync(childWo.Id)).Ok;
        detail.Tooling.Should().ContainSingle().Which.Code.Should().Be("ST-1");
        detail.SetupValues.Should().ContainSingle().Which.Should().Match<WorkOrderSetupValueModel>(v => v.Code == "COL" && v.Value == "blu");

        (await Api.GetAsync<ToolingDetailModel>($"/api/tooling/{t.Id}")).Ok.WorkOrders.Should().ContainSingle();

        // Later changes to the routing do not touch the released work order.
        await Api.PutRoutingAsync(child.Id, Routing(seg.Id, [], [], (await Api.GetAsync<RoutingModel>($"/api/materials/{child.Id}/routing")).Ok.Steps.Single().Id));
        (await Api.GetWorkOrderAsync(childWo.Id)).Ok.Tooling.Should().HaveCount(1);
    }

    [Fact]
    public async Task Release_InactiveTooling_Failure_422NamingIt()
    {
        var seg = await Api.CreateSegmentAsync("S1");
        var mat = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var t = await ToolAsync("ST-9");
        await Api.PutRoutingAsync(mat.Id, Routing(seg.Id, [t.Id], null));
        await Api.DeleteAsync($"/api/tooling/{t.Id}");

        var order = await Api.CreateOrderAsync(mat.Id, 1);
        var released = await Api.ReleaseAsync(order.Id);

        released.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        released.Raw.Should().Contain("ST-9").And.Contain("disattivata");
        (await Api.GetOrderAsync(order.Id)).Ok.Status.Should().Be(ProductionOrderStatus.Planned);
    }
}
