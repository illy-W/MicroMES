using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.PlanningFeature.Services;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;
using MicroMES.ApiFeatures.ProductionOrderFeature.Seeding;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

/// <summary>Preview, apply, removal and alerts of the plan on in-memory SQLite, with a controlled clock (Monday 5 Jan 2026, 08:00 Rome).</summary>
public class PlanningServiceFixture : BaseFeatureFixture
{
    private static readonly DateTime Monday8 = Local(2026, 1, 5, 8);
    private int _segment, _material, _m1, _m2, _order, _wo1, _wo2;

    public PlanningServiceFixture()
    {
        Service<OverridableTimeProvider>().Set(Monday8);
        _material = AddMaterial("PF");
        _segment = AddSegment("SEG");
        Arrange(db =>
        {
            var now = DateTime.UtcNow;
            var calendar = new WorkCalendar { Code = "STANDARD", Name = "Standard", IsActive = true, IsDefault = true, CreatedAt = now, UpdatedAt = now };
            db.WorkCalendars.Add(calendar);
            db.SaveChanges();
            foreach (var s in Standard())
                db.WorkCalendarSlots.Add(new WorkCalendarSlot { WorkCalendarId = calendar.Id, DayOfWeek = s.DayOfWeek, StartTime = s.Start, EndTime = s.End });
            var m1 = new Equipment { Code = "M1", Name = "M1", IsActive = true, Capacity = 1, EfficiencyPercent = 100m, CreatedAt = now, UpdatedAt = now };
            var m2 = new Equipment { Code = "M2", Name = "M2", IsActive = true, Capacity = 1, EfficiencyPercent = 100m, CreatedAt = now, UpdatedAt = now };
            db.Equipment.AddRange(m1, m2);
            db.SaveChanges();
            db.EquipmentProcessSegments.Add(new EquipmentProcessSegment { EquipmentId = m1.Id, ProcessSegmentId = _segment, IsDefault = true });
            db.EquipmentProcessSegments.Add(new EquipmentProcessSegment { EquipmentId = m2.Id, ProcessSegmentId = _segment });
            _m1 = m1.Id;
            _m2 = m2.Id;
        });
        (_order, _wo1, _wo2) = Order("OP-1", ProductionOrderStatus.Released, minutes1: 120, minutes2: 60);
    }

    /// <summary>An order with two chained work orders (120 and 60 minutes by default).</summary>
    private (int Order, int Wo1, int Wo2) Order(string code, ProductionOrderStatus status, decimal minutes1 = 120, decimal minutes2 = 60,
        DateTime? due = null, int priority = 0, int? segment = null)
    {
        int order = 0, w1 = 0, w2 = 0;
        Arrange(db =>
        {
            var now = DateTime.UtcNow;
            var o = new ProductionOrder
            {
                Code = code, MaterialId = _material, Quantity = 1, Status = status, Priority = priority, RequestedEnd = due, CreatedAt = now, UpdatedAt = now
            };
            db.ProductionOrders.Add(o);
            db.SaveChanges();
            WorkOrder Make(int op, decimal minutes) => new()
            {
                ProductionOrderId = o.Id, OperationNumber = op, ProcessSegmentId = segment ?? _segment, ProcessSegmentCode = "SEG", ProcessSegmentName = "SEG",
                SetupMinutes = minutes, PlannedQuantity = 1, Status = WorkOrderStatus.Waiting, CreatedAt = now, UpdatedAt = now
            };
            var a = Make(10, minutes1);
            var b = Make(20, minutes2);
            db.WorkOrders.AddRange(a, b);
            db.SaveChanges();
            db.WorkOrderDependencies.Add(new WorkOrderDependency { PredecessorWorkOrderId = a.Id, SuccessorWorkOrderId = b.Id });
            order = o.Id; w1 = a.Id; w2 = b.Id;
        });
        return (order, w1, w2);
    }

    private WorkOrder Row(int id) => Query(db => db.WorkOrders.Single(w => w.Id == id));

