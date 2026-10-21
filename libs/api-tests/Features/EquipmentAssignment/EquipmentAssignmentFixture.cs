using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.EquipmentFeature.Services;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.EquipmentAssignment;

/// <summary>Machine selection rule, assignment, start/resume and correction with a machine (in-memory SQLite).</summary>
public class EquipmentAssignmentFixture : BaseFeatureFixture
{
    private int _segment, _free, _order;
    private int _m1, _m2, _old, _foreign;

    public EquipmentAssignmentFixture()
    {
        var material = AddMaterial("PF");
        _segment = AddSegment("SEG");
        _free = AddSegment("FREE"); // no machines
        var other = AddSegment("OTH");
        Arrange(db =>
        {
            var now = DateTime.UtcNow;
            Equipment E(string code, bool active = true) => new() { Code = code, Name = $"Macchina {code}", IsActive = active, CreatedAt = now, UpdatedAt = now };
            var m1 = E("M1"); var m2 = E("M2"); var old = E("OLD", false); var foreign = E("FOR");
            db.Equipment.AddRange(m1, m2, old, foreign);
            db.SaveChanges();
            db.EquipmentProcessSegments.AddRange(
                new EquipmentProcessSegment { EquipmentId = m1.Id, ProcessSegmentId = _segment },
                new EquipmentProcessSegment { EquipmentId = m2.Id, ProcessSegmentId = _segment },
                new EquipmentProcessSegment { EquipmentId = old.Id, ProcessSegmentId = _segment },
                new EquipmentProcessSegment { EquipmentId = foreign.Id, ProcessSegmentId = other });
            var order = new ProductionOrder { Code = "OP-1", MaterialId = material, Quantity = 10, Status = ProductionOrderStatus.Released, CreatedAt = now, UpdatedAt = now };
            db.ProductionOrders.Add(order);
            db.SaveChanges();
            (_m1, _m2, _old, _foreign, _order) = (m1.Id, m2.Id, old.Id, foreign.Id, order.Id);
        });
    }

    private int AddWo(int op, WorkOrderStatus status, int? segment = null, int? assigned = null)
    {
        var id = 0;
        Arrange(db =>
        {
            var now = DateTime.UtcNow;
            var wo = new WorkOrder
            {
                ProductionOrderId = _order, OperationNumber = op, ProcessSegmentId = segment ?? _segment, ProcessSegmentCode = "SEG",
                ProcessSegmentName = "SEG", PlannedQuantity = 10, Status = status, AssignedEquipmentId = assigned, CreatedAt = now, UpdatedAt = now
            };
            db.WorkOrders.Add(wo);
            db.SaveChanges();
            id = wo.Id;
        });
        return id;
    }

    private int AddClosedSession(int wo, int? equipment, string? code)
    {
        var id = 0;
        Arrange(db =>
        {
            var s = new WorkOrderResponse
            {
                WorkOrderId = wo, StartedAt = DateTime.UtcNow.AddHours(-2), EndedAt = DateTime.UtcNow.AddHours(-1),
                GoodQuantity = 1, EquipmentId = equipment, EquipmentCode = code, EquipmentName = code
            };
            db.WorkOrderResponses.Add(s);
            db.SaveChanges();
            id = s.Id;
        });
        return id;
    }

    private static SegmentMachine M(int id, string code, bool active = true, bool isDefault = false)
        => new(new Equipment { Id = id, Code = code, Name = code, IsActive = active }, isDefault);

    // ---- selection rule (pure) ----------------------------------------------------------------------

    [Fact]
    public void Pick_ActiveDefault_Success_ChoosesDefault()
        => EquipmentSelector.Pick([M(1, "A"), M(2, "B", isDefault: true)])!.Id.Should().Be(2);

    [Fact]
    public void Pick_InactiveDefaultAndOneActive_Success_ChoosesTheOnlyActive()
        => EquipmentSelector.Pick([M(1, "A"), M(2, "B", active: false, isDefault: true)])!.Id.Should().Be(1);

