using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

public class ProductionOrderEstimateTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private async Task<int> ReleasedOrderAsync(decimal quantity)
    {
        var seg = (await Api.CreateSegmentAsync("SEG", setup: 30, run: 10)).Id;
        var material = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        await Api.PutLinearRoutingAsync(material.Id, seg, seg);
        var order = await Api.CreateOrderAsync(material.Id, quantity);
        await Api.ReleaseAsync(order.Id);
        return order.Id;
    }

    [Fact]
    public async Task GetOrder_ReleasedOrder_Success_EstimatedEndInTheFutureOnWorkingTime()
    {
        var before = DateTime.UtcNow;
        var order = (await Api.GetOrderAsync(await ReleasedOrderAsync(3))).Ok;

        order.EstimatedEnd.Should().NotBeNull();
        order.EstimatedEnd!.Value.Should().BeAfter(before);
        order.EstimatedEnd.Value.Kind.Should().Be(DateTimeKind.Utc);
        // 2 x (30 + 3 x 10) = 120 working minutes: never more than a few working days away.
        order.EstimatedEnd.Value.Should().BeBefore(before.AddDays(6));
        // Stored planned minutes stay the standard ones.
        order.WorkOrders.Should().OnlyContain(w => w.PlannedMinutes == 60);
    }

    [Fact]
    public async Task GetOverview_ReleasedOrder_Success_SameEstimateAsDetail()
    {
        var id = await ReleasedOrderAsync(1);
        var detail = (await Api.GetOrderAsync(id)).Ok;
        var overview = (await Api.GetOrderOverviewAsync(id)).Ok;

        overview.Summary.EstimatedEnd.Should().NotBeNull();
        overview.Summary.EstimatedEnd!.Value.Should().BeCloseTo(detail.EstimatedEnd!.Value, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetOrder_PlannedOrder_Success_NoEstimate()
    {
        var seg = (await Api.CreateSegmentAsync("SEG", setup: 5, run: 1)).Id;
        var material = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        await Api.PutLinearRoutingAsync(material.Id, seg);
        var order = await Api.CreateOrderAsync(material.Id, 1);

        (await Api.GetOrderAsync(order.Id)).Ok.EstimatedEnd.Should().BeNull();
    }

    [Fact]
    public async Task GetOrder_MachineWithLowEfficiency_Success_EstimateLaterThanStandardMachine()
    {
        var seg = (await Api.CreateSegmentAsync("SEG", setup: 0, run: 60)).Id;
        var machine = await Api.CreateEquipmentAsync("SLOW", seg);
        await Fixture.WithDbAsync(async db =>
        {
            var e = await db.Equipment.FindAsync(machine.Id);
            e!.EfficiencyPercent = 25m;
            return await db.SaveChangesAsync();
        });
        var material = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        await Api.PutLinearRoutingAsync(material.Id, seg);
        var order = await Api.CreateOrderAsync(material.Id, 2); // 120 standard minutes -> 480 real minutes on the slow machine
        await Api.ReleaseAsync(order.Id);

        var slow = (await Api.GetOrderAsync(order.Id)).Ok;
        slow.WorkOrders.Single().AssignedEquipment.Should().NotBeNull();
        var asDefault = await Fixture.WithDbAsync(async db =>
        {
            var e = await db.Equipment.FindAsync(machine.Id);
            e!.EfficiencyPercent = 100m;
            await db.SaveChangesAsync();
            return 0;
        });
        var fast = (await Api.GetOrderAsync(order.Id)).Ok;

        slow.EstimatedEnd.Should().BeAfter(fast.EstimatedEnd!.Value);
        slow.WorkOrders.Single().PlannedMinutes.Should().Be(fast.WorkOrders.Single().PlannedMinutes);
    }
}
