using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.CalendarFeature.Services;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;

namespace MicroMES.ApiTests.Features.ProductionOrderFeature;

public class ProductionEndEstimatorFixture
{
    // "Now" = Monday 5 January 2026, 08:00 local (CET).
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(Local(2026, 1, 5, 8)));
    private readonly ProductionData _data;
    private readonly Dictionary<int, Equipment> _equipment = [];
    private readonly Dictionary<int, List<AvailabilityException>> _calendarExceptions = [];
    private int _dep;

    public ProductionEndEstimatorFixture()
    {
        _data = new ProductionData(_clock);
        Machine(1, efficiency: 100, calendarId: null);   // default calendar
        Machine(2, efficiency: 50, calendarId: null);    // half speed
        Machine(3, efficiency: 100, calendarId: 2);      // double shift
    }

    private void Machine(int id, decimal efficiency, int? calendarId)
        => _equipment[id] = new Equipment { Id = id, Code = $"M{id}", Name = $"M{id}", EfficiencyPercent = efficiency, WorkCalendarId = calendarId };

    private void Build()
    {
        var calendars = new Dictionary<int, WorkCalendar>
        {
            [1] = new() { Id = 1, Code = "STANDARD", IsDefault = true, IsActive = true },
            [2] = new() { Id = 2, Code = "DOPPIO", IsActive = true }
        };
        var weekly = new Dictionary<int, List<WeeklySlot>>
        {
            [1] = Standard(),
            [2] = Weekdays.SelectMany(d => new[] { new WeeklySlot(d, T(6), T(14)), new WeeklySlot(d, T(14), T(22)) }).ToList()
        };
        _data.Availability = new AvailabilityDirectory(Rome, calendars, weekly, _calendarExceptions, _equipment, []);
    }

    private void Order(int id, int? parent = null, ProductionOrderStatus status = ProductionOrderStatus.Released)
    {
        _data.Materials[id] = new Material { Id = id, Code = $"M{id}", Name = $"Mat {id}" };
        _data.Orders[id] = new ProductionOrder { Id = id, Code = $"OP-{id}", MaterialId = id, Quantity = 1, ParentOrderId = parent, Status = status };
    }

    /// <summary>Work order whose planned minutes are the setup (quantity 1, run 0).</summary>
    private void Wo(int id, int order, decimal minutes, WorkOrderStatus status = WorkOrderStatus.Waiting, int? machine = null)
        => _data.WorkOrders[id] = new WorkOrder
        {
            Id = id, ProductionOrderId = order, OperationNumber = id, SetupMinutes = minutes, RunMinutesPerUnit = 0,
            PlannedQuantity = 1, Status = status, AssignedEquipmentId = machine
        };

    private void Dep(int pred, int succ)
        => _data.Dependencies.Add(new WorkOrderDependency { Id = ++_dep, PredecessorWorkOrderId = pred, SuccessorWorkOrderId = succ });

    private void Session(int id, int woId, DateTime start, DateTime? end, int? machine = null)
        => _data.Sessions.Add(new WorkOrderResponse { Id = id, WorkOrderId = woId, StartedAt = start, EndedAt = end, EquipmentId = machine });

    private DateTime? Estimate(int orderId)
    {
        Build();
        return ProductionEndEstimator.Estimate(_data, orderId);
    }

    [Fact]
    public void Estimate_SimpleChainOnDefaultCalendar_Success_SumsMinutesOverPauses()
    {
        Order(1);
        Wo(11, 1, 120); Wo(12, 1, 180); Dep(11, 12);
        // 08:00 -> 10:00, then 3 h: 10-12 + 13-14.
        Estimate(1).Should().Be(Local(2026, 1, 5, 14));
    }

    [Fact]
    public void Estimate_ParallelBranches_Success_LongestBranchWins()
    {
        Order(1);
        Wo(11, 1, 60); Wo(12, 1, 300); Wo(13, 1, 60);
        Dep(11, 13); Dep(12, 13);
        // 12 ends 08:00+300min = Mon 14:00 (pause); then 13 = 15:00.
        Estimate(1).Should().Be(Local(2026, 1, 5, 15));
    }

    [Fact]
    public void Estimate_TwoCalendars_Success_PerMachineCalendarUsed()
    {
        Order(1);
        Wo(11, 1, 960, machine: 3);   // double shift: 6-14,14-22 -> 16 h from 08:00 = Tue 06:00 +2h... see below
        Wo(12, 1, 60, machine: 1); Dep(11, 12);
        // Monday 08:00-22:00 = 840 min on the double shift, remaining 120 -> Tuesday 08:00. Then default calendar: Tuesday 08:00 + 60.
        Estimate(1).Should().Be(Local(2026, 1, 6, 9));
    }

    [Fact]
    public void Estimate_EfficiencyBelow100_Success_LongerThanStandard()
    {
        Order(1);
        Wo(11, 1, 60, machine: 2); // 50% -> 120 real minutes
        Estimate(1).Should().Be(Local(2026, 1, 5, 10));
    }

    [Fact]
    public void Estimate_NoMachine_Success_DefaultCalendarAndFullEfficiency()
    {
        Order(1);
        Wo(11, 1, 60);
        Estimate(1).Should().Be(Local(2026, 1, 5, 9));
    }

    [Fact]
    public void Estimate_StoredPlannedMinutesNeverRewritten_Success()
    {
        Order(1);
        Wo(11, 1, 60, machine: 2);
        Estimate(1);
        _data.WorkOrders[11].SetupMinutes.Should().Be(60);
        ProductionData.PlannedMinutes(_data.WorkOrders[11]).Should().Be(60);
    }

    [Fact]
    public void Estimate_NowOutsideWorkingTime_Success_StartsAtNextWorkingInstant()
    {
        _clock.Advance(TimeSpan.FromHours(10)); // Monday 18:00 local
        Order(1);
        Wo(11, 1, 60);
        Estimate(1).Should().Be(Local(2026, 1, 6, 9));
    }

    [Fact]
    public void Estimate_OrderWithChildren_Success_ChildWorkGatesParent()
    {
        Order(1); Order(2, parent: 1);
        Wo(11, 1, 60); Wo(21, 2, 120);
        Dep(21, 11); // child last step -> parent start step
        Estimate(1).Should().Be(Local(2026, 1, 5, 11)); // 2 h + 1 h
        Estimate(2).Should().Be(Local(2026, 1, 5, 10), "a child order estimates only its own subtree");
    }

    [Fact]
    public void Estimate_RunningWorkOrder_Success_OnlyRemainingTimeFromNow()
    {
        Order(1, status: ProductionOrderStatus.InProgress);
        Wo(11, 1, 120, WorkOrderStatus.Running, machine: 1);
        Wo(12, 1, 60); Dep(11, 12);
        // Started 30 min ago: 90 min left from 08:00 -> 09:30, then 1 h -> 10:30.
        Session(1, 11, Local(2026, 1, 5, 7, 30), null, machine: 1);
        Estimate(1).Should().Be(Local(2026, 1, 5, 10, 30));
    }

    [Fact]
    public void Estimate_InterruptedWithClosedSession_Success_SubtractsWorkedMinutes()
    {
        Order(1, status: ProductionOrderStatus.InProgress);
        Wo(11, 1, 120, WorkOrderStatus.Interrupted, machine: 1);
        Session(1, 11, Local(2026, 1, 2, 9), Local(2026, 1, 2, 9, 45), machine: 1);
        Estimate(1).Should().Be(Local(2026, 1, 5, 9, 15)); // 75 min left
    }

    [Fact]
    public void Estimate_RunningOverPlanned_Success_NeverNegative()
    {
        Order(1, status: ProductionOrderStatus.InProgress);
        Wo(11, 1, 30, WorkOrderStatus.Running, machine: 1);
        Session(1, 11, Local(2026, 1, 2, 9), null, machine: 1);
        Estimate(1).Should().Be(Local(2026, 1, 5, 8));
    }

    [Fact]
    public void Estimate_EndedPredecessors_Success_IgnoredAndOnlyOpenWorkCounts()
    {
        Order(1, status: ProductionOrderStatus.InProgress);
        Wo(11, 1, 600, WorkOrderStatus.Ended); Wo(12, 1, 60, WorkOrderStatus.Ready); Wo(13, 1, 600, WorkOrderStatus.Aborted);
        Dep(11, 12);
        Estimate(1).Should().Be(Local(2026, 1, 5, 9));
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Planned)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Cancelled)]
    [InlineData(ProductionOrderStatus.Aborted)]
    public void Estimate_NotOpenOrder_Success_Null(ProductionOrderStatus status)
    {
        Order(1, status: status);
        Wo(11, 1, 60);
        Estimate(1).Should().BeNull();
    }

    [Fact]
    public void Estimate_NoWorkOrders_Success_Null()
    {
        Order(1);
        Estimate(1).Should().BeNull();
    }

    [Fact]
    public void Estimate_AllWorkOrdersEnded_Success_Null()
    {
        Order(1, status: ProductionOrderStatus.InProgress);
        Wo(11, 1, 60, WorkOrderStatus.Ended);
        Estimate(1).Should().BeNull();
    }

    [Fact]
    public void Estimate_CompanyHolidayInTheWay_Success_SkipsIt()
    {
        _calendarExceptions[1] = [Closed(D(2026, 1, 6), D(2026, 1, 9))];
        Order(1);
        Wo(11, 1, 600); // 10 h: Mon 8h, then Mon->Fri closed, Mon 12th
        Estimate(1).Should().Be(Local(2026, 1, 12, 10));
    }

    [Fact]
    public void Estimate_CalendarWithoutWorkingTime_Success_Null()
    {
        _calendarExceptions[1] = [Closed(D(2026, 1, 1), D(2035, 12, 31))];
        Order(1);
        Wo(11, 1, 60);
        Estimate(1).Should().BeNull();
    }

    [Fact]
    public void Estimate_WithoutAvailability_Success_Null()
    {
        Order(1);
        Wo(11, 1, 60);
        ProductionEndEstimator.Estimate(_data, 1).Should().BeNull();
    }

    [Fact]
    public void ToOrderDetailAndOverview_OpenOrder_Success_ExposeTheSameEstimate()
    {
        Order(1);
        Wo(11, 1, 60);
        Build();

        _data.ToOrderDetail(_data.Orders[1]).EstimatedEnd.Should().Be(Local(2026, 1, 5, 9));
        _data.ToOverview(1).Summary.EstimatedEnd.Should().Be(Local(2026, 1, 5, 9));
    }
}