    [Fact]
    public void Pick_TwoActiveNoDefault_Success_None()
        => EquipmentSelector.Pick([M(1, "A"), M(2, "B")]).Should().BeNull();

    [Fact]
    public void Pick_NoMachines_Success_None()
    {
        EquipmentSelector.Pick([]).Should().BeNull();
        EquipmentSelector.Pick(null).Should().BeNull();
    }

    [Fact]
    public void Check_Cases_Success_MessagesPerCase()
    {
        EquipmentSelector.Check([], 1).Should().Be(EquipmentSelector.NoMachinesMessage);
        EquipmentSelector.Check([M(1, "A")], 9).Should().Contain("non è collegata");
        EquipmentSelector.Check([M(1, "A", active: false)], 1).Should().Contain("disattivata");
        EquipmentSelector.Check([M(1, "A")], 1).Should().BeNull();
    }

    // ---- assignment ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(WorkOrderStatus.Waiting)]
    [InlineData(WorkOrderStatus.Ready)]
    [InlineData(WorkOrderStatus.Interrupted)]
    public async Task AssignEquipmentAsync_AllowedStatus_Success_AssignsAndExposesModel(WorkOrderStatus status)
    {
        var wo = AddWo(10, status);

        var result = await Service<WorkOrderService>().AssignEquipmentAsync(wo, new AssignEquipmentRequest { EquipmentId = _m1 });

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        result.Data!.Status.Should().Be(status, "assigning is not a transition");
        result.Data.AssignedEquipment.Should().BeEquivalentTo(new { Code = "M1", IsActive = true, IsEnabled = true });
        Query(db => db.WorkOrders.Single(w => w.Id == wo).AssignedEquipmentId).Should().Be(_m1);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Running)]
    [InlineData(WorkOrderStatus.Ended)]
    [InlineData(WorkOrderStatus.Aborted)]
    public async Task AssignEquipmentAsync_NotAllowedStatus_Failure_Conflict(WorkOrderStatus status)
    {
        var wo = AddWo(10, status);

        var result = await Service<WorkOrderService>().AssignEquipmentAsync(wo, new AssignEquipmentRequest { EquipmentId = _m1 });

        result.Failure.Should().Be(ProductionOrderFailure.Conflict);
    }

    [Fact]
    public async Task AssignEquipmentAsync_NullMachine_Success_RemovesAssignment()
    {
        var wo = AddWo(10, WorkOrderStatus.Ready, assigned: _m1);

        var result = await Service<WorkOrderService>().AssignEquipmentAsync(wo, new AssignEquipmentRequest());

        result.IsSuccessful.Should().BeTrue();
        result.Data!.AssignedEquipment.Should().BeNull();
    }

    [Fact]
    public async Task AssignEquipmentAsync_InvalidMachines_Failure_Invalid()
    {
        var wo = AddWo(10, WorkOrderStatus.Ready);
        var noMachines = AddWo(20, WorkOrderStatus.Ready, segment: _free);

        foreach (var (target, machine) in new[] { (wo, _old), (wo, _foreign), (wo, 404), (noMachines, _m1) })
        {
            var result = await Service<WorkOrderService>().AssignEquipmentAsync(target, new AssignEquipmentRequest { EquipmentId = machine });
            result.Failure.Should().Be(ProductionOrderFailure.Invalid, $"machine {machine}");
        }
        (await Service<WorkOrderService>().AssignEquipmentAsync(noMachines, new AssignEquipmentRequest { EquipmentId = _m1 }))
            .Errors.Should().Contain(EquipmentSelector.NoMachinesMessage);
    }

    [Fact]
    public async Task AssignEquipmentAsync_UnknownWorkOrder_Failure_NotFound()
        => (await Service<WorkOrderService>().AssignEquipmentAsync(404, new AssignEquipmentRequest { EquipmentId = _m1 }))
            .Failure.Should().Be(ProductionOrderFailure.NotFound);

    // ---- start / resume -----------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_AssignedMachineNoBody_Success_SessionTakesAssigned()
    {
        var wo = AddWo(10, WorkOrderStatus.Ready, assigned: _m1);

        var result = await Service<WorkOrderService>().StartAsync(wo);

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        var session = result.Data!.Sessions.Single();
        (session.EquipmentId, session.EquipmentCode).Should().Be((_m1, "M1"));
        result.Data.CurrentEquipment!.Code.Should().Be("M1");
    }

    [Fact]
    public async Task StartAsync_NoMachineAndPhaseHasMachines_Failure_ChooseTheMachine()
    {
        var wo = AddWo(10, WorkOrderStatus.Ready);

        var result = await Service<WorkOrderService>().StartAsync(wo);

        result.Failure.Should().Be(ProductionOrderFailure.Invalid);
        result.Errors.Should().Contain("Scegli la macchina.");
        Query(db => db.WorkOrders.Single(w => w.Id == wo).Status).Should().Be(WorkOrderStatus.Ready);
        Query(db => db.WorkOrderResponses.Count()).Should().Be(0);
    }

    [Fact]
    public async Task StartAsync_ExplicitMachine_Success_AssignmentFollows()
    {
        var wo = AddWo(10, WorkOrderStatus.Ready, assigned: _m1);

        var result = await Service<WorkOrderService>().StartAsync(wo, new OpenSessionRequest { EquipmentId = _m2 });

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Sessions.Single().EquipmentCode.Should().Be("M2");
        result.Data.AssignedEquipment!.Code.Should().Be("M2");
        Query(db => db.WorkOrders.Single(w => w.Id == wo).AssignedEquipmentId).Should().Be(_m2);
    }

    [Fact]
    public async Task StartAsync_AssignedMachineNoLongerActive_Failure_MustChooseAnother()
    {
        var wo = AddWo(10, WorkOrderStatus.Ready, assigned: _old);

        var result = await Service<WorkOrderService>().StartAsync(wo);

        result.Failure.Should().Be(ProductionOrderFailure.Invalid);
        result.Errors.Should().Contain(e => e.StartsWith("Scegli la macchina"));
        (await Service<WorkOrderService>().StartAsync(wo, new OpenSessionRequest { EquipmentId = _m1 })).IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_InvalidExplicitMachine_Failure_Invalid()
    {
        var wo = AddWo(10, WorkOrderStatus.Ready);

        foreach (var machine in new[] { _old, _foreign, 404 })
            (await Service<WorkOrderService>().StartAsync(wo, new OpenSessionRequest { EquipmentId = machine }))
                .Failure.Should().Be(ProductionOrderFailure.Invalid, $"machine {machine}");
    }

    [Fact]
    public async Task StartAsync_PhaseWithoutMachines_Success_EmptyAndMachineRejected()
    {
        var wo = AddWo(10, WorkOrderStatus.Ready, segment: _free);

        (await Service<WorkOrderService>().StartAsync(wo, new OpenSessionRequest { EquipmentId = _m1 })).Failure
            .Should().Be(ProductionOrderFailure.Invalid);
        var started = await Service<WorkOrderService>().StartAsync(wo);

        started.IsSuccessful.Should().BeTrue();
        started.Data!.Sessions.Single().EquipmentId.Should().BeNull();
    }

    [Fact]
    public async Task StartAsync_WrongStatus_Failure_ConflictBeforeMachineCheck()
    {
        var wo = AddWo(10, WorkOrderStatus.Waiting);

        (await Service<WorkOrderService>().StartAsync(wo)).Failure.Should().Be(ProductionOrderFailure.Conflict);
    }

    [Fact]
    public async Task ResumeAsync_NoBody_Success_UsesMachineOfLastSession()
    {
        var wo = AddWo(10, WorkOrderStatus.Interrupted, assigned: _m1);
        AddClosedSession(wo, _m2, "M2");

        var result = await Service<WorkOrderService>().ResumeAsync(wo);

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        result.Data!.Sessions.Last().EquipmentCode.Should().Be("M2");
        result.Data.AssignedEquipment!.Code.Should().Be("M2", "the assignment follows the machine used");
    }

    [Fact]
    public async Task ResumeAsync_ExplicitMachine_Success_OverridesLastSession()
    {
        var wo = AddWo(10, WorkOrderStatus.Interrupted, assigned: _m1);
        AddClosedSession(wo, _m1, "M1");

        var result = await Service<WorkOrderService>().ResumeAsync(wo, new OpenSessionRequest { EquipmentId = _m2 });

        result.Data!.Sessions.Last().EquipmentCode.Should().Be("M2");
    }

    [Fact]
    public async Task ResumeAsync_LastMachineNoLongerEnabled_Failure_ChooseAgain()
    {
        var wo = AddWo(10, WorkOrderStatus.Interrupted);
        AddClosedSession(wo, _old, "OLD");

        (await Service<WorkOrderService>().ResumeAsync(wo)).Failure.Should().Be(ProductionOrderFailure.Invalid);
    }

    // ---- correction ---------------------------------------------------------------------------------

    private static CorrectSessionRequest Correction(int? equipment) => new()
    {
        GoodQuantity = 2, ScrapQuantity = 0, Notes = "Macchina sbagliata", EquipmentId = equipment
    };

    [Fact]
    public async Task CorrectSessionAsync_NewMachine_Success_TraceKeepsPreviousAndNew()
    {
        var wo = AddWo(10, WorkOrderStatus.Interrupted, assigned: _m1);
        var session = AddClosedSession(wo, _m1, "M1");

        var result = await Service<WorkOrderService>().CorrectSessionAsync(wo, session, Correction(_m2));

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        var s = result.Data!.Sessions.Single();
        s.EquipmentCode.Should().Be("M2");
        var trace = s.Corrections.Single();
        (trace.PreviousEquipmentCode, trace.NewEquipmentCode).Should().Be(("M1", "M2"));
    }

    [Fact]
    public async Task CorrectSessionAsync_NoMachineInRequest_Success_MachineUnchanged()
    {
        var wo = AddWo(10, WorkOrderStatus.Interrupted);
        var session = AddClosedSession(wo, _old, "OLD");

        var result = await Service<WorkOrderService>().CorrectSessionAsync(wo, session, Correction(null));

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Sessions.Single().EquipmentCode.Should().Be("OLD");
        result.Data.Sessions.Single().Corrections.Single().NewEquipmentCode.Should().Be("OLD");
    }

    [Fact]
    public async Task CorrectSessionAsync_InvalidMachine_Failure_InvalidAndNothingChanged()
    {
        var wo = AddWo(10, WorkOrderStatus.Interrupted);
        var session = AddClosedSession(wo, _m1, "M1");

        foreach (var machine in new[] { _old, _foreign })
            (await Service<WorkOrderService>().CorrectSessionAsync(wo, session, Correction(machine))).Failure
                .Should().Be(ProductionOrderFailure.Invalid);
        Query(db => db.WorkOrderResponses.Single().EquipmentId).Should().Be(_m1);
        Query(db => db.WorkOrderResponseCorrections.Count()).Should().Be(0);
    }

    // ---- list filter --------------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_EquipmentFilter_Success_OnlyAssignedToThatMachine()
    {
        var a = AddWo(10, WorkOrderStatus.Ready, assigned: _m1);
        AddWo(20, WorkOrderStatus.Ready, assigned: _m2);
        AddWo(30, WorkOrderStatus.Ready);

        var result = await Service<WorkOrderService>().GetAllAsync(null, null, null, _m1);

        result.Data!.Select(w => w.Id).Should().Equal(a);
    }
}