    private static PlanningRequest Req(PlanningScope scope, int? order = null, string? token = null)
        => new() { Scope = scope, ProductionOrderId = order, PreviewToken = token };

    // ---- preview ----------------------------------------------------------------------------------

    [Fact]
    public async Task Preview_Order_Success_ProposesTheChainWithoutWritingAnything()
    {
        var result = await Service<PlanningService>().PreviewAsync(Req(PlanningScope.Order, _order));

        result.IsSuccessful.Should().BeTrue();
        var p = result.Data!;
        p.Items.Should().HaveCount(2);
        p.Items[0].ProposedStart.Should().Be(Monday8);
        p.Items[0].ProposedEnd.Should().Be(Local(2026, 1, 5, 10));
        p.Items[1].ProposedStart.Should().Be(Local(2026, 1, 5, 10));
        p.Items[1].ProposedEnd.Should().Be(Local(2026, 1, 5, 11));
        p.Items.Should().OnlyContain(i => i.Change == PlanChangeKind.New && i.ProposedEquipment!.Code == "M1" && i.AssignsEquipment);
        p.Items[1].Reasons.Should().Contain(r => r.Code == MicroMES.ApiFeatures.PlanningFeature.Engine.PlanningReasonCode.WaitsForPredecessor && r.RelatedWorkOrderId == _wo1);
        p.Summary.Considered.Should().Be(2);
        p.Summary.Planned.Should().Be(2);
        p.Summary.New.Should().Be(2);
        p.Summary.Unplanned.Should().Be(0);
        p.Summary.LastEnd.Should().Be(Local(2026, 1, 5, 11));
        p.Token.Should().NotBeNullOrEmpty();

        Row(_wo1).PlannedStartAt.Should().BeNull();
        Row(_wo1).AssignedEquipmentId.Should().BeNull();
        Row(_wo2).PlannedAt.Should().BeNull();
    }

    [Fact]
    public async Task Preview_SameInputTwice_Success_SameToken()
    {
        var a = (await Service<PlanningService>().PreviewAsync(Req(PlanningScope.All))).Data!;
        var b = (await Service<PlanningService>().PreviewAsync(Req(PlanningScope.All))).Data!;
        b.Token.Should().Be(a.Token);
    }

