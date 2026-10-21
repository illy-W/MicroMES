using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiTests.Integration.Shared;
using Microsoft.EntityFrameworkCore;

namespace MicroMES.ApiTests.Integration.Features;

public class ProductionOrderRulesTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private int _segment;

    private async Task<int> ProductWithChildAsync()
    {
        _segment = (await Api.CreateSegmentAsync("SEG", setup: 1, run: 1)).Id;
        var parent = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var child = await Api.CreateMaterialAsync("SL", MaterialType.Intermediate);
        await Api.PutLinearRoutingAsync(parent.Id, _segment);
        await Api.PutLinearRoutingAsync(child.Id, _segment, _segment);
        await Api.AddBomItemAsync(parent.Id, child.Id, 1);
        return parent.Id;
    }

    [Fact]
    public async Task Cancel_PlannedOrder_Success_NoWorkOrdersWereEverCreated()
    {
        var product = await ProductWithChildAsync();
        var order = await Api.CreateOrderAsync(product, 1);

        var cancelled = (await Api.CancelAsync(order.Id)).Ok;

        cancelled.Status.Should().Be(ProductionOrderStatus.Cancelled);
        cancelled.EndedAt.Should().NotBeNull();
        (await Api.GetWorkOrdersAsync()).Ok.Should().BeEmpty();
        (await Api.CancelAsync(order.Id)).Status.Should().Be(HttpStatusCode.Conflict, "terminal state");
    }

    [Fact]
    public async Task Cancel_ReleasedOrderWithNoStartedWork_Success_CancelsChildrenAndAbortsEveryWorkOrder()
    {
        var product = await ProductWithChildAsync();
        var order = await Api.CreateOrderAsync(product, 1);
        await Api.ReleaseAsync(order.Id);

        (await Api.CancelAsync(order.Id)).Ok.Status.Should().Be(ProductionOrderStatus.Cancelled);

        (await Api.GetOrdersAsync(includeChildren: true)).Ok.Should().OnlyContain(o => o.Status == ProductionOrderStatus.Cancelled);
        var workOrders = (await Api.GetWorkOrdersAsync()).Ok;
        workOrders.Should().HaveCount(3).And.OnlyContain(w => w.Status == WorkOrderStatus.Aborted);
    }

    [Fact]
    public async Task Cancel_ParentWhileAChildWorkOrderIsStarted_Failure_409ThenAbortClosesEverything()
    {
        var product = await ProductWithChildAsync();
        var order = await Api.CreateOrderAsync(product, 1);
        await Api.ReleaseAsync(order.Id);
        var childOrder = (await Api.GetOrdersAsync()).Ok.Single(o => o.ParentOrderId == order.Id);
        var childFirst = (await Api.GetWorkOrdersAsync(childOrder.Id)).Ok.Single(w => w.OperationNumber == 10);
        await Api.StartAsync(childFirst.Id);

        var cancel = await Api.CancelAsync(order.Id);
        var cancelChild = await Api.CancelAsync(childOrder.Id);

        cancel.Status.Should().Be(HttpStatusCode.Conflict);
        cancel.Raw.Should().Contain("lavori già avviati");
        cancelChild.Status.Should().Be(HttpStatusCode.Conflict, "a child follows its parent: commands go to the parent");
        (await Api.GetOrderAsync(order.Id)).Ok.Status.Should().Be(ProductionOrderStatus.Released);
        (await Api.GetWorkOrderAsync(childFirst.Id)).Ok.Status.Should().Be(WorkOrderStatus.Running);

        var aborted = await Api.PostAsync<MicroMES.ApiFeatures.ProductionOrderFeature.Models.ProductionOrderDetailModel>(
            $"/api/production-orders/{order.Id}/abort", new { notes = "cliente ha ritirato" });
        aborted.Ok.Status.Should().Be(ProductionOrderStatus.Aborted);
        aborted.Ok.Notes.Should().Contain("cliente ha ritirato");
        (await Api.GetOrdersAsync()).Ok.Should().OnlyContain(o => o.Status == ProductionOrderStatus.Aborted);
        var running = (await Api.GetWorkOrderAsync(childFirst.Id)).Ok;
        running.Status.Should().Be(WorkOrderStatus.Aborted);
        running.Sessions.Should().ContainSingle().Which.EndedAt.Should().NotBeNull("aborting closes the open session");
    }

    [Fact]
    public async Task Release_CyclicBom_Failure_422AndNothingPersisted()
    {
        _segment = (await Api.CreateSegmentAsync("SEG")).Id;
        var a = await Api.CreateMaterialAsync("A", MaterialType.FinishedGood);
        var b = await Api.CreateMaterialAsync("B", MaterialType.Intermediate);
        await Api.PutLinearRoutingAsync(a.Id, _segment);
        await Api.PutLinearRoutingAsync(b.Id, _segment);
        (await Api.AddBomItemAsync(a.Id, b.Id, 1)).Status.Should().Be(HttpStatusCode.Created);
        (await Api.AddBomItemAsync(b.Id, a.Id, 1)).Status.Should().Be(HttpStatusCode.Created, "cycles are only caught at release time");
        var order = await Api.CreateOrderAsync(a.Id, 1);

        var release = await Api.ReleaseAsync(order.Id);

        release.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        release.Raw.Should().Contain("ciclica");
        (await Api.GetOrderAsync(order.Id)).Ok.Status.Should().Be(ProductionOrderStatus.Planned);
        (await Api.GetOrdersAsync()).Ok.Should().ContainSingle();
        (await Api.GetWorkOrdersAsync()).Ok.Should().BeEmpty();
    }

    [Fact]
    public async Task Release_ChildCodeAlreadyTaken_Failure_409AndTransactionRollsEverythingBack()
    {
        var product = await ProductWithChildAsync();
        var order = await Api.CreateOrderAsync(product, 1);
        // Sabotage: an unrelated order already owns the code the child order is about to take ("<code>-1"),
        // so the unique index fails after the parent's work orders have been inserted.
        await Fixture.WithDbAsync(async db =>
        {
            db.ProductionOrders.Add(new ProductionOrder
            {
                Code = $"{order.Code}-1", MaterialId = product, Quantity = 1, Status = ProductionOrderStatus.Planned,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return 0;
        });

        var release = await Api.ReleaseAsync(order.Id);

        release.Status.Should().Be(HttpStatusCode.Conflict);
        release.Raw.Should().Contain("Esiste già un ordine di produzione con questo codice")
            .And.NotContain("IX_", "the constraint name stays in the logs");
        (await Api.GetOrderAsync(order.Id)).Ok.Status.Should().Be(ProductionOrderStatus.Planned);
        (await Api.GetWorkOrdersAsync()).Ok.Should().BeEmpty("the work orders inserted before the failure were rolled back");
        (await Api.GetOrdersAsync()).Ok.Should().HaveCount(2, "root and sabotage only: no child order survived");
        var leftovers = await Fixture.WithDbAsync(async db =>
            await db.WorkOrderDependencies.CountAsync() + await db.WorkOrderMaterials.CountAsync());
        leftovers.Should().Be(0);
    }

    [Fact]
    public async Task Create_ParallelRequests_Success_RetryGivesDistinctCodesOrCleanConflicts()
    {
        var product = await ProductWithChildAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => Api.PostAsync<ProductionOrderDetailModel>("/api/production-orders",
                new { materialId = product, quantity = 1 })));

        results.Should().OnlyContain(r => r.Status == HttpStatusCode.Created || r.Status == HttpStatusCode.Conflict,
            "a lost race on the order code is retried or answered 409, never 500");
        var codes = results.Where(r => r.IsSuccess).Select(r => r.Body!.Code).ToList();
        codes.Should().OnlyHaveUniqueItems().And.NotBeEmpty();
        (await Api.GetOrdersAsync()).Ok.Should().HaveCount(codes.Count);
    }
}
