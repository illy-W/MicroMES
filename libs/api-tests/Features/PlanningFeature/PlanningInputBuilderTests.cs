using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.CalendarFeature.Services;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.PlanningFeature.Services;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

public class PlanningInputBuilderTests
{
    // "Now" = Monday 5 January 2026, 08:00 local (CET).
    private static readonly DateTime Now = Local(2026, 1, 5, 8);
    private readonly ProductionData _data = new(new FakeTimeProvider(new DateTimeOffset(Now)));
    private readonly Dictionary<int, Equipment> _machines = [];
    private readonly List<EquipmentProcessSegment> _links = [];
    private int _dep;

    public PlanningInputBuilderTests()
    {
        Machine(1); Machine(2);
        Link(1, segment: 1, isDefault: true); Link(2, segment: 1);
    }

    private void Machine(int id, bool active = true, int capacity = 1, decimal efficiency = 100m)
        => _machines[id] = new Equipment { Id = id, Code = $"M{id}", Name = $"M{id}", IsActive = active, Capacity = capacity, EfficiencyPercent = efficiency };

    private void Link(int machine, int segment, bool isDefault = false)
        => _links.Add(new EquipmentProcessSegment { Id = _links.Count + 1, EquipmentId = machine, ProcessSegmentId = segment, IsDefault = isDefault });

    private void Order(int id, ProductionOrderStatus status = ProductionOrderStatus.Released, int? parent = null, int priority = 0, DateTime? due = null)
    {
        _data.Materials[id] = new Material { Id = id, Code = $"M{id}", Name = $"Mat {id}" };
        _data.Orders[id] = new ProductionOrder
        {
            Id = id, Code = $"OP-{id}", MaterialId = id, Quantity = 1, ParentOrderId = parent, Status = status, Priority = priority, RequestedEnd = due
        };
    }

    private WorkOrder Wo(int id, int order, decimal minutes, WorkOrderStatus status = WorkOrderStatus.Waiting, int segment = 1,
        int? machine = null, DateTime? plannedStart = null, DateTime? plannedEnd = null, DateTime? endedAt = null)
        => _data.WorkOrders[id] = new WorkOrder
        {
            Id = id, ProductionOrderId = order, OperationNumber = id, ProcessSegmentId = segment, SetupMinutes = minutes, PlannedQuantity = 1,
            Status = status, AssignedEquipmentId = machine, PlannedStartAt = plannedStart, PlannedEndAt = plannedEnd, EndedAt = endedAt
        };

    private void Dep(int pred, int succ)
        => _data.Dependencies.Add(new WorkOrderDependency { Id = ++_dep, PredecessorWorkOrderId = pred, SuccessorWorkOrderId = succ });

    private PlanningBuild Build(PlanningScope scope, int? orderId = null)
    {
        var calendars = new Dictionary<int, WorkCalendar> { [1] = new() { Id = 1, Code = "STANDARD", IsDefault = true, IsActive = true } };
        var directory = new AvailabilityDirectory(Rome, calendars, new() { [1] = Standard() }, [], _machines, []);
        var catalog = new PlanningCatalog(_machines, _links);
        return PlanningInputBuilder.Build(_data, catalog, directory, scope, orderId, Now);
    }

    private static readonly DateTime P1 = Local(2026, 1, 5, 10);
    private static readonly DateTime P2 = Local(2026, 1, 5, 12);

    // ---- scopes ---------------------------------------------------------------------------------

    [Fact]
    public void Build_AllScope_PlansEveryOpenWorkOrderOfOpenOrders()
    {
        Order(1); Order(2); Order(3, ProductionOrderStatus.Planned); Order(4, ProductionOrderStatus.Completed);
        Wo(11, 1, 60); Wo(21, 2, 60, plannedStart: P1, plannedEnd: P2); Wo(31, 3, 60); Wo(41, 4, 60, WorkOrderStatus.Ended);

        var build = Build(PlanningScope.All);

        build.ScopeWorkOrderIds.Should().BeEquivalentTo([11, 21]);
        build.Input.Jobs.Select(j => j.WorkOrderId).Should().BeEquivalentTo([11, 21]);
    }