    [Fact]
    public async Task Preview_LateOrder_Success_ReportsDaysAndSummary()
    {
        var (late, _, _) = Order("OP-LATE", ProductionOrderStatus.Released, due: Monday8.AddHours(1));
        var p = (await Service<PlanningService>().PreviewAsync(Req(PlanningScope.Order, late))).Data!;

        p.Items.Should().OnlyContain(i => i.IsLate && i.LateDays >= 1);
        p.Summary.LateOrders.Should().Be(1);
        p.Summary.MaxLateDays.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Preview_OrderWithoutId_Invalid()
    {
        var result = await Service<PlanningService>().PreviewAsync(Req(PlanningScope.Order));
        result.IsSuccessful.Should().BeFalse();
        result.Failure.Should().Be(ProductionOrderFailure.Invalid);
        result.Errors.Should().Contain(e => e.Contains("productionOrderId"));
    }

    [Fact]
    public async Task Preview_UnknownOrder_NotFound()
        => (await Service<PlanningService>().PreviewAsync(Req(PlanningScope.Order, 9999))).Failure.Should().Be(ProductionOrderFailure.NotFound);

    [Theory]
    [InlineData(ProductionOrderStatus.Planned, "non ancora rilasciato")]
    [InlineData(ProductionOrderStatus.Cancelled, "annullato")]
    [InlineData(ProductionOrderStatus.Completed, "completato")]
    [InlineData(ProductionOrderStatus.Aborted, "interrotto")]
    public async Task Preview_OrderNotOpen_Conflict_ExplainsWhy(ProductionOrderStatus status, string text)
    {
        var (id, _, _) = Order("OP-X", status);
        var result = await Service<PlanningService>().PreviewAsync(Req(PlanningScope.Order, id));
        result.Failure.Should().Be(ProductionOrderFailure.Conflict);
        result.Errors.Should().Contain(e => e.Contains("OP-X") && e.Contains(text));
        (await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, id))).Failure.Should().Be(ProductionOrderFailure.Conflict);
        Row(_wo1).PlannedStartAt.Should().BeNull();
    }

    [Fact]
    public async Task Preview_AllScope_Success_IgnoresOrdersNotOpen()
    {
        Order("OP-PLANNED", ProductionOrderStatus.Planned);
        var p = (await Service<PlanningService>().PreviewAsync(Req(PlanningScope.All))).Data!;
        p.Items.Select(i => i.ProductionOrderCode).Distinct().Should().Equal("OP-1");
    }

    // ---- apply ------------------------------------------------------------------------------------

    [Fact]
    public async Task Apply_Order_Success_WritesDatesPlannedAtAndMachine()
    {
        var result = await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, _order));

        result.IsSuccessful.Should().BeTrue();
        result.Data!.DiffersFromPreview.Should().BeNull();
        result.Data.Summary.Planned.Should().Be(2);
        var w1 = Row(_wo1);
        w1.PlannedStartAt.Should().Be(Monday8);
        w1.PlannedEndAt.Should().Be(Local(2026, 1, 5, 10));
        w1.PlannedAt.Should().Be(Monday8);
        w1.AssignedEquipmentId.Should().Be(_m1);
        Row(_wo2).PlannedStartAt.Should().Be(Local(2026, 1, 5, 10));
        Row(_wo2).AssignedEquipmentId.Should().Be(_m1);
    }

    [Fact]
    public async Task Apply_AssignedMachine_Success_IsKeptAndNotReassigned()
    {
        Arrange(db => db.WorkOrders.Single(w => w.Id == _wo2).AssignedEquipmentId = _m2);
        var result = await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, _order));

        var item = result.Data!.Items.Single(i => i.WorkOrderId == _wo2);
        item.ProposedEquipment!.Code.Should().Be("M2");
        item.AssignsEquipment.Should().BeFalse();
        Row(_wo2).AssignedEquipmentId.Should().Be(_m2);
    }

    [Fact]
    public async Task Apply_AssignedMachineNoLongerValid_Success_ReplacedByAnEnabledOne()
    {
        Arrange(db =>
        {
            db.Equipment.Single(e => e.Id == _m2).IsActive = false;
            db.WorkOrders.Single(w => w.Id == _wo1).AssignedEquipmentId = _m2;
        });
        var result = await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, _order));

        var item = result.Data!.Items.Single(i => i.WorkOrderId == _wo1);
        item.AssignsEquipment.Should().BeTrue();
        item.Reasons.Should().Contain(r => r.Code == MicroMES.ApiFeatures.PlanningFeature.Engine.PlanningReasonCode.AssignedMachineNotEnabled);
        Row(_wo1).AssignedEquipmentId.Should().Be(_m1);
    }

    [Fact]
    public async Task Apply_WithPreviewToken_Success_FlagsWhetherThePlanDiffers()
    {
        var preview = (await Service<PlanningService>().PreviewAsync(Req(PlanningScope.All))).Data!;

        var same = await Service<PlanningService>().ApplyAsync(Req(PlanningScope.All, token: preview.Token));
        same.Data!.DiffersFromPreview.Should().BeFalse();
        same.Data.Token.Should().Be(preview.Token);

        // The clock moves: the new plan starts later than the one that was previewed.
        Service<OverridableTimeProvider>().Set(Monday8.AddHours(1));
        var changed = await Service<PlanningService>().ApplyAsync(Req(PlanningScope.All, token: preview.Token));
        changed.Data!.DiffersFromPreview.Should().BeTrue();
        Row(_wo1).PlannedStartAt.Should().Be(Monday8.AddHours(1));
    }

    [Fact]
    public async Task Preview_AfterApplySameClock_Success_AllUnchanged()
    {
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.All));
        var p = (await Service<PlanningService>().PreviewAsync(Req(PlanningScope.All))).Data!;
        p.Items.Should().OnlyContain(i => i.Change == PlanChangeKind.Unchanged);
        p.Summary.Unchanged.Should().Be(2);
        p.Items[0].CurrentStart.Should().Be(p.Items[0].ProposedStart);
    }

    [Fact]
    public async Task Preview_AfterApplyLaterClock_Success_ShowsMovedWithCurrentValues()
    {
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.All));
        Service<OverridableTimeProvider>().Set(Monday8.AddHours(2));
        var p = (await Service<PlanningService>().PreviewAsync(Req(PlanningScope.All))).Data!;
        p.Items.Should().OnlyContain(i => i.Change == PlanChangeKind.Moved);
        p.Items[0].CurrentStart.Should().Be(Monday8);
        p.Items[0].ProposedStart.Should().Be(Monday8.AddHours(2));
    }

    [Fact]
    public async Task Apply_UnplannedScope_Success_KeepsExistingPlansAsOccupancy()
    {
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, _order)); // OP-1 on M1 from 08:00 to 11:00
        var (other, o1, _) = Order("OP-2", ProductionOrderStatus.Released, minutes1: 60, minutes2: 30);

        var p = (await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Unplanned))).Data!;

        p.Items.Select(i => i.ProductionOrderCode).Distinct().Should().Equal("OP-2");
        // M2 is free at 08:00: it finishes first; OP-1 is untouched.
        p.Items.First(i => i.WorkOrderId == o1).ProposedEquipment!.Code.Should().Be("M2");
        Row(_wo1).PlannedStartAt.Should().Be(Monday8);
        Row(o1).PlannedStartAt.Should().Be(Monday8);
        other.Should().BePositive();
    }

    [Fact]
    public async Task Apply_AllScope_Success_ReplansAndQueuesOnTheSharedMachine()
    {
        Order("OP-2", ProductionOrderStatus.Released, minutes1: 60, minutes2: 30);
        Arrange(db => db.WorkOrders.Where(w => w.ProcessSegmentId == _segment).ToList().ForEach(w => w.AssignedEquipmentId = _m1));
        var p = (await Service<PlanningService>().ApplyAsync(Req(PlanningScope.All))).Data!;

        p.Items.Should().HaveCount(4);
        var onM1 = p.Items.OrderBy(i => i.ProposedStart).ToList();
        for (var i = 1; i < onM1.Count; i++) onM1[i].ProposedStart.Should().BeOnOrAfter(onM1[i - 1].ProposedEnd); // capacity 1: never overlapping
    }

    [Fact]
    public async Task Apply_UnplannableWorkOrder_Success_ListsItAndClearsItsOldPlan()
    {
        Arrange(db =>
        {
            var w = db.WorkOrders.Single(x => x.Id == _wo1);
            w.PlannedStartAt = Monday8; w.PlannedEndAt = Monday8.AddHours(2); w.PlannedAt = Monday8;
            db.Equipment.Where(e => e.Id == _m1 || e.Id == _m2).ToList().ForEach(e => e.IsActive = false);
        });
        var result = await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, _order));

        var p = result.Data!;
        p.Items.Should().BeEmpty();
        p.Unplanned.Should().HaveCount(2);
        p.Unplanned.Single(u => u.WorkOrderId == _wo1).Reason.Should().Be(MicroMES.ApiFeatures.PlanningFeature.Engine.UnplannedReasonCode.NoCandidateMachine);
        p.Unplanned.Single(u => u.WorkOrderId == _wo1).HadPlan.Should().BeTrue();
        p.Unplanned.Single(u => u.WorkOrderId == _wo2).Reason.Should().Be(MicroMES.ApiFeatures.PlanningFeature.Engine.UnplannedReasonCode.PredecessorUnplanned);
        p.Summary.Unplanned.Should().Be(2);
        Row(_wo1).PlannedStartAt.Should().BeNull();
    }

    [Fact]
    public async Task Apply_RunningWorkOrder_Success_IsNotWrittenAndOccupiesItsMachine()
    {
        Arrange(db =>
        {
            var w = db.WorkOrders.Single(x => x.Id == _wo1);
            w.Status = WorkOrderStatus.Running; w.StartedAt = Monday8; w.AssignedEquipmentId = _m1;
            db.WorkOrderResponses.Add(new WorkOrderResponse { WorkOrderId = w.Id, StartedAt = Monday8, EquipmentId = _m1 });
        });
        Order("OP-2", ProductionOrderStatus.Released, minutes1: 60, minutes2: 30);
        var p = (await Service<PlanningService>().ApplyAsync(Req(PlanningScope.All))).Data!;

        p.Items.Should().NotContain(i => i.WorkOrderId == _wo1);
        Row(_wo1).PlannedStartAt.Should().BeNull();
        p.Items.Single(i => i.WorkOrderId == _wo2).ProposedStart.Should().Be(Local(2026, 1, 5, 10)); // after the running job (120 min left)
    }

    [Fact]
    public async Task Apply_FailureInTheMiddle_Failed_RollsEverythingBack()
    {
        // The second update fails (trigger): the first one, already written inside the transaction, must not survive.
        Arrange(db => db.Database.ExecuteSqlRaw(
            $"CREATE TRIGGER fail_second BEFORE UPDATE ON WorkOrders WHEN NEW.Id = {_wo2} BEGIN SELECT RAISE(ABORT, 'boom'); END;"));

        var result = await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, _order));

        result.IsSuccessful.Should().BeFalse();
        result.Failure.Should().Be(ProductionOrderFailure.None);
        result.Errors.Should().Contain(e => e.Contains("Impossibile applicare il piano"));
        Row(_wo1).PlannedStartAt.Should().BeNull();
        Row(_wo1).AssignedEquipmentId.Should().BeNull();
        Row(_wo2).PlannedStartAt.Should().BeNull();
    }

    [Fact]
    public async Task RemovePlan_FailureInTheMiddle_Failed_RollsEverythingBack()
    {
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, _order));
        Arrange(db => db.Database.ExecuteSqlRaw(
            $"CREATE TRIGGER fail_second BEFORE UPDATE ON WorkOrders WHEN NEW.Id = {_wo2} BEGIN SELECT RAISE(ABORT, 'boom'); END;"));

        var result = await Service<PlanningService>().RemovePlanAsync(_order);

        result.IsSuccessful.Should().BeFalse();
        Row(_wo1).PlannedStartAt.Should().NotBeNull();
        Row(_wo2).PlannedStartAt.Should().NotBeNull();
    }

    // ---- remove -----------------------------------------------------------------------------------

    [Fact]
    public async Task RemovePlan_Success_ClearsDatesAndKeepsAssignedMachines()
    {
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, _order));
        var result = await Service<PlanningService>().RemovePlanAsync(_order);

        result.IsSuccessful.Should().BeTrue();
        result.Data!.ClearedWorkOrders.Should().Be(2);
        Row(_wo1).PlannedStartAt.Should().BeNull();
        Row(_wo1).PlannedEndAt.Should().BeNull();
        Row(_wo1).PlannedAt.Should().BeNull();
        Row(_wo1).AssignedEquipmentId.Should().Be(_m1);
    }

    [Fact]
    public async Task RemovePlan_NotPlanned_Success_ZeroCleared()
        => (await Service<PlanningService>().RemovePlanAsync(_order)).Data!.ClearedWorkOrders.Should().Be(0);

    [Fact]
    public async Task RemovePlan_ChildrenAreIncluded_ParentKeepsItsOwnPlan()
    {
        var (parent, p1, _) = Order("OP-P", ProductionOrderStatus.Released);
        var (child, c1, _) = Order("OP-C", ProductionOrderStatus.Released);
        Arrange(db => db.ProductionOrders.Single(o => o.Id == child).ParentOrderId = parent);
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.All));

        (await Service<PlanningService>().RemovePlanAsync(child)).Data!.ClearedWorkOrders.Should().Be(2);
        Row(c1).PlannedStartAt.Should().BeNull();
        Row(p1).PlannedStartAt.Should().NotBeNull();
        (await Service<PlanningService>().RemovePlanAsync(parent)).Data!.ClearedWorkOrders.Should().Be(2);
    }

    [Fact]
    public async Task RemovePlan_UnknownAndNotOpenOrders_NotFoundAndConflict()
    {
        (await Service<PlanningService>().RemovePlanAsync(9999)).Failure.Should().Be(ProductionOrderFailure.NotFound);
        var (id, _, _) = Order("OP-X", ProductionOrderStatus.Cancelled);
        (await Service<PlanningService>().RemovePlanAsync(id)).Failure.Should().Be(ProductionOrderFailure.Conflict);
    }

    // ---- alerts -----------------------------------------------------------------------------------

    [Fact]
    public async Task Alerts_NothingPlanned_Success_OnlyTheOpenWorkIsNotAnAlert()
    {
        var alerts = (await Service<PlanningService>().GetAlertsAsync()).Data!;
        alerts.Alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task Alerts_LateOrder_Success_CriticalWithDays()
    {
        var (late, _, _) = Order("OP-LATE", ProductionOrderStatus.Released, due: Monday8.AddHours(1));
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, late));

        var alerts = (await Service<PlanningService>().GetAlertsAsync()).Data!;
        var alert = alerts.Alerts.Single();
        alert.Type.Should().Be(PlanAlertType.Late);
        alert.ProductionOrderCode.Should().Be("OP-LATE");
        alert.LateDays.Should().BeGreaterThanOrEqualTo(1);
        alerts.CriticalCount.Should().Be(1);
        alerts.WarningCount.Should().Be(0);
    }

    [Fact]
    public async Task Alerts_UnplannableAndDisabledMachine_Success_NamesTheCause()
    {
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.All)); // plan on M1
        Arrange(db => db.Equipment.Single(e => e.Id == _m1).IsActive = false);

        var alerts = (await Service<PlanningService>().GetAlertsAsync()).Data!.Alerts;
        alerts.Should().Contain(a => a.Type == PlanAlertType.InactiveOrDisabledMachine && a.EquipmentCode == "M1");
        // M2 is still enabled: the work orders without a plan are plannable, nothing is reported as unplannable.
        alerts.Should().NotContain(a => a.Type == PlanAlertType.Unplannable);
    }

    [Fact]
    public async Task Alerts_StalePlan_Success_WhenAPredecessorEndsAfterThePlannedStart()
    {
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.All));
        Arrange(db =>
        {
            var w = db.WorkOrders.Single(x => x.Id == _wo1);
            w.Status = WorkOrderStatus.Ended; w.EndedAt = Local(2026, 1, 5, 10, 30); // the first work order ended later than planned
        });

        var alert = (await Service<PlanningService>().GetAlertsAsync()).Data!.Alerts.Single(a => a.Type == PlanAlertType.StalePlan);
        alert.WorkOrderId.Should().Be(_wo2);
        alert.CauseWorkOrderId.Should().Be(_wo1);
    }

    // ---- timeline ---------------------------------------------------------------------------------

    private static readonly DateTime DayStart = Local(2026, 1, 5, 0);

    private Task<MicroMES.ApiFeatures.ProductionOrderFeature.Services.ProductionOrderResult<TimelineModel>> Timeline(
        DateTime from, DateTime to, int? equipment = null, WorkOrderStatus? status = null, int? order = null)
        => Service<TimelineService>().GetAsync(from, to, equipment, status, order);

    [Fact]
    public async Task Timeline_PlannedWindow_Success_MachinesBarsNonWorkingAndLoad()
    {
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, _order));

        var t = (await Timeline(DayStart, DayStart.AddDays(1))).Data!;

        t.TimeZoneId.Should().Be("Europe/Rome");
        t.LoadPeriod.Should().Be(TimelinePeriod.Day);
        t.Equipment.Select(e => e.Code).Should().Equal("M1", "M2"); // M2 has no work and is listed anyway
        t.Equipment.Should().OnlyContain(e => e.IsActive && e.Capacity == 1);
        t.WorkOrders.Should().HaveCount(2);
        t.WorkOrders[0].EquipmentId.Should().Be(_m1);
        t.WorkOrders[0].PlannedStartAt.Should().Be(Monday8);
        t.WorkOrders[0].Lane.Should().Be(0);
        t.WorkOrders[0].ProductionOrderCode.Should().Be("OP-1");
        t.WorkOrders[0].MaterialName.Should().NotBeNullOrEmpty();
        t.WorkOrders[0].PlanStatus.Should().Be(PlanStatus.Planned);
        t.WorkOrders[0].IsLate.Should().BeFalse();
        t.WorkOrders[1].PredecessorIds.Should().Equal(_wo1);
        t.NonWorking.Where(n => n.EquipmentId == _m1).Should().HaveCount(3);
        t.Load.Single(l => l.EquipmentId == _m1).LoadPercent.Should().Be(37.5m); // 180 of 480 minutes
        t.Load.Single(l => l.EquipmentId == _m2).LoadPercent.Should().Be(0m);
    }

    [Fact]
    public async Task Timeline_WindowWithoutBars_Success_OnlyTheMachines()
    {
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, _order));
        var t = (await Timeline(DayStart.AddDays(2), DayStart.AddDays(3))).Data!;
        t.WorkOrders.Should().BeEmpty();
        t.Equipment.Should().HaveCount(2);
    }

    [Fact]
    public async Task Timeline_UnplannedWorkOrders_AreNotShown()
        => (await Timeline(DayStart, DayStart.AddDays(7))).Data!.WorkOrders.Should().BeEmpty();

    [Fact]
    public async Task Timeline_Filters_Success_MachineStatusAndOrder()
    {
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, _order));
        var (other, _, _) = Order("OP-2", ProductionOrderStatus.Released, minutes1: 30, minutes2: 30);
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, other));

        (await Timeline(DayStart, DayStart.AddDays(1))).Data!.WorkOrders.Should().HaveCount(4);
        var m2 = (await Timeline(DayStart, DayStart.AddDays(1), equipment: _m2)).Data!;
        m2.Equipment.Should().ContainSingle(e => e.Id == _m2);
        m2.WorkOrders.Should().OnlyContain(w => w.EquipmentId == _m2);
        (await Timeline(DayStart, DayStart.AddDays(1), order: other)).Data!.WorkOrders.Should().OnlyContain(w => w.ProductionOrderId == other);
        (await Timeline(DayStart, DayStart.AddDays(1), status: WorkOrderStatus.Running)).Data!.WorkOrders.Should().BeEmpty();
        (await Timeline(DayStart, DayStart.AddDays(1), status: WorkOrderStatus.Waiting)).Data!.WorkOrders.Should().HaveCount(4);

        (await Timeline(DayStart, DayStart.AddDays(1), equipment: 9999)).Failure.Should().Be(ProductionOrderFailure.NotFound);
        (await Timeline(DayStart, DayStart.AddDays(1), order: 9999)).Failure.Should().Be(ProductionOrderFailure.NotFound);
    }

    [Fact]
    public async Task Timeline_Window_Invalid_WhenInvertedOrLongerThan62Days()
    {
        var inverted = await Timeline(DayStart, DayStart);
        inverted.Failure.Should().Be(ProductionOrderFailure.Invalid);
        inverted.Errors.Should().Contain(e => e.Contains("dopo l'inizio"));

        var tooLong = await Timeline(DayStart, DayStart.AddDays(63));
        tooLong.Failure.Should().Be(ProductionOrderFailure.Invalid);
        tooLong.Errors.Should().Contain(e => e.Contains("62"));

        var exact = await Timeline(DayStart, DayStart.AddDays(62));
        exact.IsSuccessful.Should().BeTrue();
        exact.Data!.LoadPeriod.Should().Be(TimelinePeriod.Week);
    }

    [Fact]
    public async Task Timeline_UnspecifiedKindDates_AreTakenAsUtc()
    {
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, _order));
        var t = (await Timeline(DateTime.SpecifyKind(DayStart, DateTimeKind.Unspecified), DateTime.SpecifyKind(DayStart.AddDays(1), DateTimeKind.Unspecified))).Data!;
        t.From.Should().Be(DayStart);
        t.WorkOrders.Should().HaveCount(2);
    }

    [Fact]
    public async Task Timeline_OverlappingPlansOnACapacityTwoMachine_GetDifferentLanes()
    {
        Arrange(db => db.Equipment.Single(e => e.Id == _m1).Capacity = 2);
        Arrange(db => db.Equipment.Single(e => e.Id == _m2).IsActive = false);
        var (other, _, _) = Order("OP-2", ProductionOrderStatus.Released, minutes1: 120, minutes2: 60);
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.All));

        var t = (await Timeline(DayStart, DayStart.AddDays(1))).Data!;
        var firsts = t.WorkOrders.Where(w => w.Id == _wo1 || w.ProductionOrderId == other && w.PredecessorIds.Count == 0).ToList();
        firsts.Select(w => w.Lane).Should().BeEquivalentTo([0, 1]);
        t.Equipment.Should().ContainSingle().Which.Capacity.Should().Be(2);
        t.Load.Single().LoadPercent.Should().Be(37.5m); // (120+60+120+60) of 2 x 480 minutes
    }

    [Fact]
    public async Task Timeline_RunningAndEndedWorkOrders_UseTheirRealDatesAndMachines()
    {
        Arrange(db =>
        {
            var w1 = db.WorkOrders.Single(x => x.Id == _wo1);
            w1.Status = WorkOrderStatus.Ended; w1.StartedAt = Monday8.AddHours(-26); w1.EndedAt = Monday8.AddHours(-24);
            db.WorkOrderResponses.Add(new WorkOrderResponse { WorkOrderId = w1.Id, StartedAt = w1.StartedAt.Value, EndedAt = w1.EndedAt, EquipmentId = _m2 });
            var w2 = db.WorkOrders.Single(x => x.Id == _wo2);
            w2.Status = WorkOrderStatus.Running; w2.StartedAt = Monday8.AddHours(-1); w2.AssignedEquipmentId = _m1;
            db.WorkOrderResponses.Add(new WorkOrderResponse { WorkOrderId = w2.Id, StartedAt = Monday8.AddHours(-1), EquipmentId = _m1 });
        });

        var t = (await Timeline(Monday8.AddDays(-2), Monday8.AddDays(1))).Data!;

        var ended = t.WorkOrders.Single(w => w.Id == _wo1);
        ended.EquipmentId.Should().Be(_m2);
        ended.ActualStartAt.Should().Be(Monday8.AddHours(-26));
        ended.ActualEndAt.Should().Be(Monday8.AddHours(-24));
        ended.Status.Should().Be(WorkOrderStatus.Ended);
        var running = t.WorkOrders.Single(w => w.Id == _wo2);
        running.EquipmentId.Should().Be(_m1);
        running.ActualStartAt.Should().Be(Monday8.AddHours(-1));
        running.ActualEndAt.Should().BeNull();
    }

    [Fact]
    public async Task Timeline_PhaseWithoutMachines_UsesTheVirtualRowAndDefaultCalendar()
    {
        var noMachines = AddSegment("MAN");
        var (order, w1, _) = Order("OP-MAN", ProductionOrderStatus.Released, segment: noMachines);
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, order));

        var t = (await Timeline(DayStart, DayStart.AddDays(1))).Data!;

        t.WorkOrders.Should().OnlyContain(w => w.EquipmentId == null);
        t.WorkOrders.Select(w => w.Id).Should().Contain(w1);
        t.NonWorking.Should().Contain(n => n.EquipmentId == null);
    }

    [Fact]
    public async Task Timeline_LateOrder_Success_FlagsLateWithDays()
    {
        var (late, _, _) = Order("OP-LATE", ProductionOrderStatus.Released, due: Monday8.AddHours(1));
        await Service<PlanningService>().ApplyAsync(Req(PlanningScope.Order, late));

        var t = (await Timeline(DayStart, DayStart.AddDays(1), order: late)).Data!;
        t.WorkOrders.Should().OnlyContain(w => w.IsLate && w.LateDays >= 1);
    }
}
