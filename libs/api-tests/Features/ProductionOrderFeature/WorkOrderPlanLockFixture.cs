using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.ProductionOrderFeature;

/// <summary>Lock / Move / Unlock actions of a work order (pure, from the data in memory).</summary>
public class WorkOrderPlanLockFixture
{
    private readonly ProductionData _data = new(new FakeTimeProvider());
    private static readonly DateTime Start = new(2030, 1, 7, 8, 0, 0, DateTimeKind.Utc);

    private WorkOrder Wo(WorkOrderStatus status = WorkOrderStatus.Ready, ProductionOrderStatus order = ProductionOrderStatus.Released,
        bool planned = true, int? equipment = 5, bool locked = false, bool segmentHasMachines = true)
    {
        _data.Materials[1] = new Material { Id = 1, Code = "M", Name = "Mat" };
        _data.Orders[1] = new ProductionOrder { Id = 1, Code = "OP-1", MaterialId = 1, Quantity = 1, Status = order };
        if (segmentHasMachines) _data.EquipmentLinks.Add((5, 7));
        var wo = new WorkOrder
        {
            Id = 1, ProductionOrderId = 1, OperationNumber = 10, ProcessSegmentId = 7, PlannedQuantity = 1, Status = status,
            PlannedStartAt = planned ? Start : null, PlannedEndAt = planned ? Start.AddHours(1) : null,
            AssignedEquipmentId = equipment, IsPlanLocked = locked, PlanLockedAt = locked ? Start : null
        };
        _data.WorkOrders[1] = wo;
        return wo;
    }

    [Fact]
    public void Actions_PlannedReady_Success_LockAndMove()
        => _data.ToWorkOrderModel(Wo()).AllowedActions.Should().Contain(["Lock", "Move"]).And.NotContain("Unlock");

    [Fact]
    public void Actions_Locked_Success_MoveAndUnlockNoLock()
    {
        var a = _data.ToWorkOrderModel(Wo(locked: true));
        a.AllowedActions.Should().Contain(["Move", "Unlock"]).And.NotContain("Lock");
        a.IsPlanLocked.Should().BeTrue();
    }

    [Fact]
    public void Actions_NotPlanned_Success_OnlyMove()
        => _data.ToWorkOrderModel(Wo(planned: false)).AllowedActions.Should().Contain("Move").And.NotContain("Lock");

    [Fact]
    public void Actions_NoMachineButSegmentHasMachines_Success_NoLock()
        => _data.ToWorkOrderModel(Wo(equipment: null)).AllowedActions.Should().NotContain("Lock");

    [Fact]
    public void Actions_SegmentWithoutMachines_Success_Lock()
        => _data.ToWorkOrderModel(Wo(equipment: null, segmentHasMachines: false)).AllowedActions.Should().Contain("Lock");

    [Theory]
    [InlineData(WorkOrderStatus.Running)]
    [InlineData(WorkOrderStatus.Ended)]
    [InlineData(WorkOrderStatus.Aborted)]
    public void Actions_StatusNotAllowed_Success_NoPlanCommands(WorkOrderStatus status)
        => _data.ToWorkOrderModel(Wo(status)).AllowedActions.Should().NotContain(["Lock", "Move"]);

    [Theory]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Cancelled)]
    public void Actions_OrderClosed_Success_NoPlanCommands(ProductionOrderStatus order)
        => _data.ToWorkOrderModel(Wo(order: order)).AllowedActions.Should().NotContain(["Lock", "Move"]);
}