    [Fact]
    public void Build_UnplannedScope_PlansOnlyJobsWithoutPlan_AndTheOthersOccupyTheirMachine()
    {
        Order(1);
        Wo(11, 1, 60);
        Wo(12, 1, 60, machine: 2, plannedStart: P1, plannedEnd: P2);

        var build = Build(PlanningScope.Unplanned);

        build.ScopeWorkOrderIds.Should().BeEquivalentTo([11]);
        build.Input.Jobs.Select(j => j.WorkOrderId).Should().BeEquivalentTo([11]);
        build.Input.Equipment.Single(e => e.Id == 2).BusyIntervals.Should().Equal(new MicroMES.ApiFeatures.PlanningFeature.Engine.PlanningInterval(P1, P2));
        build.Input.Equipment.Single(e => e.Id == 1).BusyIntervals.Should().BeEmpty();
    }

    [Fact]
    public void Build_OrderScope_PlansTheOrderAndItsChildren_AndKeepsOtherPlansAsOccupancy()
    {
        Order(1); Order(2, parent: 1); Order(3);
        Wo(11, 1, 60, plannedStart: P1, plannedEnd: P2);      // in scope: re-planned although it has a plan
        Wo(21, 2, 60);
        Wo(31, 3, 60, machine: 1, plannedStart: P1, plannedEnd: P2); // other order: occupies M1
        Wo(32, 3, 60);                                               // other order, no plan: ignored

        var build = Build(PlanningScope.Order, 1);

        build.ScopeWorkOrderIds.Should().BeEquivalentTo([11, 21]);
        build.Input.Jobs.Select(j => j.WorkOrderId).Should().BeEquivalentTo([11, 21]);
        build.Input.Equipment.Single(e => e.Id == 1).BusyIntervals.Should().ContainSingle();
    }

    [Fact]
    public void Build_OrderScopeOfAChild_DoesNotIncludeTheParent()
    {
        Order(1); Order(2, parent: 1);
        Wo(11, 1, 60); Wo(21, 2, 60);
        Build(PlanningScope.Order, 2).ScopeWorkOrderIds.Should().BeEquivalentTo([21]);
    }

    // ---- fixed jobs ------------------------------------------------------------------------------

    [Fact]
    public void Build_RunningWorkOrder_IsFixedWithRealStartMachineAndRemainingMinutes()
    {
        Order(1);
        Wo(11, 1, 120, WorkOrderStatus.Running, machine: 1);
        _data.Sessions.Add(new WorkOrderResponse { Id = 1, WorkOrderId = 11, StartedAt = Now.AddMinutes(-30), EquipmentId = 2 });
        Wo(12, 1, 60); Dep(11, 12);

        var build = Build(PlanningScope.All);

        var fixedJob = build.Input.Jobs.Single(j => j.WorkOrderId == 11);
        fixedJob.IsFixed.Should().BeTrue();
        fixedJob.FixedStart.Should().Be(Now.AddMinutes(-30));
        fixedJob.AssignedEquipmentId.Should().Be(2); // the machine of the open session wins
        fixedJob.StandardMinutes.Should().Be(90);    // 120 - 30 minutes of the open session
        build.ScopeWorkOrderIds.Should().BeEquivalentTo([12]);
        build.Input.Jobs.Single(j => j.WorkOrderId == 12).PredecessorIds.Should().Equal(11);
    }

    [Fact]
    public void Build_RunningWorkOrderOutsideTheScope_IsStillFixed()
    {
        Order(1); Order(2);
        Wo(11, 1, 60); Wo(21, 2, 60, WorkOrderStatus.Running, machine: 1);
        _data.Sessions.Add(new WorkOrderResponse { Id = 1, WorkOrderId = 21, StartedAt = Now.AddMinutes(-10), EquipmentId = 1 });

        var build = Build(PlanningScope.Order, 1);

        build.Input.Jobs.Single(j => j.WorkOrderId == 21).IsFixed.Should().BeTrue();
        build.ScopeWorkOrderIds.Should().BeEquivalentTo([11]);
    }

    [Fact]
    public void Build_InterruptedWorkOrder_CountsOnlyTheMinutesLeft()
    {
        Order(1);
        Wo(11, 1, 100, WorkOrderStatus.Interrupted);
        _data.Sessions.Add(new WorkOrderResponse { Id = 1, WorkOrderId = 11, StartedAt = Now.AddHours(-3), EndedAt = Now.AddHours(-3).AddMinutes(40) });
        Build(PlanningScope.All).Input.Jobs.Single().StandardMinutes.Should().Be(60);
    }

    // ---- predecessors ----------------------------------------------------------------------------

