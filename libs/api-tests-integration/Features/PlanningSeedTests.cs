using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.PlanningFeature.Seeding;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;
using MicroMES.ApiTests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiTests.Integration.Features;

[Collection(ApiCollection.Name)]
public class PlanningSeedTests(ApiFixture fixture)
{
    private static async Task<List<(int Id, DateTime? Start, DateTime? End, DateTime? PlannedAt, int? Machine)>> PlanOf(ApiClient api)
        => (await api.GetWorkOrdersAsync()).Ok.OrderBy(w => w.Id)
            .Select(w => (w.Id, w.PlannedStartAt, w.PlannedEndAt, w.PlannedAt, w.AssignedEquipment?.Id)).ToList();

    [Fact]
    public async Task DevelopmentStartup_FreshDatabase_Success_SeedsADemonstrativePlanIdempotently()
    {
        var connectionString = await fixture.CreateDatabaseAsync($"plan_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(connectionString, "Development");
        var api = new ApiClient(factory.CreateClient());

        var orders = (await api.GetOrdersAsync(includeChildren: true)).Ok;
        orders.Where(o => o.ParentOrderId is null).Should().HaveCount(8, "the planning seed does not change orders");
        var all = (await api.GetWorkOrdersAsync()).Ok;
        var open = all.Where(w => w.Status is not (WorkOrderStatus.Ended or WorkOrderStatus.Aborted)).ToList();

        // Every open work order of an open order is planned (the running ones keep the plan they had, if any); closed ones are not.
        var plannable = open.Where(w => w.Status is WorkOrderStatus.Waiting or WorkOrderStatus.Ready or WorkOrderStatus.Interrupted
                                        && w.ProductionOrderStatus is ProductionOrderStatus.Released or ProductionOrderStatus.InProgress).ToList();
        plannable.Should().NotBeEmpty().And.OnlyContain(w => w.PlannedStartAt != null && w.PlannedEndAt != null && w.PlannedAt != null);
        all.Where(w => w.Status is WorkOrderStatus.Ended or WorkOrderStatus.Aborted).Should().OnlyContain(w => w.PlannedStartAt == null);
        all.Where(w => w.ProductionOrderStatus == ProductionOrderStatus.Planned).All(w => w.PlannedStartAt == null).Should().BeTrue();

        // A queue: a machine with several planned work orders one after the other.
        var queue = plannable.Where(w => w.AssignedEquipment != null).GroupBy(w => w.AssignedEquipment!.Id).OrderByDescending(g => g.Count()).First()
            .OrderBy(w => w.PlannedStartAt).ToList();
        queue.Count.Should().BeGreaterThanOrEqualTo(3);
        for (var i = 1; i < queue.Count; i++) queue[i].PlannedStartAt.Should().BeOnOrAfter(queue[i - 1].PlannedEndAt!.Value);

        // Phases without machines are planned too, on the default calendar.
        plannable.Should().Contain(w => w.AssignedEquipment == null);
        // The alternative machine: the planning assigned a machine to work orders that had none.
        plannable.Count(w => w.AssignedEquipment != null).Should().BeGreaterThan(0);

        // A late order and a stale plan.
        all.Should().Contain(w => w.PlanStatus == PlanStatus.Late);
        all.Should().Contain(w => w.PlanStatus == PlanStatus.Stale);
        orders.Should().Contain(o => o.PlanStatus == PlanStatus.Late || o.PlanStatus == PlanStatus.Stale);
        orders.Where(o => o.PlannedEnd != null).Should().OnlyContain(o => o.PlannedStart <= o.PlannedEnd);

        var alerts = (await api.GetPlanAlertsAsync()).Ok;
        alerts.Alerts.Should().Contain(a => a.Type == PlanAlertType.Late && a.LateDays >= 1);
        alerts.Alerts.Should().Contain(a => a.Type == PlanAlertType.StalePlan);

        // The timeline of the next days shows the plan.
        var from = DateTime.UtcNow.Date;
        var timeline = (await api.GetAsync<TimelineModel>($"/api/planning/timeline?from={from:yyyy-MM-ddTHH:mm:ssZ}&to={from.AddDays(14):yyyy-MM-ddTHH:mm:ssZ}")).Ok;
        timeline.WorkOrders.Should().NotBeEmpty();
        timeline.Equipment.Should().HaveCount(9);

        // Idempotence: a second run changes nothing.
        var before = await PlanOf(api);
        for (var i = 0; i < 2; i++)
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PlanningDevelopmentSeeder>().SeedAsync();
        }
        (await PlanOf(api)).Should().Equal(before);
        (await api.GetWorkOrdersAsync()).Ok.Should().HaveCount(all.Count);
    }

    [Fact]
    public async Task Seeder_AlreadySeededDatabaseWithoutPlan_Success_PlansOnceAndNeverOverwritesAUserPlan()
    {
        var connectionString = await fixture.CreateDatabaseAsync($"plan_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(connectionString, "Development");
        var api = new ApiClient(factory.CreateClient());

        // A database as it was before the planning: same data, no plan.
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(
                "UPDATE \"WorkOrders\" SET \"PlannedStartAt\" = NULL, \"PlannedEndAt\" = NULL, \"PlannedAt\" = NULL");
        (await api.GetWorkOrdersAsync()).Ok.Should().OnlyContain(w => w.PlannedStartAt == null);

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PlanningDevelopmentSeeder>().SeedAsync();
        var planned = await PlanOf(api);
        planned.Should().Contain(p => p.Start != null);

        // A plan changed by the user is respected: the seeder does not run when some open work order has a plan.
        var any = (await api.GetWorkOrdersAsync()).Ok.First(w => w.PlanStatus != PlanStatus.Unplanned && w.Status == WorkOrderStatus.Waiting);
        (await api.RemovePlanAsync(any.ProductionOrderId)).Ok.ClearedWorkOrders.Should().BeGreaterThan(0);
        var afterRemoval = await PlanOf(api);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PlanningDevelopmentSeeder>().SeedAsync();
        (await PlanOf(api)).Should().Equal(afterRemoval);
    }
}
