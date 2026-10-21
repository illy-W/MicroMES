using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

public class ProductionOrderLifecycleTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    /// <summary>Finished good with a two-step linear routing (10: 10+2/unit, 20: 5+1/unit) and one raw material (2 per piece).</summary>
    private async Task<(int Product, int Raw)> SimpleProductAsync()
    {
        var cutting = await Api.CreateSegmentAsync("CUT", setup: 10, run: 2);
        var packing = await Api.CreateSegmentAsync("PACK", setup: 5, run: 1);
        var product = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var raw = await Api.CreateMaterialAsync("RAW", MaterialType.RawMaterial);
        await Api.PutLinearRoutingAsync(product.Id, cutting.Id, packing.Id);
        await Api.AddBomItemAsync(product.Id, raw.Id, 2);
        return (product.Id, raw.Id);
    }

    private async Task<int> WorkOrderIdAsync(int orderId, int operation)
        => (await Api.GetWorkOrdersAsync(orderId)).Ok.Single(w => w.OperationNumber == operation).Id;

    [Fact]
    public async Task ProductionOrder_CreateReleaseAndRunEveryWorkOrder_Success_OrderCompletes()
    {
        var (product, raw) = await SimpleProductAsync();

        // Create: Planned, no work orders yet, code OP-<year>-NNNN.
        var planned = await Api.CreateOrderAsync(product, 3);
        planned.Status.Should().Be(ProductionOrderStatus.Planned);
        planned.Code.Should().MatchRegex(@"^OP-\d{4}-0001$");
        planned.WorkOrders.Should().BeEmpty();
        planned.AllowedActions.Should().BeEquivalentTo("Release", "Cancel");

        // Release: plan persisted, first step Ready, the other Waiting; times and materials computed.
        var released = (await Api.ReleaseAsync(planned.Id)).Ok;
        released.Status.Should().Be(ProductionOrderStatus.Released);
        released.WorkOrders.Select(w => (w.OperationNumber, w.Status)).Should().Equal(
            (10, WorkOrderStatus.Ready), (20, WorkOrderStatus.Waiting));
        released.WorkOrders.Select(w => w.PlannedMinutes).Should().Equal(16m, 8m);
        released.Summary.WorkContentMinutes.Should().Be(24);
        released.Summary.MinimumDurationMinutes.Should().Be(24);
        var line = released.Materials.Should().ContainSingle().Subject;
        (line.MaterialId, line.RequiredQuantity).Should().Be((raw, 6m));
        line.WorkOrderCode.Should().EndWith("/10", "materials go to the first start step");
        released.Dependencies.Should().ContainSingle();

        // Work order 10: start -> pause -> resume -> stop, two sessions.
        var first = await WorkOrderIdAsync(planned.Id, 10);
        var started = (await Api.StartAsync(first)).Ok;
        started.Status.Should().Be(WorkOrderStatus.Running);
        started.AllowedActions.Should().BeEquivalentTo("Pause", "Stop", "Abort");
        (await Api.GetOrderAsync(planned.Id)).Ok.Status.Should().Be(ProductionOrderStatus.InProgress);

        (await Api.PauseAsync(first, good: 1, notes: "cambio turno")).Ok.Status.Should().Be(WorkOrderStatus.Interrupted);
        (await Api.ResumeAsync(first)).Ok.Status.Should().Be(WorkOrderStatus.Running);
        var stopped = (await Api.StopAsync(first, good: 2, scrap: 1)).Ok;
        stopped.Status.Should().Be(WorkOrderStatus.Ended);
        stopped.Sessions.Should().HaveCount(2).And.OnlyContain(s => s.EndedAt != null);
        stopped.GoodQuantity.Should().Be(3);
        stopped.ScrapQuantity.Should().Be(1);
        stopped.EndedAt.Should().NotBeNull();

        // Ending the predecessor unlocks the successor; the order is still in progress.
        var second = await WorkOrderIdAsync(planned.Id, 20);
        (await Api.GetWorkOrderAsync(second)).Ok.Status.Should().Be(WorkOrderStatus.Ready);
        (await Api.GetOrderAsync(planned.Id)).Ok.Status.Should().Be(ProductionOrderStatus.InProgress);

        // Last work order ended: the order completes.
        (await Api.StartAsync(second)).Ok.Status.Should().Be(WorkOrderStatus.Running);
        (await Api.StopAsync(second, good: 3)).Ok.Status.Should().Be(WorkOrderStatus.Ended);

        var done = (await Api.GetOrderAsync(planned.Id)).Ok;
        done.Status.Should().Be(ProductionOrderStatus.Completed);
        done.EndedAt.Should().NotBeNull();
        done.ProducedQuantity.Should().Be(3);
        done.ProgressPercent.Should().Be(100);
        done.AllowedActions.Should().BeEmpty();
        (await Api.CancelAsync(planned.Id)).Status.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ProductionOrder_ForbiddenTransitions_Failure_409WithStateAndAllowedActions()
    {
        var (product, _) = await SimpleProductAsync();
        var order = await Api.CreateOrderAsync(product, 1);
        (await Api.StartAsync(999_999)).Status.Should().Be(HttpStatusCode.NotFound);
        (await Api.ReleaseAsync(order.Id)).Ok.Status.Should().Be(ProductionOrderStatus.Released);
        var ready = await WorkOrderIdAsync(order.Id, 10);
        var waiting = await WorkOrderIdAsync(order.Id, 20);

        var startWaiting = await Api.StartAsync(waiting);
        startWaiting.Status.Should().Be(HttpStatusCode.Conflict);
        startWaiting.Raw.Should().Contain("In attesa").And.Contain("Interrompi");
        (await Api.PauseAsync(ready)).Status.Should().Be(HttpStatusCode.Conflict);
        (await Api.ResumeAsync(ready)).Status.Should().Be(HttpStatusCode.Conflict);
        (await Api.StopAsync(ready)).Status.Should().Be(HttpStatusCode.Conflict);
        (await Api.ReleaseAsync(order.Id)).Status.Should().Be(HttpStatusCode.Conflict, "already released");

        (await Api.StartAsync(ready)).Ok.Status.Should().Be(WorkOrderStatus.Running);
        (await Api.StartAsync(ready)).Status.Should().Be(HttpStatusCode.Conflict, "already running");
        (await Api.CancelAsync(order.Id)).Status.Should().Be(HttpStatusCode.Conflict, "work has started");

        // Nothing above changed the state.
        (await Api.GetWorkOrderAsync(ready)).Ok.Status.Should().Be(WorkOrderStatus.Running);
        (await Api.GetWorkOrderAsync(waiting)).Ok.Status.Should().Be(WorkOrderStatus.Waiting);
        (await Api.GetOrderAsync(order.Id)).Ok.Status.Should().Be(ProductionOrderStatus.InProgress);
    }

    [Fact]
    public async Task ProductionOrder_TwoLevelBom_Success_ChildOrderGatesTheParentUntilItCompletes()
    {
        var seg = await Api.CreateSegmentAsync("SEG", setup: 1, run: 1);
        var parent = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var child = await Api.CreateMaterialAsync("SL", MaterialType.Intermediate);
        var raw = await Api.CreateMaterialAsync("RAW", MaterialType.RawMaterial);
        await Api.PutLinearRoutingAsync(parent.Id, seg.Id, seg.Id);
        await Api.PutLinearRoutingAsync(child.Id, seg.Id, seg.Id);
        await Api.AddBomItemAsync(parent.Id, child.Id, 2);
        await Api.AddBomItemAsync(child.Id, raw.Id, 3);

        var order = await Api.CreateOrderAsync(parent.Id, 5);
        var released = (await Api.ReleaseAsync(order.Id)).Ok;

        // Child order: quantity = parent quantity x BOM quantity, released with the parent.
        var all = (await Api.GetOrdersAsync(includeChildren: true)).Ok;
        all.Should().HaveCount(2);
        var childOrder = all.Single(o => o.ParentOrderId == order.Id);
        childOrder.Code.Should().Be($"{order.Code}-1");
        childOrder.Quantity.Should().Be(10);
        childOrder.Status.Should().Be(ProductionOrderStatus.Released);
        released.Materials.Should().ContainSingle(m => m.MaterialId == child.Id && m.RequiredQuantity == 10 && m.ChildProductionOrderId == childOrder.Id);
        (await Api.GetOrderAsync(childOrder.Id)).Ok.Materials.Should().ContainSingle(m => m.MaterialId == raw.Id && m.RequiredQuantity == 30);

        // The parent's first step waits for the last step of the child.
        var parentFirst = await WorkOrderIdAsync(order.Id, 10);
        (await Api.GetWorkOrderAsync(parentFirst)).Ok.Status.Should().Be(WorkOrderStatus.Waiting);
        (await Api.StartAsync(parentFirst)).Status.Should().Be(HttpStatusCode.Conflict);

        var childFirst = await WorkOrderIdAsync(childOrder.Id, 10);
        var childLast = await WorkOrderIdAsync(childOrder.Id, 20);
        (await Api.StartAsync(childFirst)).Ok.Status.Should().Be(WorkOrderStatus.Running);
        (await Api.StopAsync(childFirst, good: 10)).Ok.Status.Should().Be(WorkOrderStatus.Ended);
        (await Api.GetWorkOrderAsync(parentFirst)).Ok.Status.Should().Be(WorkOrderStatus.Waiting, "the child is not done yet");
        (await Api.StartAsync(childLast)).Ok.Status.Should().Be(WorkOrderStatus.Running);
        (await Api.StopAsync(childLast, good: 10)).Ok.Status.Should().Be(WorkOrderStatus.Ended);

        (await Api.GetOrderAsync(childOrder.Id)).Ok.Status.Should().Be(ProductionOrderStatus.Completed);
        (await Api.GetOrderAsync(order.Id)).Ok.Status.Should().Be(ProductionOrderStatus.Released, "the parent starts with its own first work order");
        (await Api.GetWorkOrderAsync(parentFirst)).Ok.Status.Should().Be(WorkOrderStatus.Ready);

        var parentLast = await WorkOrderIdAsync(order.Id, 20);
        (await Api.StartAsync(parentFirst)).Ok.Status.Should().Be(WorkOrderStatus.Running);
        (await Api.StopAsync(parentFirst, good: 5)).Ok.Status.Should().Be(WorkOrderStatus.Ended);
        (await Api.StartAsync(parentLast)).Ok.Status.Should().Be(WorkOrderStatus.Running);
        (await Api.StopAsync(parentLast, good: 5)).Ok.Status.Should().Be(WorkOrderStatus.Ended);
        (await Api.GetOrderAsync(order.Id)).Ok.Status.Should().Be(ProductionOrderStatus.Completed);
    }
}
