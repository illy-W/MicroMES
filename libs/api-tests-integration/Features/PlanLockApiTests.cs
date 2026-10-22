using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

/// <summary>Plan lock of a work order over HTTP: lock, unlock, automatic removal.</summary>
public class PlanLockApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private async Task<List<WorkOrderModel>> OrderAsync()
    {
        var seg = await Api.CreateSegmentAsync("SEG", setup: 60);
        await Api.CreateEquipmentAsync("M1", seg.Id);
        var mat = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        await Api.PutLinearRoutingAsync(mat.Id, seg.Id, seg.Id);
        var order = await Api.CreateOrderAsync(mat.Id, 1);
        await Api.ReleaseAsync(order.Id);
        return (await Api.GetWorkOrdersAsync(order.Id)).Ok;
    }

    private Task PlanAsync(int orderId) => Api.ApplyPlanAsync(MicroMES.ApiFeatures.PlanningFeature.Models.PlanningScope.Order, orderId);

    [Fact]
    public async Task Lock_NotPlanned_Failure_409WithMessage()
    {
        var wo = (await OrderAsync())[0];

        var r = await Api.PostAsync<WorkOrderDetailModel>($"/api/work-orders/{wo.Id}/lock");

        r.Status.Should().Be(HttpStatusCode.Conflict);
        r.Raw.Should().Contain("Il lavoro non è pianificato: pianifica il lavoro oppure scegli un orario con Sposta.");
    }

    [Fact]
    public async Task LockUnlock_Planned_Success_ChangesNothingElse()
    {
        var before = (await OrderAsync())[0];
        await PlanAsync(before.ProductionOrderId);
        var planned = (await Api.GetWorkOrderAsync(before.Id)).Ok;

        var locked = await Api.PostAsync<WorkOrderDetailModel>($"/api/work-orders/{before.Id}/lock");

        locked.Status.Should().Be(HttpStatusCode.OK);
        locked.Ok.IsPlanLocked.Should().BeTrue();
        locked.Ok.PlanLockedAt.Should().NotBeNull();
        locked.Ok.AllowedActions.Should().Contain("Unlock").And.NotContain("Lock");
        (locked.Ok.Status, locked.Ok.PlannedStartAt, locked.Ok.PlannedEndAt, locked.Ok.AssignedEquipment?.Id)
            .Should().Be((planned.Status, planned.PlannedStartAt, planned.PlannedEndAt, planned.AssignedEquipment?.Id));
        (await Api.PostAsync<WorkOrderDetailModel>($"/api/work-orders/{before.Id}/lock")).Status.Should().Be(HttpStatusCode.Conflict);

        var unlocked = await Api.PostAsync<WorkOrderDetailModel>($"/api/work-orders/{before.Id}/unlock");
        unlocked.Ok.IsPlanLocked.Should().BeFalse();
        unlocked.Ok.PlanLockedAt.Should().BeNull();
        unlocked.Ok.PlannedStartAt.Should().Be(planned.PlannedStartAt);
        (await Api.PostAsync<WorkOrderDetailModel>($"/api/work-orders/{before.Id}/unlock")).Status.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Lock_Running_Failure_409()
    {
        var wo = (await OrderAsync())[0];
        await PlanAsync(wo.ProductionOrderId);
        await Api.StartAsync(wo.Id);

        var r = await Api.PostAsync<WorkOrderDetailModel>($"/api/work-orders/{wo.Id}/lock");

        r.Status.Should().Be(HttpStatusCode.Conflict);
        r.Raw.Should().Contain("In esecuzione");
    }

    [Fact]
    public async Task Start_LockedWorkOrder_Success_LockCleared()
    {
        var wo = (await OrderAsync())[0];
        await PlanAsync(wo.ProductionOrderId);
        await Api.PostAsync<WorkOrderDetailModel>($"/api/work-orders/{wo.Id}/lock");

        var started = await Api.StartAsync(wo.Id);

        started.Ok.IsPlanLocked.Should().BeFalse();
        started.Ok.PlanLockedAt.Should().BeNull();
    }

    [Fact]
    public async Task AbortOrder_LockedWorkOrders_Success_AllLocksCleared()
    {
        var wos = await OrderAsync();
        var orderId = wos[0].ProductionOrderId;
        await PlanAsync(orderId);
        foreach (var w in wos) (await Api.PostAsync<WorkOrderDetailModel>($"/api/work-orders/{w.Id}/lock")).Status.Should().Be(HttpStatusCode.OK);

        (await Api.PostAsync<ProductionOrderDetailModel>($"/api/production-orders/{orderId}/abort")).Status.Should().Be(HttpStatusCode.OK);

        (await Api.GetWorkOrdersAsync(orderId)).Ok.Should().OnlyContain(w => !w.IsPlanLocked && w.PlanLockedAt == null);
    }

    [Fact]
    public async Task ReplanAndRemovePlan_LockedWorkOrder_Success_IsKeptAndListed()
    {
        var wos = await OrderAsync();
        var orderId = wos[0].ProductionOrderId;
        await PlanAsync(orderId);
        var first = (await Api.GetWorkOrderAsync(wos[0].Id)).Ok;
        (await Api.PostAsync<WorkOrderDetailModel>($"/api/work-orders/{first.Id}/lock")).Status.Should().Be(HttpStatusCode.OK);

        var preview = (await Api.PreviewPlanAsync(MicroMES.ApiFeatures.PlanningFeature.Models.PlanningScope.All)).Ok;
        preview.NotMoved.Should().ContainSingle(n => n.WorkOrderId == first.Id && n.IsLocked && n.Message == "Fissato: non spostato");
        preview.Items.Should().NotContain(i => i.WorkOrderId == first.Id);
        preview.Summary.LockedCount.Should().Be(1);

        var applied = (await Api.ApplyPlanAsync(MicroMES.ApiFeatures.PlanningFeature.Models.PlanningScope.All)).Ok;
        applied.NotMoved.Should().ContainSingle();
        var after = (await Api.GetWorkOrderAsync(first.Id)).Ok;
        (after.PlannedStartAt, after.PlannedEndAt, after.AssignedEquipment?.Id).Should().Be((first.PlannedStartAt, first.PlannedEndAt, first.AssignedEquipment?.Id));

        var removed = (await Api.RemovePlanAsync(orderId)).Ok;
        removed.KeptLocked.Should().HaveCount(1);
        removed.ClearedWorkOrders.Should().Be(1);
        (await Api.GetWorkOrderAsync(first.Id)).Ok.PlannedStartAt.Should().Be(first.PlannedStartAt);
    }
}
