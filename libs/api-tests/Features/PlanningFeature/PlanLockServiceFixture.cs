using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.PlanningFeature.Services;
using MicroMES.ApiFeatures.ProductionOrderFeature.Seeding;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

/// <summary>Manual move (preview and apply) on in-memory SQLite, clock Monday 5 Jan 2026 08:00 Rome. Default calendar Mon-Fri 08-12 / 13-17.</summary>
public class PlanLockServiceFixture : BaseFeatureFixture
{
    private readonly int _segment, _freeSegment, _m1, _m2, _other, _order, _wo1, _wo2, _wo3;

    public PlanLockServiceFixture()
    {
        Service<OverridableTimeProvider>().Set(Local(2026, 1, 5, 8));
        var material = AddMaterial("PF");
        _segment = AddSegment("SEG");
        _freeSegment = AddSegment("FREE");
        var otherSegment = AddSegment("OTHER");
        int m1 = 0, m2 = 0, other = 0, order = 0, w1 = 0, w2 = 0, w3 = 0;
        Arrange(db =>
        {
            var now = DateTime.UtcNow;
            var calendar = new WorkCalendar { Code = "STD", Name = "Standard", IsActive = true, IsDefault = true, CreatedAt = now, UpdatedAt = now };
            db.WorkCalendars.Add(calendar);
            db.SaveChanges();
            foreach (var s in Standard())
                db.WorkCalendarSlots.Add(new WorkCalendarSlot { WorkCalendarId = calendar.Id, DayOfWeek = s.DayOfWeek, StartTime = s.Start, EndTime = s.End });
            Equipment Eq(string code, decimal eff) => new() { Code = code, Name = code, IsActive = true, Capacity = 1, EfficiencyPercent = eff, CreatedAt = now, UpdatedAt = now };
            var a = Eq("M1", 100m);
            var b = Eq("M2", 50m);
            var c = Eq("M3", 100m);
            db.Equipment.AddRange(a, b, c);
            db.SaveChanges();
            db.EquipmentProcessSegments.Add(new EquipmentProcessSegment { EquipmentId = a.Id, ProcessSegmentId = _segment, IsDefault = true });
            db.EquipmentProcessSegments.Add(new EquipmentProcessSegment { EquipmentId = b.Id, ProcessSegmentId = _segment });
            db.EquipmentProcessSegments.Add(new EquipmentProcessSegment { EquipmentId = c.Id, ProcessSegmentId = otherSegment });
            m1 = a.Id; m2 = b.Id; other = c.Id;

            var o = new ProductionOrder { Code = "OP-1", MaterialId = material, Quantity = 1, Status = ProductionOrderStatus.Released, RequestedEnd = Local(2026, 1, 6, 17), CreatedAt = now, UpdatedAt = now };
            db.ProductionOrders.Add(o);
            db.SaveChanges();
            WorkOrder Make(int op, int segment, decimal minutes, int? machine) => new()
            {
                ProductionOrderId = o.Id, OperationNumber = op, ProcessSegmentId = segment, ProcessSegmentCode = "S", ProcessSegmentName = "S",
                SetupMinutes = minutes, PlannedQuantity = 1, Status = WorkOrderStatus.Waiting, AssignedEquipmentId = machine, CreatedAt = now, UpdatedAt = now
            };
            var x = Make(10, _segment, 120, m1);
            var y = Make(20, _segment, 60, m1);
            var z = Make(30, _freeSegment, 60, null);
            db.WorkOrders.AddRange(x, y, z);
            db.SaveChanges();
            db.WorkOrderDependencies.Add(new WorkOrderDependency { PredecessorWorkOrderId = x.Id, SuccessorWorkOrderId = y.Id });
            y.PlannedStartAt = Local(2026, 1, 6, 9);
            y.PlannedEndAt = Local(2026, 1, 6, 10);
            db.SaveChanges();
            order = o.Id; w1 = x.Id; w2 = y.Id; w3 = z.Id;
        });
        (_m1, _m2, _other, _order, _wo1, _wo2, _wo3) = (m1, m2, other, order, w1, w2, w3);
    }

