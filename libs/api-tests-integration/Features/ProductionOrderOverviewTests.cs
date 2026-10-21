using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

public class ProductionOrderOverviewTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    /// <summary>Parent routing has 1 step, child routing 2 steps: the family has 3 work orders.</summary>
    private async Task<int> ProductWithChildAsync()
    {
        var seg = (await Api.CreateSegmentAsync("SEG", setup: 1, run: 1)).Id;
        var parent = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var child = await Api.CreateMaterialAsync("SL", MaterialType.Intermediate);
        await Api.PutLinearRoutingAsync(parent.Id, seg);
        await Api.PutLinearRoutingAsync(child.Id, seg, seg);
        await Api.AddBomItemAsync(parent.Id, child.Id, 1);
        return parent.Id;
    }

    [Fact]
    public async Task GetOverview_ReleasedOrderWithChild_Success_AllWorkOrdersAndCrossOrderDependencies()
    {
        var order = await Api.CreateOrderAsync(await ProductWithChildAsync(), 1);
        await Api.ReleaseAsync(order.Id);

        var o = (await Api.GetOrderOverviewAsync(order.Id)).Ok;

        o.Orders.Should().HaveCount(2);
        o.WorkOrders.Should().HaveCount(3);
        o.Summary.TotalWorkOrders.Should().Be(3);
        o.Summary.Counts.Ready.Should().Be(1);
        o.Summary.Counts.Waiting.Should().Be(2);
        o.Dependencies.Should().HaveCount(2);
        var parentWo = o.WorkOrders.Single(w => w.ProductionOrderId == order.Id).Id;
        o.Dependencies.Should().Contain(d => d.SuccessorWorkOrderId == parentWo, "child last step gates the parent start step");
        o.Summary.CriticalPathWorkOrderIds.Should().HaveCount(3).And.EndWith(parentWo);
        o.Summary.MinimumDurationMinutes.Should().Be(6);
    }

    [Fact]
    public async Task GetOverview_PlannedOrder_Success_EmptyLists()
    {
        var order = await Api.CreateOrderAsync(await ProductWithChildAsync(), 1);

        var o = (await Api.GetOrderOverviewAsync(order.Id)).Ok;

        o.Orders.Should().ContainSingle().Which.Id.Should().Be(order.Id);
        o.WorkOrders.Should().BeEmpty();
        o.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOverview_UnknownOrder_Failure_404()
        => (await Api.GetOrderOverviewAsync(999999)).Status.Should().Be(HttpStatusCode.NotFound);

    [Fact]
    public async Task GetOverview_ChildOrderId_Success_OnlyItsSubtree()
    {
        var order = await Api.CreateOrderAsync(await ProductWithChildAsync(), 1);
        await Api.ReleaseAsync(order.Id);
        var child = (await Api.GetOrdersAsync()).Ok.Single(x => x.ParentOrderId == order.Id);

        var o = (await Api.GetOrderOverviewAsync(child.Id)).Ok;

        o.Orders.Should().ContainSingle().Which.Id.Should().Be(child.Id);
        o.WorkOrders.Should().HaveCount(2).And.OnlyContain(w => w.ProductionOrderId == child.Id);
        o.Dependencies.Should().ContainSingle();
    }
}
