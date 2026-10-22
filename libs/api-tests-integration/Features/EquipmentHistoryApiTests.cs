using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

public class EquipmentHistoryApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private Task<ApiResult<EquipmentHistoryModel>> HistoryAsync(int id, string query = "")
        => Api.GetAsync<EquipmentHistoryModel>($"/api/equipment/{id}/history{query}");

    [Fact]
    public async Task History_UnknownMachine_404()
        => (await HistoryAsync(99999)).Status.Should().Be(HttpStatusCode.NotFound);

    [Fact]
    public async Task History_InvalidDays_422_WithMessage()
    {
        var seg = await Api.CreateSegmentAsync("SEG");
        var m = await Api.CreateEquipmentAsync("M1", seg.Id);
        var r = await HistoryAsync(m.Id, "?days=10");
        r.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        r.Raw.Should().Contain("Il periodo deve essere 7, 30 o 90 giorni.");
    }

    [Fact]
    public async Task History_NoSessions_EmptyAndNoScrapPercent()
    {
        var seg = await Api.CreateSegmentAsync("SEG");
        var m = await Api.CreateEquipmentAsync("M1", seg.Id);
        var h = (await HistoryAsync(m.Id)).Ok;
        h.Days.Should().Be(30);
        h.Summary.WorkOrders.Should().Be(0);
        h.Summary.ScrapPercent.Should().BeNull();
        h.Sessions.Items.Should().BeEmpty();
        h.Sessions.TotalCount.Should().Be(0);
        h.ScrapByReason.Should().BeEmpty();
    }

    [Fact]
    public async Task History_ClosedAndRunningSessions_SummaryAndList()
    {
        var seg = await Api.CreateSegmentAsync("SEG", run: 10);
        var m1 = await Api.CreateEquipmentAsync("M1", seg.Id);
        var m2 = await Api.CreateEquipmentAsync("M2", seg.Id);
        var mat = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        await Api.PutLinearRoutingAsync(mat.Id, seg.Id);
        var order = await Api.CreateOrderAsync(mat.Id, 10);
        await Api.ReleaseAsync(order.Id);
        var wo = (await Api.GetWorkOrdersAsync(order.Id)).Ok.Single();

        (await Api.StartAsync(wo.Id, m1.Id)).Status.Should().Be(HttpStatusCode.OK);
        (await Api.StopAsync(wo.Id, good: 8, scrap: 2)).Status.Should().Be(HttpStatusCode.OK);

        var h = (await HistoryAsync(m1.Id, "?days=7&page=1&pageSize=5")).Ok;
        h.Summary.WorkOrders.Should().Be(1);
        h.Summary.GoodQuantity.Should().Be(8);
        h.Summary.ScrapQuantity.Should().Be(2);
        h.Summary.ScrapPercent.Should().Be(20m);
        h.Sessions.Items.Should().ContainSingle().Which.WorkOrderId.Should().Be(wo.Id);
        h.Sessions.Items[0].IsRunning.Should().BeFalse();
        h.ScrapByReason.Should().ContainSingle().Which.Name.Should().Be("Senza motivo");

        // the other machine has nothing (session machine, not planned one)
        (await HistoryAsync(m2.Id)).Ok.Sessions.TotalCount.Should().Be(0);
    }
}
