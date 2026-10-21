using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.ProductionOrderFeature;

public class ProductionOrderPlannerFixture
{
    private const MaterialType Finished = MaterialType.FinishedGood;
    private const MaterialType Inter = MaterialType.Intermediate;
    private const MaterialType Raw = MaterialType.RawMaterial;

    private static IEnumerable<(int, int)> OperationEdges(PlannedOrder order)
        => order.Dependencies.Select(d => (d.Predecessor.OperationNumber, d.Successor.OperationNumber));

    // ---- single level --------------------------------------------------------------------------------

    [Fact]
    public void Plan_MaterialWithoutRouting_Failure_EmptyRouting()
    {
        var catalog = new PlannerCatalogBuilder().Material(1, Finished).Build();

        var result = ProductionOrderPlanner.Plan(1, 5m, catalog);

        result.Order.Should().BeNull();
        result.Error.Should().Be(PlanError.EmptyRouting);
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Plan_RoutingWithoutSteps_Failure_EmptyRouting()
    {
        var catalog = new PlannerCatalogBuilder().Material(1, Finished).Routing(1, []).Build();

        ProductionOrderPlanner.Plan(1, 5m, catalog).Error.Should().Be(PlanError.EmptyRouting);
    }

    [Fact]
    public void Plan_LinearRouting_Success_OneWorkOrderPerStepChainedInOrder()
    {
        var catalog = new PlannerCatalogBuilder().Material(1, Finished).LinearRouting(1, 10, 20, 30).Build();

        var result = ProductionOrderPlanner.Plan(1, 4m, catalog);

        result.Error.Should().Be(PlanError.None);
        var order = result.Order!;
        order.MaterialId.Should().Be(1);
        order.Quantity.Should().Be(4m);
        order.WorkOrders.Select(w => w.OperationNumber).Should().Equal(10, 20, 30);
        order.WorkOrders.Should().OnlyContain(w => w.PlannedQuantity == 4m);
        OperationEdges(order).Should().BeEquivalentTo([(10, 20), (20, 30)]);
        order.StartSteps.Select(w => w.OperationNumber).Should().Equal(10);
        order.FinalSteps.Select(w => w.OperationNumber).Should().Equal(30);
        order.Children.Should().BeEmpty();
        order.Materials.Should().BeEmpty();
    }

    [Fact]
    public void Plan_UnsortedSteps_Success_WorkOrdersSortedByOperationNumber()
    {
        var catalog = new PlannerCatalogBuilder().Material(1, Finished)
            .Routing(1, [(30, 0m, 1m), (10, 0m, 1m), (20, 0m, 1m)], (10, 20), (20, 30))
            .Build();

        var order = ProductionOrderPlanner.Plan(1, 1m, catalog).Order!;

        order.WorkOrders.Select(w => w.OperationNumber).Should().Equal(10, 20, 30);
    }

    [Fact]
    public void Plan_ParallelBranchesConverging_Success_StartAndFinalStepsAreDetected()
    {
        // 10 -> (20, 30) -> 40
        var catalog = new PlannerCatalogBuilder().Material(1, Finished)
            .Routing(1, [(10, 0m, 1m), (20, 0m, 1m), (30, 0m, 1m), (40, 0m, 1m)], (10, 20), (10, 30), (20, 40), (30, 40))
            .Build();

        var order = ProductionOrderPlanner.Plan(1, 2m, catalog).Order!;

        OperationEdges(order).Should().BeEquivalentTo([(10, 20), (10, 30), (20, 40), (30, 40)]);
        order.StartSteps.Select(w => w.OperationNumber).Should().Equal(10);
        order.FinalSteps.Select(w => w.OperationNumber).Should().Equal(40);
    }

    [Fact]
    public void Plan_TwoStartSteps_Success_FirstStartStepIsTheLowestOperationNumber()
    {
        // 20 and 10 both start, both lead to 30
        var catalog = new PlannerCatalogBuilder().Material(1, Finished)
            .Routing(1, [(20, 0m, 1m), (10, 0m, 1m), (30, 0m, 1m)], (20, 30), (10, 30))
            .Build();

        var order = ProductionOrderPlanner.Plan(1, 1m, catalog).Order!;

        order.StartSteps.Select(w => w.OperationNumber).Should().BeEquivalentTo([10, 20]);
        ProductionOrderPlanner.FirstStartStep(order).OperationNumber.Should().Be(10);
    }

    [Fact]
    public void Plan_SingleStep_Success_IsBothStartAndFinal()
    {
        var catalog = new PlannerCatalogBuilder().Material(1, Finished).LinearRouting(1, 10).Build();

        var order = ProductionOrderPlanner.Plan(1, 1m, catalog).Order!;

        order.StartSteps.Should().ContainSingle().Which.OperationNumber.Should().Be(10);
        order.FinalSteps.Should().ContainSingle().Which.OperationNumber.Should().Be(10);
        order.Dependencies.Should().BeEmpty();
    }

    // ---- planned minutes -----------------------------------------------------------------------------

    [Theory]
    [InlineData(10, 2.5, 4, 20)]
    [InlineData(0, 3, 10, 30)]
    [InlineData(15, 0, 100, 15)]
    [InlineData(5, 1.5, 0.5, 5.75)]
    public void PlannedMinutes_Success_IsSetupPlusRunTimesQuantity(decimal setup, decimal run, decimal quantity, decimal expected)
    {
        ProductionOrderPlanner.PlannedMinutes(setup, run, quantity).Should().Be(expected);
    }

    [Fact]
    public void Plan_Success_PlannedWorkOrderMinutesUseStepTimesAndOrderQuantity()
    {
        var catalog = new PlannerCatalogBuilder().Material(1, Finished)
            .Routing(1, [(10, 10m, 2.5m), (20, 5m, 1m)], (10, 20))
            .Build();

        var order = ProductionOrderPlanner.Plan(1, 4m, catalog).Order!;

        order.WorkOrders.Select(w => w.PlannedMinutes).Should().Equal(20m, 9m);
    }

    // ---- BOM / children ------------------------------------------------------------------------------

    [Fact]
    public void Plan_ComponentWithRouting_Success_CreatesChildWithQuantityParentTimesBom()
    {
        var catalog = new PlannerCatalogBuilder()
            .Material(1, Finished).LinearRouting(1, 10, 20)
            .Material(2, Inter).LinearRouting(2, 10, 20)
            .Bom(1, 2, 3m)
            .Build();

        var order = ProductionOrderPlanner.Plan(1, 5m, catalog).Order!;

        var child = order.Children.Should().ContainSingle().Subject;
        child.MaterialId.Should().Be(2);
        child.Quantity.Should().Be(15m);
        child.WorkOrders.Should().OnlyContain(w => w.PlannedQuantity == 15m);
        var line = order.Materials.Should().ContainSingle().Subject;
        line.MaterialId.Should().Be(2);
        line.RequiredQuantity.Should().Be(15m);
        line.Child.Should().BeSameAs(child);
    }

    [Fact]
    public void Plan_ComponentWithRouting_Success_ChildLastStepGatesEveryStartStepOfParent()
    {
        // parent has two start steps (10 and 20), the child ends with step 20
        var catalog = new PlannerCatalogBuilder()
            .Material(1, Finished).Routing(1, [(10, 0m, 1m), (20, 0m, 1m), (30, 0m, 1m)], (10, 30), (20, 30))
            .Material(2, Inter).LinearRouting(2, 10, 20)
            .Bom(1, 2, 1m)
            .Build();

        var order = ProductionOrderPlanner.Plan(1, 1m, catalog).Order!;

        var childLast = order.Children.Single().FinalSteps.Single();
        var gated = order.Dependencies.Where(d => d.Predecessor == childLast).Select(d => d.Successor.OperationNumber);
        gated.Should().BeEquivalentTo([10, 20]);
        order.Dependencies.Should().HaveCount(4, "two from the routing and two from the child");
        order.StartSteps.Select(w => w.OperationNumber).Should().BeEquivalentTo([10, 20],
            "child dependencies do not change which steps are start steps of the routing");
    }

    [Fact]
    public void Plan_NestedTwoLevels_Success_QuantitiesMultiplyDownTheTree()
    {
        var catalog = new PlannerCatalogBuilder()
            .Material(1, Finished).LinearRouting(1, 10)
            .Material(2, Inter).LinearRouting(2, 10)
            .Material(3, Inter).LinearRouting(3, 10)
            .Bom(1, 2, 3m).Bom(2, 3, 2m)
            .Build();

        var order = ProductionOrderPlanner.Plan(1, 5m, catalog).Order!;

        var child = order.Children.Single();
        var grandchild = child.Children.Single();
        child.Quantity.Should().Be(15m);
        grandchild.Quantity.Should().Be(30m);
        grandchild.MaterialId.Should().Be(3);
        child.Dependencies.Should().ContainSingle(d => d.Predecessor == grandchild.FinalSteps.Single());
    }

    [Fact]
    public void Plan_IntermediateWithoutRouting_Success_NoChildButMaterialLineKept()
    {
        var catalog = new PlannerCatalogBuilder()
            .Material(1, Finished).LinearRouting(1, 10)
            .Material(2, Inter)
            .Bom(1, 2, 2m)
            .Build();

        var order = ProductionOrderPlanner.Plan(1, 3m, catalog).Order!;

        order.Children.Should().BeEmpty();
        var line = order.Materials.Should().ContainSingle().Subject;
        line.MaterialId.Should().Be(2);
        line.RequiredQuantity.Should().Be(6m);
        line.Child.Should().BeNull();
        order.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Plan_RawMaterialComponent_Success_NeverBecomesAChildEvenWithARouting()
    {
        var catalog = new PlannerCatalogBuilder()
            .Material(1, Finished).LinearRouting(1, 10)
            .Material(2, Raw).LinearRouting(2, 10)
            .Bom(1, 2, 1m)
            .Build();

        var order = ProductionOrderPlanner.Plan(1, 1m, catalog).Order!;

        order.Children.Should().BeEmpty();
        order.Materials.Should().ContainSingle(m => m.MaterialId == 2 && m.Child == null);
    }

    [Fact]
    public void Plan_MixOfComponents_Success_MaterialsKeepBomOrderAndOnlyRoutedOnesHaveChildren()
    {
        var catalog = new PlannerCatalogBuilder()
            .Material(1, Finished).LinearRouting(1, 10)
            .Material(2, Raw).Material(3, Inter).LinearRouting(3, 10).Material(4, Inter)
            .Bom(1, 2, 4m).Bom(1, 3, 1m).Bom(1, 4, 2m)
            .Build();

        var order = ProductionOrderPlanner.Plan(1, 10m, catalog).Order!;

        order.Materials.Select(m => (m.MaterialId, m.RequiredQuantity)).Should().Equal((2, 40m), (3, 10m), (4, 20m));
        order.Children.Select(c => c.MaterialId).Should().Equal(3);
    }

    [Fact]
    public void Plan_SharedSubComponentInTwoBranches_Success_IsNotMistakenForACycle()
    {
        // 1 -> 2 -> 4 and 1 -> 3 -> 4: material 4 appears twice but never on the same path
        var catalog = new PlannerCatalogBuilder()
            .Material(1, Finished).LinearRouting(1, 10)
            .Material(2, Inter).LinearRouting(2, 10)
            .Material(3, Inter).LinearRouting(3, 10)
            .Material(4, Inter).LinearRouting(4, 10)
            .Bom(1, 2, 1m).Bom(1, 3, 1m).Bom(2, 4, 1m).Bom(3, 4, 2m)
            .Build();

        var result = ProductionOrderPlanner.Plan(1, 1m, catalog);

        result.Error.Should().Be(PlanError.None);
        result.Order!.Children.Select(c => c.Children.Single().Quantity).Should().Equal(1m, 2m);
    }

    // ---- cycles and depth ----------------------------------------------------------------------------

    [Fact]
    public void Plan_CyclicBom_Failure_CyclicBom()
    {
        var catalog = new PlannerCatalogBuilder()
            .Material(1, Finished).LinearRouting(1, 10)
            .Material(2, Inter).LinearRouting(2, 10)
            .Bom(1, 2, 1m).Bom(2, 1, 1m)
            .Build();

        var result = ProductionOrderPlanner.Plan(1, 1m, catalog);

        result.Order.Should().BeNull();
        result.Error.Should().Be(PlanError.CyclicBom);
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Plan_SelfReferencingBom_Failure_CyclicBom()
    {
        var catalog = new PlannerCatalogBuilder()
            .Material(1, Finished).LinearRouting(1, 10)
            .Bom(1, 1, 1m)
            .Build();

        ProductionOrderPlanner.Plan(1, 1m, catalog).Error.Should().Be(PlanError.CyclicBom);
    }

    [Fact]
    public void Plan_ChainAtMaximumDepth_Success()
    {
        var catalog = Chain(ProductionOrderPlanner.MaxDepth);

        ProductionOrderPlanner.Plan(1, 1m, catalog).Error.Should().Be(PlanError.None);
    }

    [Fact]
    public void Plan_ChainBeyondMaximumDepth_Failure_ReportedAsCyclicBom()
    {
        var catalog = Chain(ProductionOrderPlanner.MaxDepth + 1);

        var result = ProductionOrderPlanner.Plan(1, 1m, catalog);

        result.Order.Should().BeNull();
        result.Error.Should().Be(PlanError.CyclicBom);
    }

    private static PlannerCatalog Chain(int levels)
    {
        var builder = new PlannerCatalogBuilder();
        for (var id = 1; id <= levels; id++)
        {
            builder.Material(id, id == 1 ? Finished : Inter).LinearRouting(id, 10);
            if (id > 1) builder.Bom(id - 1, id, 1m);
        }
        return builder.Build();
    }

    // ---- minimum duration ----------------------------------------------------------------------------

    [Fact]
    public void EarliestFinish_LinearChain_Success_SumsDurations()
    {
        var durations = new Dictionary<int, decimal> { [1] = 10m, [2] = 5m, [3] = 20m };

        var finish = ProductionOrderPlanner.EarliestFinish(durations, [(1, 2), (2, 3)]);

        finish.Should().BeEquivalentTo(new Dictionary<int, decimal> { [1] = 10m, [2] = 15m, [3] = 35m });
    }

    [Fact]
    public void EarliestFinish_ParallelBranches_Success_FollowsTheCriticalPath()
    {
        // 1 -> 2 -> 4 (10+5+7) vs 1 -> 3 -> 4 (10+20+7)
        var durations = new Dictionary<int, decimal> { [1] = 10m, [2] = 5m, [3] = 20m, [4] = 7m };

        var finish = ProductionOrderPlanner.EarliestFinish(durations, [(1, 2), (1, 3), (2, 4), (3, 4)]);

        finish[2].Should().Be(15m);
        finish[3].Should().Be(30m);
        finish[4].Should().Be(37m);
    }

    [Fact]
    public void EarliestFinish_IndependentNodes_Success_StartAtZero()
    {
        var finish = ProductionOrderPlanner.EarliestFinish(new Dictionary<int, decimal> { [1] = 3m, [2] = 8m }, []);

        finish[1].Should().Be(3m);
        finish[2].Should().Be(8m);
    }

    [Fact]
    public void EarliestFinish_UnknownNodesInDependencies_Success_AreIgnored()
    {
        var finish = ProductionOrderPlanner.EarliestFinish(new Dictionary<int, decimal> { [1] = 3m }, [(1, 99), (98, 1)]);

        finish.Should().BeEquivalentTo(new Dictionary<int, decimal> { [1] = 3m });
    }

    [Fact]
    public void EarliestFinish_PlannedParentAndChild_Success_MinimumDurationAcrossOrders()
    {
        // child (qty 6): 10 -> 20 ; parent (qty 2): 10 gated by the child last step
        var catalog = new PlannerCatalogBuilder()
            .Material(1, Finished).Routing(1, [(10, 5m, 2m)])
            .Material(2, Inter).Routing(2, [(10, 10m, 1m), (20, 0m, 3m)], (10, 20))
            .Bom(1, 2, 3m)
            .Build();
        var root = ProductionOrderPlanner.Plan(1, 2m, catalog).Order!;
        var all = new List<PlannedWorkOrder>();
        var edges = new List<(PlannedWorkOrder, PlannedWorkOrder)>();
        void Collect(PlannedOrder o)
        {
            all.AddRange(o.WorkOrders);
            edges.AddRange(o.Dependencies);
            o.Children.ForEach(Collect);
        }
        Collect(root);
        var ids = all.Select((w, i) => (w, i)).ToDictionary(x => x.w, x => x.i);

        var finish = ProductionOrderPlanner.EarliestFinish(
            all.ToDictionary(w => ids[w], w => w.PlannedMinutes),
            edges.Select(e => (ids[e.Item1], ids[e.Item2])));

        // child: 10 -> 10+6 = 16 ; 20 -> 16 + 18 = 34 ; parent: 34 + 5 + 4 = 43
        finish.Values.Max().Should().Be(43m);
    }
}
