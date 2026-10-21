using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiFeatures.MaterialFeature.Models;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiTests.Integration.Shared;
using MicroMES.ApiFeatures.ProductionOrderFeature.Seeding;
using MicroMES.ApiFeatures.SampleDataFeature;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiTests.Integration.Features;

[Collection(ApiCollection.Name)]
public class MigrationsAndSeedersTests(ApiFixture fixture)
{
    [Fact]
    public async Task Migrations_AppliedToEmptyDatabase_Success_NothingPendingAndModelInSync()
    {
        var (pending, hasModelChanges) = await fixture.WithDbAsync(async db =>
            ((await db.Database.GetPendingMigrationsAsync()).ToList(), db.Database.HasPendingModelChanges()));

        pending.Should().BeEmpty("the fixture applied every migration");
        hasModelChanges.Should().BeFalse("a model change without its migration would break deployments");
    }

    [Fact]
    public async Task DevelopmentStartup_FreshDatabase_Success_AppliesMigrationsAndSeedsConsistentData()
    {
        var connectionString = await fixture.CreateDatabaseAsync($"seed_{Guid.NewGuid():N}");

        // The Development host migrates and runs both seeders on its own; any exception would surface here.
        await using var factory = new ApiFactory(connectionString, "Development");
        var api = new ApiClient(factory.CreateClient());

        var materials = (await api.GetAsync<List<MaterialModel>>("/api/materials?includeInactive=true")).Ok;
        materials.Should().HaveCount(18);
        materials.Should().ContainSingle(m => !m.IsActive, "one raw material is kept inactive on purpose");

        (await api.GetAsync<List<ProcessSegmentModel>>("/api/process-segments")).Ok.Should().HaveCount(11);

        var equipment = (await api.GetAsync<List<EquipmentModel>>("/api/equipment?includeInactive=true")).Ok;
        equipment.Should().HaveCount(9).And.OnlyContain(e => e.IsActive);
        equipment.Select(e => e.Code).Should().BeEquivalentTo(
            "TAG-TUB-1", "TAG-TUB-2", "TAG-LAM-1", "SAL-1", "SAL-2", "VER-POL-1", "VER-ACQ-1", "ASS-B1", "IMB-T1");
        equipment.Should().Contain(e => e.ProcessSegments.Count > 1, "one machine serves more than one segment");
        var segmentsWithMachines = (await api.GetAsync<List<ProcessSegmentModel>>("/api/process-segments")).Ok;
        segmentsWithMachines.Should().Contain(s => s.Equipment.Count == 0, "some segments have no constraint");
        segmentsWithMachines.Single(s => s.Code == "TAG-TUB").Equipment.Select(e => e.Code).Should().Equal("TAG-TUB-1", "TAG-TUB-2");
        segmentsWithMachines.Single(s => s.Code == "SAL").Equipment.Select(e => e.Code).Should().Equal("SAL-1", "SAL-2");

        segmentsWithMachines.Single(s => s.Code == "TAG-TUB").Equipment.Single(e => e.IsDefault).Code.Should().Be("TAG-TUB-1");
        segmentsWithMachines.Single(s => s.Code == "SAL").Equipment.Single(e => e.IsDefault).Code.Should().Be("SAL-1");
        var noDefault = segmentsWithMachines.Single(s => s.Code == "TAG-LAM");
        noDefault.Equipment.Should().HaveCountGreaterThan(1).And.OnlyContain(e => !e.IsDefault, "a multi-machine segment without default shows the manual choice");

        var topLevel = (await api.GetOrdersAsync(includeChildren: false)).Ok;
        topLevel.Select(o => o.Status).Should().BeEquivalentTo(new[]
        {
            ProductionOrderStatus.Completed, ProductionOrderStatus.Cancelled, ProductionOrderStatus.Released,
            ProductionOrderStatus.InProgress, ProductionOrderStatus.Planned,
            ProductionOrderStatus.Released, ProductionOrderStatus.Aborted, ProductionOrderStatus.InProgress
        }, "the x20 coffee-table order stays Released while only its child order (frame) is being worked; "
            + "the paused-milling order is Released; the chair order with the aborted run is Aborted; the bookshelf order has work started");

        // The seeded history must obey the state machines: a Completed order has every work order Ended.
        var completed = topLevel.Single(o => o.Status == ProductionOrderStatus.Completed);
        completed.ProgressPercent.Should().Be(100);
        (await api.GetWorkOrdersAsync(completed.Id)).Ok
            .Should().NotBeEmpty().And.OnlyContain(w => w.Status == WorkOrderStatus.Ended);

        var running = (await api.GetAsync<List<WorkOrderModel>>($"/api/work-orders?status={WorkOrderStatus.Running}")).Ok;
        running.Should().HaveCount(4, "frame, chair cutting, bookshelf shelf finishing and bookshelf side-panel welding");

        // Machines: every running work order is on a machine; the chair plate cut on TAG-TUB-2, also the candidate of a Ready one.
        running.Should().OnlyContain(w => w.CurrentEquipment != null && w.AssignedEquipment != null && w.CurrentEquipment.Id == w.AssignedEquipment.Id);
        var inUse = running.Single(w => w.ProcessSegmentCode == "TAG-LAM");
        inUse.CurrentEquipment!.Code.Should().Be("TAG-TUB-2");
        var ready = (await api.GetAsync<List<WorkOrderModel>>($"/api/work-orders?status={WorkOrderStatus.Ready}&equipmentId={inUse.CurrentEquipment.Id}")).Ok;
        ready.Should().ContainSingle(w => w.ProcessSegmentCode == "TAG-TUB");

        // Sessions of the completed order carry the machine; welding ran on SAL-2 first, the work order ended assigned to SAL-1.
        var doneOrder = topLevel.Single(o => o.Status == ProductionOrderStatus.Completed);
        var welding = (await api.GetWorkOrderAsync((await api.GetWorkOrdersAsync(doneOrder.Id)).Ok.Single(w => w.OperationNumber == 30).Id)).Ok;
        welding.AssignedEquipment!.Code.Should().Be("SAL-1");
        welding.Sessions.Select(s => s.EquipmentCode).Should().Equal("SAL-2", "SAL-1");
        var cutting = (await api.GetWorkOrdersAsync(doneOrder.Id)).Ok.Single(w => w.OperationNumber == 20);
        cutting.AssignedEquipment!.Code.Should().Be("TAG-LAM-1");
    }

