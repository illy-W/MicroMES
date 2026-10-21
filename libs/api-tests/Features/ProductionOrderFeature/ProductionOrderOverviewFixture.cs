using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.ProductionOrderFeature;

public class ProductionOrderOverviewFixture
{
    private readonly ProductionData _data = new(new FakeTimeProvider());
    private int _dep;

    private void Order(int id, int? parent, ProductionOrderStatus status = ProductionOrderStatus.Released)
    {
        _data.Materials[id] = new Material { Id = id, Code = $"M{id}", Name = $"Mat {id}" };
        _data.Orders[id] = new ProductionOrder { Id = id, Code = $"OP-{id}", MaterialId = id, Quantity = 1, ParentOrderId = parent, Status = status };
    }

    /// <summary>Work order with planned minutes = setup (quantity 1, run 0).</summary>
    private void Wo(int id, int order, int op, decimal minutes, WorkOrderStatus status = WorkOrderStatus.Waiting)
        => _data.WorkOrders[id] = new WorkOrder
        {
            Id = id, ProductionOrderId = order, OperationNumber = op, SetupMinutes = minutes, RunMinutesPerUnit = 0,
            PlannedQuantity = 1, Status = status
        };

    private void Dep(int pred, int succ)
        => _data.Dependencies.Add(new WorkOrderDependency { Id = ++_dep, PredecessorWorkOrderId = pred, SuccessorWorkOrderId = succ });

    [Fact]
    public void ToOverview_FamilyWithoutChildren_Success_LinearChain()
    {
        Order(1, null);
        Wo(11, 1, 10, 5, WorkOrderStatus.Ended); Wo(12, 1, 20, 7, WorkOrderStatus.Ready);
        Dep(11, 12);

        var o = _data.ToOverview(1);

        o.Orders.Should().ContainSingle().Which.Id.Should().Be(1);
        o.WorkOrders.Select(w => w.Id).Should().Equal(11, 12);
        o.Dependencies.Should().ContainSingle();
        o.Summary.TotalWorkOrders.Should().Be(2);
        o.Summary.MinimumDurationMinutes.Should().Be(12);
        o.Summary.WorkContentMinutes.Should().Be(12);
        o.Summary.CriticalPathWorkOrderIds.Should().Equal(11, 12);
    }

    [Fact]
    public void ToOverview_OneChild_Success_CrossOrderDependencyAndCriticalPath()
    {
        Order(1, null); Order(2, 1);
        Wo(11, 1, 10, 4); Wo(21, 2, 10, 3); Wo(22, 2, 20, 6);
        Dep(21, 22); Dep(22, 11); // child last step -> parent start step

        var o = _data.ToOverview(1);

        o.Orders.Select(x => x.Id).Should().Equal(1, 2);
        o.Dependencies.Should().Contain(d => d.PredecessorWorkOrderId == 22 && d.SuccessorWorkOrderId == 11);
        o.Dependencies.Should().HaveCount(2);
        o.Summary.MinimumDurationMinutes.Should().Be(13);
        o.Summary.CriticalPathWorkOrderIds.Should().Equal(21, 22, 11);
    }

    [Fact]
    public void ToOverview_TwoLevelsOfChildren_Success_ChildIdReturnsOnlyItsSubtree()
    {
        Order(1, null); Order(2, 1); Order(3, 2);
        Wo(11, 1, 10, 1); Wo(21, 2, 10, 1); Wo(31, 3, 10, 1);
        Dep(31, 21); Dep(21, 11);

        var all = _data.ToOverview(1);
        var sub = _data.ToOverview(2);

        all.Orders.Should().HaveCount(3);
        all.WorkOrders.Should().HaveCount(3);
        all.Dependencies.Should().HaveCount(2);
        sub.Orders.Select(x => x.Id).Should().Equal(2, 3);
        sub.Orders.Single(x => x.Id == 2).ParentOrderId.Should().BeNull();
        sub.WorkOrders.Select(w => w.Id).Should().Equal(21, 31);
        sub.Dependencies.Should().ContainSingle().Which.PredecessorWorkOrderId.Should().Be(31);
        sub.Summary.CriticalPathWorkOrderIds.Should().Equal(31, 21);
    }

    [Fact]
    public void ToOverview_MixedStatuses_Success_CountersPerStatus()
    {
        Order(1, null);
        Wo(11, 1, 10, 1, WorkOrderStatus.Ended); Wo(12, 1, 20, 1, WorkOrderStatus.Ended);
        Wo(13, 1, 30, 1, WorkOrderStatus.Running); Wo(14, 1, 40, 1, WorkOrderStatus.Ready);
        Wo(15, 1, 50, 1, WorkOrderStatus.Interrupted); Wo(16, 1, 60, 1, WorkOrderStatus.Aborted);
        Wo(17, 1, 70, 1);

        var c = _data.ToOverview(1).Summary.Counts;

        (c.Waiting, c.Ready, c.Running, c.Interrupted, c.Ended, c.Aborted).Should().Be((1, 1, 1, 1, 2, 1));
    }

    [Fact]
    public void ToOverview_ParallelBranches_Success_CriticalPathPicksLongestBranch()
    {
        Order(1, null);
        Wo(11, 1, 10, 2); Wo(12, 1, 20, 10); Wo(13, 1, 30, 3); Wo(14, 1, 40, 1);
        Dep(11, 12); Dep(11, 13); Dep(12, 14); Dep(13, 14);

        var s = _data.ToOverview(1).Summary;

        s.CriticalPathWorkOrderIds.Should().Equal(11, 12, 14);
        s.MinimumDurationMinutes.Should().Be(13);
        s.WorkContentMinutes.Should().Be(16);
    }

    [Fact]
    public void ToOverview_PlannedOrder_Success_EmptyListsAndZeroSummary()
    {
        Order(1, null, ProductionOrderStatus.Planned);

        var o = _data.ToOverview(1);

        o.Orders.Should().ContainSingle();
        o.WorkOrders.Should().BeEmpty();
        o.Dependencies.Should().BeEmpty();
        o.Summary.TotalWorkOrders.Should().Be(0);
        o.Summary.MinimumDurationMinutes.Should().Be(0);
        o.Summary.CriticalPathWorkOrderIds.Should().BeEmpty();
        o.GeneratedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}
