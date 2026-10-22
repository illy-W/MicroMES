using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.RoutingFeature.Models;
using MicroMES.ApiFeatures.ToolingFeature.Models;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

/// <summary>Setup in plan and move, tooling filter of the timeline, tooling conflicts and alerts, over HTTP on PostgreSQL.</summary>
public class PlanToolingApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    private static DateTime Future(DayOfWeek day, int hour)
    {
        var d = DateTime.UtcNow.Date.AddDays(21);
        while (d.DayOfWeek != day) d = d.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(new DateTime(d.Year, d.Month, d.Day, hour, 0, 0, DateTimeKind.Unspecified), Rome);
    }

    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ssZ");

    private int _tooling, _m1, _m2;
    private ProductionOrderDetailModel _order = null!;

    /// <summary>Two chained steps on SEG (M1, M2), both needing the tooling ST-1 (quantity 1).</summary>
    private async Task<List<WorkOrderModel>> OrderAsync()
    {
        var seg = await Api.CreateSegmentAsync("SEG", setup: 20, run: 10);
        _m1 = (await Api.CreateEquipmentAsync("M1", seg.Id)).Id;
        _m2 = (await Api.CreateEquipmentAsync("M2", seg.Id)).Id;
        _tooling = (await Api.PostAsync<ToolingDetailModel>("/api/tooling", new CreateToolingRequest { Code = "ST-1", Name = "Stampo", SetupMinutes = 20 })).Ok.Id;
        var mat = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var routing = await Api.PutRoutingAsync(mat.Id, new UpdateRoutingRequest
        {
            Steps =
            [
                new UpdateRoutingStepRequest { Key = "a", OperationNumber = 10, ProcessSegmentId = seg.Id, ToolingIds = [_tooling] },
                new UpdateRoutingStepRequest { Key = "b", OperationNumber = 20, ProcessSegmentId = seg.Id, ToolingIds = [_tooling] }
            ],
            Dependencies = [new UpdateRoutingDependencyRequest { PredecessorKey = "a", SuccessorKey = "b" }]
        });
        routing.Status.Should().Be(System.Net.HttpStatusCode.OK, routing.Raw);
        _order = await Api.CreateOrderAsync(mat.Id, 1);
        await Api.ReleaseAsync(_order.Id);
        return (await Api.GetWorkOrdersAsync(_order.Id)).Ok;
    }

    [Fact]
    public async Task Apply_PersistsSetup_ExposedInPreviewDetailAndTimeline_FilterByTooling()
    {
        await OrderAsync();

        var preview = (await Api.PreviewPlanAsync(PlanningScope.Order, _order.Id)).Ok;
        preview.Items.Should().HaveCount(2).And.OnlyContain(i => i.CurrentSetupMinutes == null);
        preview.Items.OrderBy(i => i.ProposedStart).First().SetupReasons.Should().NotBeEmpty();
        preview.Summary.SetupMinutesAfter.Should().Be(preview.Items.Sum(i => i.SetupMinutes));
        preview.Summary.SetupMinutesBefore.Should().Be(40); // full setup of the step, twice

        var applied = (await Api.ApplyPlanAsync(PlanningScope.Order, _order.Id)).Ok;
        var first = applied.Items.OrderBy(i => i.ProposedStart).First();
        var detail = (await Api.GetWorkOrderAsync(first.WorkOrderId)).Ok;
        detail.PlannedSetupMinutes.Should().Be((int)first.SetupMinutes);
        detail.PlannedSetupReasons.Should().Equal(first.SetupReasons);
        detail.IsFullSetup.Should().Be(first.IsFullSetup);

        var from = applied.Items.Min(i => i.ProposedStart).Date.AddDays(-1);
        var t = (await Api.GetAsync<TimelineModel>($"/api/planning/timeline?from={Iso(from)}&to={Iso(from.AddDays(10))}&toolingId={_tooling}")).Ok;
        t.WorkOrders.Should().HaveCount(2).And.OnlyContain(w => w.PlannedSetupMinutes != null && w.Tooling.Any(x => x.ToolingId == _tooling));
        var none = (await Api.GetAsync<TimelineModel>($"/api/planning/timeline?from={Iso(from)}&to={Iso(from.AddDays(10))}&toolingId=99999")).Status;
        none.Should().Be(System.Net.HttpStatusCode.NotFound);

        (await Api.RemovePlanAsync(_order.Id)).Ok.ClearedWorkOrders.Should().Be(2);
        var cleared = (await Api.GetWorkOrderAsync(first.WorkOrderId)).Ok;
        cleared.PlannedSetupMinutes.Should().BeNull();
        cleared.PlannedSetupReasons.Should().BeEmpty();
    }

    [Fact]
    public async Task Move_OverlapOnSharedTooling_ConflictNamed_AndSetupPersisted()
    {
        var wos = await OrderAsync();
        var start = Future(DayOfWeek.Wednesday, 9);
        (await Api.PostAsync<MoveApplyModel>($"/api/work-orders/{wos[0].Id}/move", new MoveWorkOrderRequest { StartAt = start, EquipmentId = _m1 })).Ok
            .WorkOrder.PlannedSetupMinutes.Should().NotBeNull();

        var preview = (await Api.PostAsync<MovePreviewModel>($"/api/work-orders/{wos[1].Id}/move/preview",
            new MoveWorkOrderRequest { StartAt = start.AddMinutes(10), EquipmentId = _m2 })).Ok;

        preview.SetupMinutes.Should().BeGreaterThan(0);
        preview.SetupReasons.Should().NotBeEmpty();
        preview.Conflicts.Should().Contain(c => c.Code == MoveConflictCode.ToolingOverCapacity && c.Message.Contains("ST-1"));
    }

    [Fact]
    public async Task Alerts_InactiveTooling_OnePerTooling_WithWorkOrders()
    {
        var wos = await OrderAsync();
        (await Api.DeleteAsync($"/api/tooling/{_tooling}")).Status.Should().BeOneOf(System.Net.HttpStatusCode.OK, System.Net.HttpStatusCode.NoContent);

        var alerts = (await Api.GetPlanAlertsAsync()).Ok.Alerts;

        var a = alerts.Single(x => x.Type == PlanAlertType.ToolingInactive);
        a.Severity.Should().Be(PlanAlertSeverity.Warning);
        a.ToolingCode.Should().Be("ST-1");
        a.WorkOrderIds.Should().BeEquivalentTo(wos.Select(w => w.Id));
        alerts.Should().Contain(x => x.Type == PlanAlertType.UnplannableForTooling && x.Severity == PlanAlertSeverity.Critical);
    }
}
