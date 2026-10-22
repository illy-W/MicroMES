using FluentAssertions;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiFeatures.ToolingFeature.Seeding;
using MicroMES.ApiTests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiTests.Integration.Features;

[Collection(ApiCollection.Name)]
public class ToolingSeedTests(ApiFixture fixture)
{
    private static async Task<int[]> Counts(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return
        [
            await db.Set<MicroMES.ApiCore.Persistence.Entities.Tooling>().CountAsync(),
            await db.Set<MicroMES.ApiCore.Persistence.Entities.SetupAttribute>().CountAsync(),
            await db.Set<MicroMES.ApiCore.Persistence.Entities.ToolingEquipment>().CountAsync(),
            await db.Set<MicroMES.ApiCore.Persistence.Entities.RoutingStepTooling>().CountAsync(),
            await db.Set<MicroMES.ApiCore.Persistence.Entities.RoutingStepSetupValue>().CountAsync(),
            await db.Set<MicroMES.ApiCore.Persistence.Entities.WorkOrderTooling>().CountAsync()
        ];
    }

    [Fact]
    public async Task DevelopmentStartup_FreshDatabase_Success_SeedsToolingWithSnapshotsAndIsIdempotent()
    {
        var connectionString = await fixture.CreateDatabaseAsync($"tool_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(connectionString, "Development");
        _ = factory.CreateClient();

        var counts = await Counts(factory);
        counts[0].Should().Be(4);
        counts[1].Should().Be(2);
        counts[2].Should().Be(5);
        counts[3].Should().BeGreaterThan(0);
        counts[4].Should().BeGreaterThan(0);
        counts[5].Should().BeGreaterThan(0, "orders released on a fresh database take the snapshots");

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ToolingDevelopmentSeeder>().SeedAsync();
        (await Counts(factory)).Should().Equal(counts);
    }

    [Fact]
    public async Task Seeder_AlreadySeededDatabaseWithoutTooling_Success_FillsTablesOnceWithoutSnapshots()
    {
        var connectionString = await fixture.CreateDatabaseAsync($"tool_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(connectionString, "Development");
        _ = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(
                "DELETE FROM \"WorkOrderSetupValues\"; DELETE FROM \"WorkOrderTooling\"; DELETE FROM \"RoutingStepSetupValues\"; DELETE FROM \"RoutingStepTooling\"; DELETE FROM \"ToolingEquipment\"; DELETE FROM \"Tooling\"; DELETE FROM \"SetupAttributes\";");
        (await Counts(factory)).Should().OnlyContain(c => c == 0);

        for (var i = 0; i < 2; i++)
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ToolingDevelopmentSeeder>().SeedAsync();
        }
        var counts = await Counts(factory);
        counts[0].Should().Be(4);
        counts[1].Should().Be(2);
        counts[3].Should().BeGreaterThan(0);
        counts[5].Should().Be(0, "no backfill of snapshots");
    }
}
