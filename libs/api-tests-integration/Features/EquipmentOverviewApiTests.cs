using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;
using MicroMES.ApiFeatures.ToolingFeature.Models;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

/// <summary>Machine overview and the tooling of the machine detail, over HTTP on PostgreSQL.</summary>
public class EquipmentOverviewApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private Task<ApiResult<EquipmentOverviewModel>> OverviewAsync(int id) => Api.GetAsync<EquipmentOverviewModel>($"/api/equipment/{id}/overview");

    [Fact]
    public async Task Overview_UnknownMachine_404_WithItalianMessage()
    {
        var r = await OverviewAsync(99999);
        r.Status.Should().Be(HttpStatusCode.NotFound);
        r.Raw.Should().Contain("La macchina non esiste.");
    }

    [Fact]
    public async Task Overview_IdleMachine_EmptyQueueUnknownMountedAndNoLoadErrors()
    {
        var seg = await Api.CreateSegmentAsync("SEG");
        var m = await Api.CreateEquipmentAsync("M1", seg.Id);

        var o = (await OverviewAsync(m.Id)).Ok;

        o.EquipmentId.Should().Be(m.Id);
        o.State.Should().BeOneOf(EquipmentNowState.Free, EquipmentNowState.ClosedNow);
        (o.State == EquipmentNowState.ClosedNow).Should().Be(o.ClosedReason is not null);
        o.Running.Should().BeEmpty();
        o.Queue.Should().BeEmpty();
        o.MountedState.Known.Should().BeFalse();
        o.Alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task Overview_InactiveMachine_IsInactive()
    {
        var seg = await Api.CreateSegmentAsync("SEG");
        var m = await Api.CreateEquipmentAsync("M1", seg.Id);
        (await Api.DeleteAsync($"/api/equipment/{m.Id}")).Status.Should().Be(HttpStatusCode.NoContent);

        (await OverviewAsync(m.Id)).Ok.State.Should().Be(EquipmentNowState.Inactive);
    }

    [Fact]
    public async Task Overview_PlannedAndRunningWork_QueueRunningMountedAndLoad()
    {
        var seg = await Api.CreateSegmentAsync("SEG", setup: 30, run: 10);
        var m1 = await Api.CreateEquipmentAsync("M1", seg.Id);
        var tool = (await Api.PostAsync<ToolingDetailModel>("/api/tooling",
            new CreateToolingRequest { Code = "ST-1", Name = "Stampo", SetupMinutes = 20, EquipmentIds = [m1.Id] })).Ok;
        var mat = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        await Api.PutLinearRoutingAsync(mat.Id, seg.Id);
        var order = await Api.CreateOrderAsync(mat.Id, 5);
        await Api.ReleaseAsync(order.Id);
        (await Api.ApplyPlanAsync(PlanningScope.Order, order.Id)).Ok.Should().NotBeNull();
        var wo = (await Api.GetWorkOrdersAsync(order.Id)).Ok.Single();

        var queued = (await OverviewAsync(m1.Id)).Ok;
        queued.Queue.Should().ContainSingle().Which.WorkOrderId.Should().Be(wo.Id);
        queued.Queue[0].ProductionOrderCode.Should().Be(order.Code);
        queued.Queue[0].PlannedEndAt.Should().BeAfter(queued.Queue[0].PlannedStartAt);
        queued.Load.Load30.Should().BeGreaterThan(0);

        (await Api.StartAsync(wo.Id, m1.Id)).Status.Should().Be(HttpStatusCode.OK);
        var running = (await OverviewAsync(m1.Id)).Ok;
        running.State.Should().Be(EquipmentNowState.Working);
        running.Running.Should().ContainSingle().Which.WorkOrderId.Should().Be(wo.Id);
        running.Running[0].ElapsedMinutes.Should().BeGreaterThanOrEqualTo(0);
        running.Queue.Should().BeEmpty();
        running.MountedState.Known.Should().BeTrue();
        running.MountedState.FromWorkOrderCode.Should().Be(wo.Code);

        // detail: compatible tooling of the machine, consistent with the overview (no tooling snapshot on this routing: not mounted)
        var detail = (await Api.GetAsync<EquipmentModel>($"/api/equipment/{m1.Id}")).Ok;
        detail.CompatibleTooling.Should().ContainSingle(t => t.Id == tool.Id && t.Code == "ST-1" && t.SetupMinutes == 20);
        detail.CompatibleTooling.Single().IsMountedNow.Should().Be(running.MountedState.Tooling.Any(t => t.Id == tool.Id));
        detail.UniversalTooling.Should().BeEmpty();
    }

    [Fact]
    public async Task Detail_UniversalTooling_ListedForAnyMachine_NotInList()
    {
        var seg = await Api.CreateSegmentAsync("SEG");
        var m1 = await Api.CreateEquipmentAsync("M1", seg.Id);
        var m2 = await Api.CreateEquipmentAsync("M2", seg.Id);
        var universal = (await Api.PostAsync<ToolingDetailModel>("/api/tooling", new CreateToolingRequest { Code = "UNI", Name = "Universale" })).Ok;
        var only2 = (await Api.PostAsync<ToolingDetailModel>("/api/tooling", new CreateToolingRequest { Code = "ST-2", Name = "Solo M2", EquipmentIds = [m2.Id] })).Ok;

        var d1 = (await Api.GetAsync<EquipmentModel>($"/api/equipment/{m1.Id}")).Ok;
        d1.UniversalTooling.Should().ContainSingle(t => t.Id == universal.Id);
        d1.CompatibleTooling.Should().BeEmpty();
        var d2 = (await Api.GetAsync<EquipmentModel>($"/api/equipment/{m2.Id}")).Ok;
        d2.CompatibleTooling.Should().ContainSingle(t => t.Id == only2.Id);

        var list = (await Api.GetAsync<List<EquipmentModel>>("/api/equipment")).Ok;
        list.Should().OnlyContain(e => e.CompatibleTooling.Count == 0 && e.UniversalTooling.Count == 0);
    }
}
