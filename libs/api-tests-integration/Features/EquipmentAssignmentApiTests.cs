using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiTests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MicroMES.ApiTests.Integration.Features;

public class EquipmentAssignmentApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private async Task SetDefaultAsync(ProcessSegmentModel segment, int? defaultId, params int[] equipment)
        => (await Api.PutAsync<ProcessSegmentModel>($"/api/process-segments/{segment.Id}", new UpdateProcessSegmentRequest
        {
            Code = segment.Code, Name = segment.Name, IsActive = true, EquipmentIds = equipment.ToList(), DefaultEquipmentId = defaultId
        })).Status.Should().Be(HttpStatusCode.OK);

    /// <summary>Product with a one-step routing on a segment with the given machines; returns the released work order.</summary>
    private async Task<WorkOrderModel> ReleasedAsync(ProcessSegmentModel segment, string code = "PF")
    {
        var product = await Api.CreateMaterialAsync(code, MaterialType.FinishedGood);
        await Api.PutLinearRoutingAsync(product.Id, segment.Id);
        var order = await Api.CreateOrderAsync(product.Id, 1);
        var released = (await Api.ReleaseAsync(order.Id)).Ok;
        return released.WorkOrders.Single();
    }

    [Fact]
    public async Task Release_DefaultOrSingleOrAmbiguous_Success_AssignsAccordingly()
    {
        var withDefault = await Api.CreateSegmentAsync("A");
        var single = await Api.CreateSegmentAsync("B");
        var ambiguous = await Api.CreateSegmentAsync("C");
        var none = await Api.CreateSegmentAsync("D");
        var a1 = await Api.CreateEquipmentAsync("A1", withDefault.Id);
        var a2 = await Api.CreateEquipmentAsync("A2", withDefault.Id);
        await Api.CreateEquipmentAsync("B1", single.Id);
        await Api.CreateEquipmentAsync("C1", ambiguous.Id);
        await Api.CreateEquipmentAsync("C2", ambiguous.Id);
        await SetDefaultAsync(withDefault, a2.Id, a1.Id, a2.Id);

        (await ReleasedAsync(withDefault, "P1")).AssignedEquipment!.Code.Should().Be("A2");
        (await ReleasedAsync(single, "P2")).AssignedEquipment!.Code.Should().Be("B1");
        (await ReleasedAsync(ambiguous, "P3")).AssignedEquipment.Should().BeNull();
        (await ReleasedAsync(none, "P4")).AssignedEquipment.Should().BeNull();
    }

    [Fact]
    public async Task Release_DefaultDeactivated_Success_NoAssignmentToInactiveMachine()
    {
        var seg = await Api.CreateSegmentAsync("A");
        var a1 = await Api.CreateEquipmentAsync("A1", seg.Id);
        var a2 = await Api.CreateEquipmentAsync("A2", seg.Id);
        await SetDefaultAsync(seg, a1.Id, a1.Id, a2.Id);
        await Api.DeleteAsync($"/api/equipment/{a1.Id}");

        (await ReleasedAsync(seg)).AssignedEquipment!.Code.Should().Be("A2", "the only active machine");
    }

    [Fact]
    public async Task Default_ChangedAfterRelease_Success_ExistingWorkOrdersUnchanged()
    {
        var seg = await Api.CreateSegmentAsync("A");
        var a1 = await Api.CreateEquipmentAsync("A1", seg.Id);
        var a2 = await Api.CreateEquipmentAsync("A2", seg.Id);
        await SetDefaultAsync(seg, a1.Id, a1.Id, a2.Id);
        var wo = await ReleasedAsync(seg);

        await SetDefaultAsync(seg, a2.Id, a1.Id, a2.Id);

        (await Api.GetWorkOrderAsync(wo.Id)).Ok.AssignedEquipment!.Code.Should().Be("A1");
    }

    [Fact]
    public async Task Assign_FullFlow_Success_And409And422And404()
    {
        var seg = await Api.CreateSegmentAsync("A");
        var other = await Api.CreateSegmentAsync("B");
        var a1 = await Api.CreateEquipmentAsync("A1", seg.Id);
        var a2 = await Api.CreateEquipmentAsync("A2", seg.Id);
        var foreign = await Api.CreateEquipmentAsync("B1", other.Id);
        var wo = await ReleasedAsync(seg);
        string Url(int id) => $"/api/work-orders/{id}/equipment";

        var ok = await Api.PutAsync<WorkOrderDetailModel>(Url(wo.Id), new AssignEquipmentRequest { EquipmentId = a2.Id });
        ok.Status.Should().Be(HttpStatusCode.OK);
        ok.Ok.AssignedEquipment!.Should().BeEquivalentTo(new { Code = "A2", IsActive = true, IsEnabled = true });
        ok.Ok.Status.Should().Be(WorkOrderStatus.Ready);
        (await Api.GetWorkOrdersAsync()).Ok.Single().AssignedEquipment!.Code.Should().Be("A2");

        (await Api.PutAsync<WorkOrderDetailModel>(Url(wo.Id), new AssignEquipmentRequest { EquipmentId = foreign.Id })).Status
            .Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.PutAsync<WorkOrderDetailModel>(Url(wo.Id), new AssignEquipmentRequest { EquipmentId = 404 })).Status
            .Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.PutAsync<WorkOrderDetailModel>(Url(999_999), new AssignEquipmentRequest { EquipmentId = a1.Id })).Status
            .Should().Be(HttpStatusCode.NotFound);

        (await Api.PutAsync<WorkOrderDetailModel>(Url(wo.Id), new AssignEquipmentRequest())).Ok.AssignedEquipment.Should().BeNull();

        await Api.StartAsync(wo.Id, a1.Id);
        (await Api.PutAsync<WorkOrderDetailModel>(Url(wo.Id), new AssignEquipmentRequest { EquipmentId = a2.Id })).Status
            .Should().Be(HttpStatusCode.Conflict, "Running work orders change machine at the next session");
    }

    [Fact]
    public async Task Assign_PhaseWithoutMachines_Failure_422WithClearMessage()
    {
        var seg = await Api.CreateSegmentAsync("A");
        var other = await Api.CreateSegmentAsync("B");
        var machine = await Api.CreateEquipmentAsync("B1", other.Id);
        var wo = await ReleasedAsync(seg);

        var result = await Api.PutAsync<WorkOrderDetailModel>($"/api/work-orders/{wo.Id}/equipment", new AssignEquipmentRequest { EquipmentId = machine.Id });

        result.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        result.Raw.Should().Contain("La fase non ha macchine collegate");
    }

    [Fact]
    public async Task StartResume_WithAndWithoutBody_Success_MachineRulesAndCompatibility()
    {
        var seg = await Api.CreateSegmentAsync("A");
        var a1 = await Api.CreateEquipmentAsync("A1", seg.Id);
        var a2 = await Api.CreateEquipmentAsync("A2", seg.Id);
        var wo = await ReleasedAsync(seg); // two machines, no default: unassigned

        var missing = await Api.StartAsync(wo.Id);
        missing.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        missing.Raw.Should().Contain("Scegli la macchina");
        (await Api.GetWorkOrderAsync(wo.Id)).Ok.Status.Should().Be(WorkOrderStatus.Ready);

        var started = (await Api.StartAsync(wo.Id, a1.Id)).Ok;
        started.CurrentEquipment!.Code.Should().Be("A1");
        started.AssignedEquipment!.Code.Should().Be("A1");
        (await Api.PauseAsync(wo.Id, good: 1)).Ok.CurrentEquipment.Should().BeNull();

        var resumed = (await Api.ResumeAsync(wo.Id)).Ok; // no body: the machine of the last session
        resumed.Sessions.Select(s => s.EquipmentCode).Should().Equal("A1", "A1");
        await Api.PauseAsync(wo.Id, good: 1);
        var moved = (await Api.ResumeAsync(wo.Id, a2.Id)).Ok;
        moved.Sessions.Select(s => s.EquipmentCode).Should().Equal("A1", "A1", "A2");
        moved.AssignedEquipment!.Code.Should().Be("A2");
        (await Api.GetWorkOrdersAsync()).Ok.Should().ContainSingle(w => w.CurrentEquipment != null && w.CurrentEquipment.Code == "A2");
    }

    [Fact]
    public async Task Start_NoMachinesPhase_Success_NoBodyAndEmptyMachine()
    {
        var seg = await Api.CreateSegmentAsync("A");
        var wo = await ReleasedAsync(seg);

        var started = (await Api.StartAsync(wo.Id)).Ok;

        started.Sessions.Single().EquipmentId.Should().BeNull();
        started.AssignedEquipment.Should().BeNull();
    }

    [Fact]
    public async Task Session_MachineRenamedOrDeactivated_Success_SnapshotKeptAndNotEnabledFlagged()
    {
        var seg = await Api.CreateSegmentAsync("A");
        var a1 = await Api.CreateEquipmentAsync("A1", seg.Id);
        await Api.CreateEquipmentAsync("A2", seg.Id);
        var wo = await ReleasedAsync(seg);
        await Api.StartAsync(wo.Id, a1.Id);
        await Api.PauseAsync(wo.Id, good: 1);

        await Api.PutAsync<EquipmentModel>($"/api/equipment/{a1.Id}",
            new UpdateEquipmentRequest { Code = "A1X", Name = "Rinominata", IsActive = false, ProcessSegmentIds = [seg.Id] });

        var detail = (await Api.GetWorkOrderAsync(wo.Id)).Ok;
        detail.Sessions.Single().EquipmentCode.Should().Be("A1", "history keeps the snapshot");
        detail.AssignedEquipment.Should().BeEquivalentTo(new { Code = "A1X", IsActive = false, IsEnabled = false });
        var resume = await Api.ResumeAsync(wo.Id);
        resume.Status.Should().Be(HttpStatusCode.UnprocessableEntity, "the last machine is no longer enabled");
    }

    [Fact]
    public async Task Correction_ChangeMachine_Success_TraceAndValidation()
    {
        var seg = await Api.CreateSegmentAsync("A");
        var a1 = await Api.CreateEquipmentAsync("A1", seg.Id);
        var a2 = await Api.CreateEquipmentAsync("A2", seg.Id);
        var wo = await ReleasedAsync(seg);
        await Api.StartAsync(wo.Id, a1.Id);
        var stopped = (await Api.PauseAsync(wo.Id, good: 1)).Ok; // paused: the order stays open, so the session can be corrected
        var session = stopped.Sessions.Single().Id;
        string Url() => $"/api/work-orders/{wo.Id}/responses/{session}/corrections";

        (await Api.PostAsync<WorkOrderDetailModel>(Url(), new CorrectSessionRequest { GoodQuantity = 1, Notes = "x", EquipmentId = 404 }))
            .Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        var ok = (await Api.PostAsync<WorkOrderDetailModel>(Url(), new CorrectSessionRequest { GoodQuantity = 1, Notes = "Era la A2", EquipmentId = a2.Id })).Ok;

        ok.Sessions.Single().EquipmentCode.Should().Be("A2");
        var trace = ok.Sessions.Single().Corrections.Single();
        (trace.PreviousEquipmentCode, trace.NewEquipmentCode).Should().Be(("A1", "A2"));
    }

    [Fact]
    public async Task WorkOrders_EquipmentFilter_Success_OnlyThatMachine()
    {
        var seg = await Api.CreateSegmentAsync("A");
        var a1 = await Api.CreateEquipmentAsync("A1", seg.Id);
        await Api.CreateEquipmentAsync("A2", seg.Id);
        var wo = await ReleasedAsync(seg, "P1");
        await ReleasedAsync(seg, "P2");
        await Api.PutAsync<WorkOrderDetailModel>($"/api/work-orders/{wo.Id}/equipment", new AssignEquipmentRequest { EquipmentId = a1.Id });

        var filtered = (await Api.GetAsync<List<WorkOrderModel>>($"/api/work-orders?equipmentId={a1.Id}")).Ok;

        filtered.Should().ContainSingle().Which.Id.Should().Be(wo.Id);
    }

    [Fact]
    public async Task ProcessSegment_DefaultRules_Failure_422AndSpecificUniqueMessage()
    {
        var seg = await Api.CreateSegmentAsync("A");
        var a1 = await Api.CreateEquipmentAsync("A1", seg.Id);
        var outside = await Api.CreateEquipmentAsync("Z1");

        var bad = await Api.PutAsync<ProcessSegmentModel>($"/api/process-segments/{seg.Id}", new UpdateProcessSegmentRequest
        {
            Code = "A", Name = "A", IsActive = true, EquipmentIds = [a1.Id], DefaultEquipmentId = outside.Id
        });
        bad.Status.Should().Be(HttpStatusCode.UnprocessableEntity);

        await SetDefaultAsync(seg, a1.Id, a1.Id);
        (await Api.GetAsync<ProcessSegmentModel>($"/api/process-segments/{seg.Id}")).Ok.Equipment.Single().IsDefault.Should().BeTrue();

        // The database refuses a second default on the same segment with the specific message.
        var a2 = await Api.CreateEquipmentAsync("A2", seg.Id);
        var failure = await Record.ExceptionAsync(() => Fixture.WithDbAsync(db => db.EquipmentProcessSegments
            .Where(l => l.EquipmentId == a2.Id && l.ProcessSegmentId == seg.Id).ExecuteUpdateAsync(s => s.SetProperty(l => l.IsDefault, true))));
        var pg = failure!.GetBaseException().Should().BeOfType<PostgresException>().Subject;
        pg.SqlState.Should().Be("23505");
        MicroMES.ApiCore.Persistence.Violations.ConstraintViolation.From(failure)!.Message.Should().Be("La fase ha già una macchina predefinita.");
    }
}
