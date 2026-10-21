using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.MaterialFeature.Models;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiTests.Integration.Shared;
using Microsoft.EntityFrameworkCore;

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
        materials.Should().HaveCount(16);
        materials.Should().ContainSingle(m => !m.IsActive, "one raw material is kept inactive on purpose");

        (await api.GetAsync<List<ProcessSegmentModel>>("/api/process-segments")).Ok.Should().HaveCount(11);

        var topLevel = (await api.GetOrdersAsync(includeChildren: false)).Ok;
        topLevel.Select(o => o.Status).Should().BeEquivalentTo(new[]
        {
            ProductionOrderStatus.Completed, ProductionOrderStatus.Cancelled, ProductionOrderStatus.Released,
            ProductionOrderStatus.InProgress, ProductionOrderStatus.Planned
        }, "the coffee-table order stays Released while only its child order (frame) is being worked");

        // The seeded history must obey the state machines: a Completed order has every work order Ended.
        var completed = topLevel.Single(o => o.Status == ProductionOrderStatus.Completed);
        completed.ProgressPercent.Should().Be(100);
        (await api.GetWorkOrdersAsync(completed.Id)).Ok
            .Should().NotBeEmpty().And.OnlyContain(w => w.Status == WorkOrderStatus.Ended);

        var running = (await api.GetAsync<List<WorkOrderModel>>($"/api/work-orders?status={WorkOrderStatus.Running}")).Ok;
        running.Should().HaveCount(2, "one work order runs on the frame child order and one on the chair order");
    }
}