    private static MoveWorkOrderRequest Req(DateTime start, int? machine = null, DateTime? expected = null)
        => new() { StartAt = start, EquipmentId = machine, ExpectedUpdatedAt = expected };

    private WorkOrder Row(int id) => Query(db => db.WorkOrders.Single(w => w.Id == id));

    [Fact]
    public async Task Preview_WorkingStart_Success_ComputesEndAndWritesNothing()
    {
        var r = await Service<PlanLockService>().PreviewAsync(_wo1, Req(Local(2026, 1, 6, 10)));

        r.IsSuccessful.Should().BeTrue();
        r.Data!.NewStart.Should().Be(Local(2026, 1, 6, 10));
        r.Data.NewEnd.Should().Be(Local(2026, 1, 6, 12));
        r.Data.DurationMinutes.Should().Be(120);
        r.Data.Equipment!.Id.Should().Be(_m1);
        Row(_wo1).PlannedStartAt.Should().BeNull();
        Row(_wo1).IsPlanLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Preview_MachineWithEfficiency50_Success_DoublesDuration()
    {
        var r = await Service<PlanLockService>().PreviewAsync(_wo1, Req(Local(2026, 1, 6, 8), _m2));

        r.Data!.DurationMinutes.Should().Be(240);
        r.Data.NewEnd.Should().Be(Local(2026, 1, 6, 12));
    }

    [Fact]
    public async Task Preview_StartInThePast_Failure_422()
    {
        var r = await Service<PlanLockService>().PreviewAsync(_wo1, Req(Local(2026, 1, 5, 8)));

        r.Failure.Should().Be(ProductionOrderFailure.Invalid);
        r.Errors.Should().Contain("L'inizio deve essere dopo adesso.");
    }

    [Fact]
    public async Task Preview_NonWorkingStart_Failure_NamesReasonAndSuggestion()
    {
        var r = await Service<PlanLockService>().PreviewAsync(_wo1, Req(Local(2026, 1, 10, 10)));

        r.Failure.Should().Be(ProductionOrderFailure.Invalid);
        r.Errors.Distinct().Single().Should().Contain("M1 non lavora il 10/01").And.Contain("Prossimo orario disponibile: 12/01 08:00");
    }

    [Fact]
    public async Task Preview_MachineNotEnabledForSegment_Failure_422()
    {
        var r = await Service<PlanLockService>().PreviewAsync(_wo1, Req(Local(2026, 1, 6, 10), _other));

        r.Failure.Should().Be(ProductionOrderFailure.Invalid);
        r.Errors.Distinct().Single().Should().Contain("non è collegata alla fase");
    }

    [Fact]
    public async Task Preview_SegmentWithoutMachines_Success_UsesDefaultCalendar()
    {
        var r = await Service<PlanLockService>().PreviewAsync(_wo3, Req(Local(2026, 1, 6, 16)));

        r.IsSuccessful.Should().BeTrue();
        r.Data!.Equipment.Should().BeNull();
        r.Data.NewEnd.Should().Be(Local(2026, 1, 6, 17));
    }

    [Fact]
    public async Task Preview_SegmentWithoutMachinesAndMachineGiven_Failure_422()
    {
        var r = await Service<PlanLockService>().PreviewAsync(_wo3, Req(Local(2026, 1, 6, 10), _m1));

        r.Failure.Should().Be(ProductionOrderFailure.Invalid);
    }

    [Fact]
    public async Task Preview_SuccessorAndLate_Success_ConflictsListed()
    {
        // wo1 ends Tue 17:00 +: starting Tue 15:00, 120 min -> Wed 09:00; successor starts Tue 09:00 (before), order due Tue 17:00 (late)
        var r = await Service<PlanLockService>().PreviewAsync(_wo1, Req(Local(2026, 1, 6, 16)));

        r.Data!.Conflicts.Select(c => c.Code).Should().Contain([MoveConflictCode.SuccessorsStartBeforeEnd, MoveConflictCode.Late]);
    }

    [Fact]
    public async Task Preview_PredecessorEndsAfterStart_Success_Conflict()
    {
        // moving wo2 (successor of wo1) while wo1 is planned later
        Arrange(db =>
        {
            var w = db.WorkOrders.Single(x => x.Id == _wo1);
            w.PlannedStartAt = Local(2026, 1, 7, 8);
            w.PlannedEndAt = Local(2026, 1, 7, 10);
            db.SaveChanges();
        });

        var r = await Service<PlanLockService>().PreviewAsync(_wo2, Req(Local(2026, 1, 6, 8)));

        r.Data!.Conflicts.Should().Contain(c => c.Code == MoveConflictCode.PredecessorEndsAfterStart && c.Minutes == 26 * 60);
    }

    [Fact]
    public async Task Preview_MachineOverCapacity_Success_ListsInvolvedWorkOrder()
    {
        // wo2 is planned on M1 Tue 09-10: moving wo1 on M1 Tue 09:00 overlaps
        var r = await Service<PlanLockService>().PreviewAsync(_wo1, Req(Local(2026, 1, 6, 9)));

        var c = r.Data!.Conflicts.Single(x => x.Code == MoveConflictCode.MachineOverCapacity);
        c.WorkOrderIds.Should().Contain(_wo2);
        c.EquipmentId.Should().Be(_m1);
    }

    [Fact]
    public async Task Apply_Success_WritesPlanMachineAndLockInOneStep()
    {
        var r = await Service<PlanLockService>().ApplyAsync(_wo1, Req(Local(2026, 1, 6, 8), _m2));

        r.IsSuccessful.Should().BeTrue();
        r.Data!.WorkOrder.IsPlanLocked.Should().BeTrue();
        var row = Row(_wo1);
        (row.PlannedStartAt, row.PlannedEndAt, row.AssignedEquipmentId, row.IsPlanLocked)
            .Should().Be((Local(2026, 1, 6, 8), Local(2026, 1, 6, 12), _m2, true));
        row.PlannedAt.Should().NotBeNull();
        row.PlanLockedAt.Should().NotBeNull();
        Row(_wo2).PlannedStartAt.Should().Be(Local(2026, 1, 6, 9)); // successor not moved
    }

    [Fact]
    public async Task Apply_ChangedSincePreview_Failure_409()
    {
        var preview = (await Service<PlanLockService>().PreviewAsync(_wo1, Req(Local(2026, 1, 6, 8)))).Data!;
        Arrange(db =>
        {
            var w = db.WorkOrders.Single(x => x.Id == _wo1);
            w.UpdatedAt = w.UpdatedAt.AddMinutes(5);
            db.SaveChanges();
        });

        var r = await Service<PlanLockService>().ApplyAsync(_wo1, Req(Local(2026, 1, 6, 8), null, preview.ExpectedUpdatedAt));

        r.Failure.Should().Be(ProductionOrderFailure.Conflict);
        r.Errors.Should().Contain("Il lavoro è cambiato dopo l'anteprima.");
        Row(_wo1).IsPlanLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Apply_RunningWorkOrder_Failure_409NamesStatus()
    {
        Arrange(db =>
        {
            var w = db.WorkOrders.Single(x => x.Id == _wo1);
            w.Status = WorkOrderStatus.Running;
            db.SaveChanges();
        });

        var r = await Service<PlanLockService>().ApplyAsync(_wo1, Req(Local(2026, 1, 6, 8)));

        r.Failure.Should().Be(ProductionOrderFailure.Conflict);
        r.Errors.Distinct().Single().Should().Contain("In esecuzione");
    }

    [Fact]
    public async Task Preview_UnknownWorkOrder_Failure_404()
        => (await Service<PlanLockService>().PreviewAsync(9999, Req(Local(2026, 1, 6, 8)))).Failure.Should().Be(ProductionOrderFailure.NotFound);
}
