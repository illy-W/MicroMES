using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;
using MicroMES.ApiTests.Integration.Shared;
using Microsoft.EntityFrameworkCore;

namespace MicroMES.ApiTests.Integration.Features;

/// <summary>Planning over HTTP on PostgreSQL: preview, apply, removal, alerts and the plan in the work order and order models.</summary>
public class PlanningApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private int _segment, _material;
    private EquipmentModelLite _m1 = null!, _m2 = null!;

    private sealed record EquipmentModelLite(int Id, string Code);

    /// <summary>Segment with two machines (M1 first), routing of two chained steps, and the material.</summary>
    private async Task SetupAsync(decimal setup = 60)
    {
        _segment = (await Api.CreateSegmentAsync("SEG", setup: setup)).Id;
        var m1 = await Api.CreateEquipmentAsync("M1", _segment);
        var m2 = await Api.CreateEquipmentAsync("M2", _segment);
        _m1 = new(m1.Id, m1.Code);
        _m2 = new(m2.Id, m2.Code);
        _material = (await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood)).Id;
        await Api.PutLinearRoutingAsync(_material, _segment, _segment);
    }

    private async Task<ProductionOrderDetailModel> OrderAsync(bool release = true, DateTime? requestedEnd = null, int quantity = 1)
    {
        var order = (await Api.PostAsync<ProductionOrderDetailModel>("/api/production-orders", new CreateProductionOrderRequest
        {
            MaterialId = _material, Quantity = quantity, RequestedEnd = requestedEnd
        })).Ok;
        return release ? (await Api.ReleaseAsync(order.Id)).Ok : order;
    }

    // ---- preview ----------------------------------------------------------------------------------

    [Fact]
    public async Task Preview_ReleasedOrder_Success_ProposesPlanAndWritesNothing()
    {
        await SetupAsync();
        var order = await OrderAsync();

        var preview = (await Api.PreviewPlanAsync(PlanningScope.Order, order.Id)).Ok;

        preview.Items.Should().HaveCount(2);
        preview.Items.Should().OnlyContain(i => i.Change == PlanChangeKind.New && i.ProposedEnd > i.ProposedStart);
        preview.Items[1].ProposedStart.Should().BeOnOrAfter(preview.Items[0].ProposedEnd);
        preview.Items[1].Reasons.Should().Contain(r => r.Code == MicroMES.ApiFeatures.PlanningFeature.Engine.PlanningReasonCode.WaitsForPredecessor);
        preview.Summary.Planned.Should().Be(2);
        preview.Token.Should().NotBeNullOrEmpty();

        var wos = (await Api.GetWorkOrdersAsync(order.Id)).Ok;
        wos.Should().OnlyContain(w => w.PlannedStartAt == null && w.PlannedEndAt == null && w.PlanStatus == PlanStatus.Unplanned);
        (await Api.GetOrderAsync(order.Id)).Ok.PlanStatus.Should().Be(PlanStatus.Unplanned);
    }

    [Theory]
    [InlineData(PlanningScope.Order)]
    [InlineData(PlanningScope.Unplanned)]
    [InlineData(PlanningScope.All)]
    public async Task Preview_EveryScope_Success(PlanningScope scope)
    {
        await SetupAsync();
        var order = await OrderAsync();
        var preview = (await Api.PreviewPlanAsync(scope, scope == PlanningScope.Order ? order.Id : null)).Ok;
        preview.Scope.Should().Be(scope);
        preview.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Preview_Errors_AreSpecific_404_409_422()
    {
        await SetupAsync();
        var planned = await OrderAsync(release: false);

        var missing = await Api.PreviewPlanAsync(PlanningScope.Order, 99999);
        missing.Status.Should().Be(HttpStatusCode.NotFound);

        var notReleased = await Api.PreviewPlanAsync(PlanningScope.Order, planned.Id);
        notReleased.Status.Should().Be(HttpStatusCode.Conflict);
        notReleased.Raw.Should().Contain(planned.Code).And.Contain("rilasciat");

        var noId = await Api.PreviewPlanAsync(PlanningScope.Order);
        noId.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        noId.Raw.Should().Contain("productionOrderId");
    }

    [Fact]
    public async Task Apply_ConflictOnOrdersNotOpen_NothingWritten()
    {
        await SetupAsync();
        var cancelled = await OrderAsync(release: false);
        (await Api.CancelAsync(cancelled.Id)).IsSuccess.Should().BeTrue();

        (await Api.ApplyPlanAsync(PlanningScope.Order, cancelled.Id)).Status.Should().Be(HttpStatusCode.Conflict);
        (await Api.ApplyPlanAsync(PlanningScope.Order, 99999)).Status.Should().Be(HttpStatusCode.NotFound);
        (await Api.ApplyPlanAsync(PlanningScope.Order)).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ---- apply ------------------------------------------------------------------------------------

    [Fact]
    public async Task Apply_Order_Success_PlanVisibleOnWorkOrdersAndOrder()
    {
        await SetupAsync();
        var order = await OrderAsync();
        var preview = (await Api.PreviewPlanAsync(PlanningScope.Order, order.Id)).Ok;

        var applied = (await Api.ApplyPlanAsync(PlanningScope.Order, order.Id, preview.Token)).Ok;
        applied.DiffersFromPreview.Should().BeFalse();
        applied.Summary.Planned.Should().Be(2);

        var wos = (await Api.GetWorkOrdersAsync(order.Id)).Ok;
        wos.Should().OnlyContain(w => w.PlannedStartAt != null && w.PlannedEndAt != null && w.PlannedAt != null && w.PlanStatus == PlanStatus.Planned);
        wos.Should().OnlyContain(w => w.AssignedEquipment != null);
        wos.Select(w => w.PlannedStartAt).Should().Equal(applied.Items.Select(i => (DateTime?)i.ProposedStart));

        var detail = (await Api.GetOrderAsync(order.Id)).Ok;
        detail.PlanStatus.Should().Be(PlanStatus.Planned);
        detail.PlannedStart.Should().Be(wos.Min(w => w.PlannedStartAt));
        detail.PlannedEnd.Should().Be(wos.Max(w => w.PlannedEndAt));
        detail.EstimatedEnd.Should().Be(detail.PlannedEnd); // the plan replaces the estimate
        (await Api.GetOrderOverviewAsync(order.Id)).Ok.Summary.EstimatedEnd.Should().Be(detail.PlannedEnd);

        (await Api.GetOrdersAsync()).Ok.Single(o => o.Id == order.Id).PlannedEnd.Should().Be(detail.PlannedEnd);
        var overview = (await Api.GetOrderOverviewAsync(order.Id)).Ok;
        overview.WorkOrders.Should().OnlyContain(w => w.PlannedStartAt != null && w.PlanStatus == PlanStatus.Planned);
        overview.Orders.Single().PlanStatus.Should().Be(PlanStatus.Planned);
    }

    [Fact]
    public async Task Apply_WhenTheDataChangedSincePreview_Success_FlagsTheDifference()
    {
        await SetupAsync();
        var order = await OrderAsync();
        var preview = (await Api.PreviewPlanAsync(PlanningScope.All)).Ok;
        await OrderAsync(); // a second order appears: the plan is not the one that was previewed

        var applied = (await Api.ApplyPlanAsync(PlanningScope.All, previewToken: preview.Token)).Ok;
        applied.DiffersFromPreview.Should().BeTrue();
        applied.Items.Should().HaveCount(4);
        order.Id.Should().BePositive();
    }

    [Fact]
    public async Task Apply_UnplannedScope_Success_LeavesExistingPlansAlone()
    {
        await SetupAsync();
        var first = await OrderAsync();
        await Api.ApplyPlanAsync(PlanningScope.Order, first.Id);
        var before = (await Api.GetWorkOrdersAsync(first.Id)).Ok.Select(w => (w.Id, w.PlannedStartAt, w.PlannedEndAt)).ToList();

        var second = await OrderAsync();
        var applied = (await Api.ApplyPlanAsync(PlanningScope.Unplanned)).Ok;

        applied.Items.Should().OnlyContain(i => i.ProductionOrderId == second.Id);
        (await Api.GetWorkOrdersAsync(first.Id)).Ok.Select(w => (w.Id, w.PlannedStartAt, w.PlannedEndAt)).Should().Equal(before);
        // The machines are shared: no overlap on the same machine (capacity 1).
        var all = (await Api.GetWorkOrdersAsync()).Ok.Where(w => w.AssignedEquipment != null).ToList();
        foreach (var g in all.GroupBy(w => w.AssignedEquipment!.Id))
        {
            var ordered = g.OrderBy(w => w.PlannedStartAt).ToList();
            for (var i = 1; i < ordered.Count; i++) ordered[i].PlannedStartAt.Should().BeOnOrAfter(ordered[i - 1].PlannedEndAt!.Value);
        }
    }

    [Fact]
    public async Task Apply_AllScope_Success_ReplansEverythingAndKeepsAnAssignedMachine()
    {
        await SetupAsync();
        var order = await OrderAsync();
        var wo = (await Api.GetWorkOrdersAsync(order.Id)).Ok.First();
        (await Api.PutAsync<WorkOrderDetailModel>($"/api/work-orders/{wo.Id}/equipment", new AssignEquipmentRequest { EquipmentId = _m2.Id })).IsSuccess.Should().BeTrue();

        await Api.ApplyPlanAsync(PlanningScope.All);
        var again = (await Api.PreviewPlanAsync(PlanningScope.All)).Ok;

        again.Items.Single(i => i.WorkOrderId == wo.Id).ProposedEquipment!.Id.Should().Be(_m2.Id);
        again.Items.Single(i => i.WorkOrderId == wo.Id).AssignsEquipment.Should().BeFalse();
        again.Summary.Considered.Should().Be(2);
    }

    // ---- remove -----------------------------------------------------------------------------------

    [Fact]
    public async Task RemovePlan_Success_ClearsDatesKeepsMachines_AndErrors()
    {
        await SetupAsync();
        var order = await OrderAsync();
        await Api.ApplyPlanAsync(PlanningScope.Order, order.Id);

        var removed = (await Api.RemovePlanAsync(order.Id)).Ok;
        removed.ClearedWorkOrders.Should().Be(2);

        var wos = (await Api.GetWorkOrdersAsync(order.Id)).Ok;
        wos.Should().OnlyContain(w => w.PlannedStartAt == null && w.PlannedEndAt == null && w.PlannedAt == null
                                       && w.PlanStatus == PlanStatus.Unplanned && w.AssignedEquipment != null);
        (await Api.GetOrderAsync(order.Id)).Ok.PlannedEnd.Should().BeNull();

        (await Api.RemovePlanAsync(99999)).Status.Should().Be(HttpStatusCode.NotFound);
        var planned = await OrderAsync(release: false);
        (await Api.RemovePlanAsync(planned.Id)).Status.Should().Be(HttpStatusCode.Conflict);
    }

    // ---- plan status and alerts -------------------------------------------------------------------

    [Fact]
    public async Task PlanStatus_LateOrder_Success_AndLateAlertWithDays()
    {
        await SetupAsync(setup: 600); // 10 working hours per work order
        var order = await OrderAsync(requestedEnd: DateTime.UtcNow.AddMinutes(30));
        await Api.ApplyPlanAsync(PlanningScope.Order, order.Id);

        (await Api.GetOrderAsync(order.Id)).Ok.PlanStatus.Should().Be(PlanStatus.Late);
        (await Api.GetWorkOrdersAsync(order.Id)).Ok.Should().OnlyContain(w => w.PlanStatus == PlanStatus.Late);

        var alerts = (await Api.GetPlanAlertsAsync()).Ok;
        var late = alerts.Alerts.Single(a => a.Type == PlanAlertType.Late);
        late.ProductionOrderId.Should().Be(order.Id);
        late.Severity.Should().Be(PlanAlertSeverity.Critical);
        late.LateDays.Should().BeGreaterThanOrEqualTo(1);
        late.Message.Should().Contain(order.Code);
        late.Suggestion.Should().NotBeEmpty();
        alerts.CriticalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Alerts_NoPlanNoProblems_Success_Empty()
    {
        await SetupAsync();
        await OrderAsync();
        (await Api.GetPlanAlertsAsync()).Ok.Alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task Alerts_MachineDeactivated_Success_StaleAndInactiveMachine()
    {
        await SetupAsync();
        var order = await OrderAsync();
        await Api.ApplyPlanAsync(PlanningScope.Order, order.Id);
        var used = (await Api.GetWorkOrdersAsync(order.Id)).Ok.First().AssignedEquipment!;

        await Fixture.WithDbAsync(async db =>
        {
            (await db.Equipment.SingleAsync(e => e.Id == used.Id)).IsActive = false;
            return await db.SaveChangesAsync();
        });

        (await Api.GetOrderAsync(order.Id)).Ok.PlanStatus.Should().Be(PlanStatus.Stale);
        var alerts = (await Api.GetPlanAlertsAsync()).Ok.Alerts;
        alerts.Should().Contain(a => a.Type == PlanAlertType.InactiveOrDisabledMachine && a.EquipmentCode == used.Code && a.WorkOrderId != null);
    }

    [Fact]
    public async Task Alerts_NoActiveMachineForPhase_Success_UnplannableWithReason()
    {
        await SetupAsync();
        var order = await OrderAsync();
        await Fixture.WithDbAsync(async db =>
        {
            foreach (var e in await db.Equipment.ToListAsync()) e.IsActive = false;
            return await db.SaveChangesAsync();
        });

        var preview = (await Api.PreviewPlanAsync(PlanningScope.Order, order.Id)).Ok;
        preview.Items.Should().BeEmpty();
        preview.Unplanned.Should().HaveCount(2);
        preview.Summary.Unplanned.Should().Be(2);

        var alerts = (await Api.GetPlanAlertsAsync()).Ok.Alerts;
        alerts.Where(a => a.Type == PlanAlertType.Unplannable).Should().HaveCount(2);
        alerts.First(a => a.Type == PlanAlertType.Unplannable).Message.Should().Contain("macchin");
    }
}