    [Fact]
    public async Task DevelopmentStartup_FreshDatabase_Success_SeedsEveryOverviewScenarioAndIsIdempotent()
    {
        var connectionString = await fixture.CreateDatabaseAsync($"seed_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(connectionString, "Development");
        var api = new ApiClient(factory.CreateClient());

        var orders = (await api.GetOrdersAsync(includeChildren: true)).Ok;
        var top = orders.Where(o => o.ParentOrderId is null).OrderBy(o => o.Id).ToList();
        top.Should().HaveCount(8);

        ProductionOrderModel Top(string material, decimal qty) => top.Single(o => o.MaterialCode == material && o.Quantity == qty);

        Top("PF-SED-01", 4).Status.Should().Be(ProductionOrderStatus.Completed);
        Top("PF-TAV-60", 5).Status.Should().Be(ProductionOrderStatus.Cancelled);
        var tav = Top("PF-TAV-60", 20);
        tav.Status.Should().Be(ProductionOrderStatus.Released);
        var chairs = Top("PF-SED-01", 12);
        chairs.Status.Should().Be(ProductionOrderStatus.InProgress);
        var planned = Top("PF-TAV-60", 10);
        planned.Status.Should().Be(ProductionOrderStatus.Planned);
        var paused = Top("PF-TAV-60", 8);
        paused.Status.Should().Be(ProductionOrderStatus.Released);
        var aborted = Top("PF-SED-01", 2);
        aborted.Status.Should().Be(ProductionOrderStatus.Aborted);

        // Coffee tables x20: 3 orders (top + 2 children), 10 work orders, 9 dependencies (2 cross-order).
        orders.Where(o => o.ParentOrderId == tav.Id).Select(o => o.Status).Should()
            .BeEquivalentTo(new[] { ProductionOrderStatus.InProgress, ProductionOrderStatus.Released });
        var ov = (await api.GetOrderOverviewAsync(tav.Id)).Ok;
        ov.Orders.Should().HaveCount(3);
        ov.WorkOrders.Should().HaveCount(10);
        ov.Dependencies.Should().HaveCount(9);
        var orderOf = ov.WorkOrders.ToDictionary(w => w.Id, w => w.ProductionOrderId);
        ov.Dependencies.Count(d => orderOf[d.PredecessorWorkOrderId] != orderOf[d.SuccessorWorkOrderId]).Should().Be(2);
        ov.Summary.Counts.Waiting.Should().Be(5);
        ov.Summary.Counts.Ready.Should().Be(1);
        ov.Summary.Counts.Running.Should().Be(1);
        ov.Summary.Counts.Ended.Should().Be(3);
        ov.Summary.CriticalPathWorkOrderIds.Should().NotBeEmpty();

        // Chairs x12: no children, two start work orders feeding welding, one Ended and one Running.
        var cov = (await api.GetOrderOverviewAsync(chairs.Id)).Ok;
        cov.Orders.Should().ContainSingle();
        var welding = cov.WorkOrders.Single(w => w.OperationNumber == 30).Id;
        var feeders = cov.Dependencies.Where(d => d.SuccessorWorkOrderId == welding).Select(d => d.PredecessorWorkOrderId).ToList();
        feeders.Should().HaveCount(2);
        cov.WorkOrders.Where(w => feeders.Contains(w.Id)).Select(w => w.Status).Should()
            .BeEquivalentTo(new[] { WorkOrderStatus.Ended, WorkOrderStatus.Running });

        // Planned: 200 with no work orders.
        var pov = (await api.GetOrderOverviewAsync(planned.Id)).Ok;
        pov.WorkOrders.Should().BeEmpty();

        // Completed: every work order Ended.
        (await api.GetOrderOverviewAsync(Top("PF-SED-01", 4).Id)).Ok.WorkOrders
            .Should().NotBeEmpty().And.OnlyContain(w => w.Status == WorkOrderStatus.Ended);

        // Interrupted and Aborted work orders are shown too.
        (await api.GetOrderOverviewAsync(paused.Id)).Ok.WorkOrders
            .Should().Contain(w => w.Status == WorkOrderStatus.Interrupted);
        (await api.GetOrderOverviewAsync(aborted.Id)).Ok.WorkOrders
            .Should().Contain(w => w.Status == WorkOrderStatus.Aborted);

        // Bookshelves x6 (OP-2026-0008): the side panels (child order) are consumed only at assembly (op 30).
        var shelves = Top("PF-LIB-01", 6);
        shelves.Code.Should().Be("OP-2026-0008");
        shelves.Status.Should().Be(ProductionOrderStatus.InProgress);
        var sov = (await api.GetOrderOverviewAsync(shelves.Id)).Ok;
        sov.Orders.Should().HaveCount(2);
        sov.WorkOrders.Should().HaveCount(8, "five bookshelf steps and three side-panel steps");
        var shelfWo = sov.WorkOrders.Where(w => w.ProductionOrderId == shelves.Id).ToDictionary(w => w.OperationNumber);
        var panelOrder = sov.Orders.Single(o => o.ParentOrderId == shelves.Id);
        var panelLast = sov.WorkOrders.Single(w => w.ProductionOrderId == panelOrder.Id && w.OperationNumber == 30).Id;
        sov.Dependencies.Where(d => d.PredecessorWorkOrderId == panelLast).Select(d => d.SuccessorWorkOrderId)
            .Should().Equal([shelfWo[30].Id], "the child feeds the third step, not the first");
        sov.Dependencies.Should().HaveCount(4 + 2 + 1);
        shelfWo[10].Status.Should().Be(WorkOrderStatus.Ended);
        shelfWo[20].Status.Should().Be(WorkOrderStatus.Running);
        shelfWo[30].Status.Should().Be(WorkOrderStatus.Waiting);
        // Raw components land on different steps; the intermediate on the assembly.
        shelfWo[10].Materials.Select(m => m.MaterialCode).Should().Equal("TAV-ROV-25");
        shelfWo[20].Materials.Select(m => m.MaterialCode).Should().Equal("VER-ACQ-TRA");
        shelfWo[30].Materials.Select(m => m.MaterialCode).Should().BeEquivalentTo("SL-LIB-FIA", "VIT-M6-30", "PIE-GOM-30");
        shelfWo[30].Materials.Single(m => m.MaterialCode == "SL-LIB-FIA").ChildProductionOrderId.Should().Be(panelOrder.Id);
        shelfWo[50].Materials.Select(m => m.MaterialCode).Should().Equal("IMB-SCA-60");

        // Coffee tables: only the packaging box is linked (to the packing step); the structure/top stay on assembly.
        var tavWo = ov.WorkOrders.Where(w => w.ProductionOrderId == tav.Id).ToDictionary(w => w.OperationNumber);
        tavWo[10].Materials.Select(m => m.MaterialCode).Should().BeEquivalentTo("SL-TAV-STR", "SL-TAV-PIA", "VIT-M6-30", "PIE-GOM-30");
        tavWo[30].Materials.Select(m => m.MaterialCode).Should().Equal("IMB-SCA-60");

        // Chairs: tubes at cutting, sheet at plate cutting, hardware at assembly.
        cov.WorkOrders.Single(w => w.OperationNumber == 10).Materials.Select(m => m.MaterialCode).Should().Equal("TUB-Q30-2");
        cov.WorkOrders.Single(w => w.OperationNumber == 20).Materials.Select(m => m.MaterialCode).Should().Equal("LAM-S235-2");
        cov.WorkOrders.Single(w => w.OperationNumber == 60).Materials.Select(m => m.MaterialCode)
            .Should().BeEquivalentTo("TAV-ROV-25", "VIT-M6-30", "PIE-GOM-30");

        var workOrderCount = (await api.GetWorkOrdersAsync()).Ok.Count;

        // Idempotence: a second run of both seeders adds nothing.
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<MasterDataDevelopmentSeeder>().SeedAsync();
            await scope.ServiceProvider.GetRequiredService<ProductionOrderDevelopmentSeeder>().SeedAsync();
        }
        (await api.GetOrdersAsync(includeChildren: true)).Ok.Should().HaveCount(orders.Count);
        (await api.GetAsync<List<MaterialModel>>("/api/materials?includeInactive=true")).Ok.Should().HaveCount(18);
        (await api.GetWorkOrdersAsync()).Ok.Should().HaveCount(workOrderCount);
        (await api.GetAsync<List<EquipmentModel>>("/api/equipment?includeInactive=true")).Ok.Should().HaveCount(9);
    }
}