    [Fact]
    public void Build_EndedPredecessor_GivesClosedPredecessorsEndFromItsRealEnd()
    {
        Order(1);
        var end = Now.AddHours(-2);
        Wo(11, 1, 60, WorkOrderStatus.Ended, endedAt: end);
        Wo(12, 1, 60, WorkOrderStatus.Ended, endedAt: end.AddHours(-1));
        Wo(13, 1, 60); Dep(11, 13); Dep(12, 13);

        var job = Build(PlanningScope.All).Input.Jobs.Single();
        job.ClosedPredecessorsEnd.Should().Be(end);
        job.PredecessorIds.Should().BeEmpty();
    }

    [Fact]
    public void Build_PredecessorPlannedOutOfScope_ConstrainsWithItsPlannedEnd_AndUnplannedOneDoesNot()
    {
        Order(1); Order(2);
        Wo(21, 2, 60, plannedStart: P1, plannedEnd: P2);
        Wo(22, 2, 60);
        Wo(11, 1, 60); Wo(12, 1, 60);
        Dep(21, 11); Dep(22, 12);

        var jobs = Build(PlanningScope.Order, 1).Input.Jobs;
        jobs.Single(j => j.WorkOrderId == 11).ClosedPredecessorsEnd.Should().Be(P2);
        jobs.Single(j => j.WorkOrderId == 12).ClosedPredecessorsEnd.Should().BeNull();
    }

    [Fact]
    public void Build_PredecessorInScope_IsAnEdgeNotAClosedEnd()
    {
        Order(1);
        Wo(11, 1, 60); Wo(12, 1, 60); Dep(11, 12);
        var j = Build(PlanningScope.All).Input.Jobs.Single(x => x.WorkOrderId == 12);
        j.PredecessorIds.Should().Equal(11);
        j.ClosedPredecessorsEnd.Should().BeNull();
    }

    // ---- machines --------------------------------------------------------------------------------

    [Fact]
    public void Build_Candidates_AreActiveLinkedMachines_WithTheDefaultOfTheSegment()
    {
        Machine(3, active: false); Link(3, 1);
        Order(1, priority: 4, due: P2);
        Wo(11, 1, 60, machine: 2);

        var job = Build(PlanningScope.All).Input.Jobs.Single();
        job.CandidateEquipmentIds.Should().Equal(1, 2);
        job.DefaultEquipmentId.Should().Be(1);
        job.AssignedEquipmentId.Should().Be(2);
        job.PhaseHasMachines.Should().BeTrue();
        job.Priority.Should().Be(4);
        job.RequestedEnd.Should().Be(P2);
        job.OrderCode.Should().Be("OP-1");
    }

    [Fact]
    public void Build_SegmentWithoutLinks_HasNoMachines_AndSegmentWithOnlyInactiveOnesHasMachinesButNoCandidate()
    {
        Machine(3, active: false); Link(3, 3);
        Order(1);
        Wo(11, 1, 60, segment: 2);
        Wo(12, 1, 60, segment: 3);

        var jobs = Build(PlanningScope.All).Input.Jobs;
        var none = jobs.Single(j => j.WorkOrderId == 11);
        none.PhaseHasMachines.Should().BeFalse();
        none.CandidateEquipmentIds.Should().BeEmpty();
        var inactive = jobs.Single(j => j.WorkOrderId == 12);
        inactive.PhaseHasMachines.Should().BeTrue();
        inactive.CandidateEquipmentIds.Should().BeEmpty();
    }

    [Fact]
    public void Build_Equipment_CarriesCapacityEfficiencyAndEffectiveAvailability()
    {
        Machine(1, capacity: 3, efficiency: 80m);
        Order(1); Wo(11, 1, 60);
        var m = Build(PlanningScope.All).Input.Equipment.Single(e => e.Id == 1);
        m.Capacity.Should().Be(3);
        m.EfficiencyPercent.Should().Be(80m);
        m.Availability.WeeklySlots.Should().HaveCount(10);
    }

    [Fact]
    public void Build_InputCarriesNowZoneAndDefaultCalendar()
    {
        Order(1); Wo(11, 1, 60);
        var input = Build(PlanningScope.All).Input;
        input.Now.Should().Be(Now);
        input.Zone.Id.Should().Be(Rome.Id);
        input.DefaultAvailability.WeeklySlots.Should().HaveCount(10);
    }
}
