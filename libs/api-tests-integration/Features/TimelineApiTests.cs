using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;
using MicroMES.ApiTests.Integration.Shared;
using Microsoft.EntityFrameworkCore;

namespace MicroMES.ApiTests.Integration.Features;

/// <summary>Timeline of the planning and the new filters of the work order list, over HTTP on PostgreSQL.</summary>
public class TimelineApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private int _segment, _material, _m1, _m2;

    private async Task SetupAsync(decimal setup = 60)
    {
        _segment = (await Api.CreateSegmentAsync("SEG", setup: setup)).Id;
        _m1 = (await Api.CreateEquipmentAsync("M1", _segment)).Id;
        _m2 = (await Api.CreateEquipmentAsync("M2", _segment)).Id;
        _material = (await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood)).Id;
        await Api.PutLinearRoutingAsync(_material, _segment, _segment);
    }

    private async Task<ProductionOrderDetailModel> PlannedOrderAsync()
    {
        var order = await Api.CreateOrderAsync(_material, 1);
        await Api.ReleaseAsync(order.Id);
        (await Api.ApplyPlanAsync(PlanningScope.Order, order.Id)).Ok.Should().NotBeNull();
        return order;
    }

    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ssZ");

    private Task<ApiResult<TimelineModel>> TimelineAsync(DateTime from, DateTime to, string extra = "")
        => Api.GetAsync<TimelineModel>($"/api/planning/timeline?from={Iso(from)}&to={Iso(to)}{extra}");

    [Fact]
    public async Task Timeline_PlannedOrder_Success_ReturnsMachinesBarsNonWorkingAndLoad()
    {
        await SetupAsync();
        var order = await PlannedOrderAsync();
        var plan = (await Api.GetWorkOrdersAsync(order.Id)).Ok;
        var from = plan.Min(w => w.PlannedStartAt)!.Value.Date.AddDays(-1);

        var t = (await TimelineAsync(from, from.AddDays(7))).Ok;

        t.TimeZoneId.Should().NotBeNullOrEmpty();
        t.LoadPeriod.Should().Be(TimelinePeriod.Day);
        t.Equipment.Select(e => e.Code).Should().Equal("M1", "M2");
        t.WorkOrders.Should().HaveCount(2);
        t.WorkOrders.Should().OnlyContain(w => w.PlannedStartAt != null && w.Lane == 0 && w.EquipmentId != null && w.ProductionOrderCode == order.Code);
        t.WorkOrders.Single(w => w.PredecessorIds.Count == 1).PredecessorIds.Single().Should().Be(t.WorkOrders.Single(w => w.PredecessorIds.Count == 0).Id);
        t.NonWorking.Should().NotBeEmpty().And.OnlyContain(n => n.End > n.Start && n.Start >= from && n.End <= from.AddDays(7));
        t.Load.Should().HaveCountGreaterThanOrEqualTo(14).And.HaveCountLessThanOrEqualTo(16); // 7-8 company days x 2 machines
        t.Load.Should().Contain(l => l.LoadPercent > 0);
    }

    [Fact]
    public async Task Timeline_WindowAndFilters_Success()
    {
        await SetupAsync();
        var order = await PlannedOrderAsync();
        var other = await Api.CreateOrderAsync(_material, 1);
        await Api.ReleaseAsync(other.Id);
        await Api.ApplyPlanAsync(PlanningScope.All);
        var from = DateTime.UtcNow.Date.AddDays(-1);

        (await TimelineAsync(from, from.AddDays(30))).Ok.WorkOrders.Should().HaveCount(4);
        (await TimelineAsync(from.AddDays(-90), from.AddDays(-60))).Ok.WorkOrders.Should().BeEmpty();
        (await TimelineAsync(from, from.AddDays(30), $"&productionOrderId={order.Id}")).Ok.WorkOrders.Should().OnlyContain(w => w.ProductionOrderId == order.Id);
        (await TimelineAsync(from, from.AddDays(30), $"&status={WorkOrderStatus.Running}")).Ok.WorkOrders.Should().BeEmpty();
        (await TimelineAsync(from, from.AddDays(30), $"&status={WorkOrderStatus.Waiting}")).Ok.WorkOrders.Should().NotBeEmpty();

        var oneMachine = (await TimelineAsync(from, from.AddDays(30), $"&equipmentId={_m2}")).Ok;
        oneMachine.Equipment.Should().ContainSingle(e => e.Id == _m2);
        oneMachine.WorkOrders.Should().OnlyContain(w => w.EquipmentId == _m2);
        oneMachine.Load.Should().OnlyContain(l => l.EquipmentId == _m2);

        (await TimelineAsync(from, from.AddDays(30), "&equipmentId=99999")).Status.Should().Be(HttpStatusCode.NotFound);
        (await TimelineAsync(from, from.AddDays(30), "&productionOrderId=99999")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Timeline_InvalidWindow_422_WithSpecificMessages()
    {
        var from = DateTime.UtcNow.Date;
        var inverted = await TimelineAsync(from, from.AddDays(-1));
        inverted.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        inverted.Raw.Should().Contain("dopo l'inizio");

        var same = await TimelineAsync(from, from);
        same.Status.Should().Be(HttpStatusCode.UnprocessableEntity);

        var tooLong = await TimelineAsync(from, from.AddDays(63));
        tooLong.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        tooLong.Raw.Should().Contain("62");

        var max = await TimelineAsync(from, from.AddDays(62));
        max.Ok.LoadPeriod.Should().Be(TimelinePeriod.Week);
    }

    [Fact]
    public async Task Timeline_LongWindow_Success_WeeklyLoad()
    {
        await SetupAsync();
        await PlannedOrderAsync();
        var from = DateTime.UtcNow.Date;
        var t = (await TimelineAsync(from, from.AddDays(30))).Ok;
        t.LoadPeriod.Should().Be(TimelinePeriod.Week);
        t.Load.Where(l => l.EquipmentId == _m1).Should().HaveCountGreaterThanOrEqualTo(4).And.OnlyContain(l => l.PeriodEnd > l.PeriodStart);
    }

    [Fact]
    public async Task Timeline_OverlappingPlansOnACapacityTwoMachine_Success_UseTwoLanes()
    {
        await SetupAsync();
        await Fixture.WithDbAsync(async db =>
        {
            (await db.Equipment.SingleAsync(e => e.Id == _m1)).Capacity = 2;
            (await db.Equipment.SingleAsync(e => e.Id == _m2)).IsActive = false;
            return await db.SaveChangesAsync();
        });
        for (var i = 0; i < 2; i++) await Api.ReleaseAsync((await Api.CreateOrderAsync(_material, 1)).Id);
        await Api.ApplyPlanAsync(PlanningScope.All);

        var from = DateTime.UtcNow.Date.AddDays(-1);
        var t = (await TimelineAsync(from, from.AddDays(30))).Ok;
        t.Equipment.Should().ContainSingle().Which.Capacity.Should().Be(2);
        t.WorkOrders.Select(w => w.Lane).Distinct().Should().BeEquivalentTo([0, 1]);
        t.WorkOrders.Max(w => w.Lane).Should().BeLessThan(2);
    }

    [Fact]
    public async Task Timeline_PhaseWithoutMachines_Success_VirtualRow()
    {
        var seg = (await Api.CreateSegmentAsync("MAN", setup: 30)).Id;
        var material = (await Api.CreateMaterialAsync("PF2", MaterialType.FinishedGood)).Id;
        await Api.PutLinearRoutingAsync(material, seg);
        var order = await Api.CreateOrderAsync(material, 1);
        await Api.ReleaseAsync(order.Id);
        await Api.ApplyPlanAsync(PlanningScope.Order, order.Id);

        var from = DateTime.UtcNow.Date.AddDays(-1);
        var t = (await TimelineAsync(from, from.AddDays(30))).Ok;
        t.Equipment.Should().BeEmpty();
        t.WorkOrders.Should().ContainSingle().Which.EquipmentId.Should().BeNull();
        t.NonWorking.Should().Contain(n => n.EquipmentId == null);
    }

    // ---- work order list --------------------------------------------------------------------------

    [Fact]
    public async Task GetWorkOrders_SortByPlannedStart_FiltersAndCompatibility()
    {
        await SetupAsync();
        var first = await PlannedOrderAsync();
        var unplanned = await Api.CreateOrderAsync(_material, 1);
        await Api.ReleaseAsync(unplanned.Id);
        await Api.ApplyPlanAsync(PlanningScope.Unplanned);
        await Api.RemovePlanAsync(first.Id);
        await Api.ApplyPlanAsync(PlanningScope.Order, first.Id); // the first order now queues after the second one

        var byStart = (await Api.GetAsync<List<WorkOrderModel>>("/api/work-orders?sort=plannedStart")).Ok;
        byStart.Select(w => w.PlannedStartAt).Should().BeInAscendingOrder();
        byStart.Should().HaveCount(4);

        var byCode = (await Api.GetAsync<List<WorkOrderModel>>("/api/work-orders")).Ok;
        byCode.Select(w => (w.ProductionOrderCode, w.OperationNumber)).Should().BeInAscendingOrder();
        (await Api.GetAsync<List<WorkOrderModel>>("/api/work-orders?sort=code")).Ok.Select(w => w.Id).Should().Equal(byCode.Select(w => w.Id));

        (await Api.GetAsync<List<WorkOrderModel>>($"/api/work-orders?productionOrderId={first.Id}&sort=plannedStart")).Ok
            .Should().OnlyContain(w => w.ProductionOrderId == first.Id);
        (await Api.GetAsync<List<WorkOrderModel>>("/api/work-orders?status=Waiting")).Ok.Should().HaveCount(2); // the second step of each order; the first ones are Ready
        (await Api.GetAsync<List<WorkOrderModel>>("/api/work-orders?status=Ready")).Ok.Should().HaveCount(2);
        (await Api.GetAsync<List<WorkOrderModel>>("/api/work-orders?status=Running")).Ok.Should().BeEmpty();
        (await Api.GetAsync<List<WorkOrderModel>>("/api/work-orders?planStatus=Planned")).Ok.Should().HaveCount(4);
        (await Api.GetAsync<List<WorkOrderModel>>("/api/work-orders?planStatus=Late")).Ok.Should().BeEmpty();

        var bad = await Api.GetAsync<List<WorkOrderModel>>("/api/work-orders?sort=banana");
        bad.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        bad.Raw.Should().Contain("banana");
    }

    [Fact]
    public async Task GetWorkOrders_SortByPlannedStart_UnplannedGoLast()
    {
        await SetupAsync();
        await PlannedOrderAsync();
        var unplanned = await Api.CreateOrderAsync(_material, 1);
        await Api.ReleaseAsync(unplanned.Id);

        var list = (await Api.GetAsync<List<WorkOrderModel>>("/api/work-orders?sort=plannedStart")).Ok;
        list.Take(2).Should().OnlyContain(w => w.PlannedStartAt != null);
        list.Skip(2).Should().OnlyContain(w => w.PlannedStartAt == null && w.ProductionOrderId == unplanned.Id);
    }
}
