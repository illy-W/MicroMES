using FluentAssertions;
using MicroMES.Api;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiFeatures.MaterialFeature.Models;
using MicroMES.ApiFeatures.VersionFeature.Models;
using MicroMES.ApiTests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace MicroMES.ApiTests.Integration.Features;

[Collection(ApiCollection.Name)]
public class StartupCommandsTests(ApiFixture fixture)
{
    private static int Pending(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetPendingMigrations().Count();
    }

    [Fact]
    public async Task MigrateCommand_EmptyDatabase_Success_AppliesMigrationsAndReturnsZero()
    {
        var cs = await fixture.CreateDatabaseAsync($"mig_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(cs, "Production", new Dictionary<string, string?> { ["Database:AutoMigrate"] = "false" });

        Pending(factory).Should().BeGreaterThan(0);
        DatabaseStartup.MigrateCommand(factory.Services).Should().Be(0);
        Pending(factory).Should().Be(0);
    }

    [Fact]
    public async Task MigrateCommand_AlreadyMigratedDatabase_Success_IsIdempotent()
    {
        var cs = await fixture.CreateDatabaseAsync($"mig_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(cs, "Production", new Dictionary<string, string?> { ["Database:AutoMigrate"] = "false" });

        DatabaseStartup.MigrateCommand(factory.Services).Should().Be(0);
        DatabaseStartup.MigrateCommand(factory.Services).Should().Be(0);
        Pending(factory).Should().Be(0);
    }

    [Fact]
    public async Task MigrateCommand_UnreachableDatabase_Failure_ReturnsOne()
    {
        await using var factory = new ApiFactory("Host=127.0.0.1;Port=1;Database=x;Timeout=2;Command Timeout=2", "Production");
        DatabaseStartup.MigrateCommand(factory.Services).Should().Be(1);
    }

    [Fact]
    public async Task Health_Production_Success_LiveAndReadyAnswer200_AndAreNotInTheContract()
    {
        var cs = await fixture.CreateDatabaseAsync($"hl_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(cs, "Production");
        var http = factory.CreateClient();

        (await http.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await http.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_UnreachableDatabase_Failure_ReadyAnswers503ButLiveStays200()
    {
        await using var factory = new ApiFactory("Host=127.0.0.1;Port=1;Database=x;Timeout=2", "Production");
        var http = factory.CreateClient();

        (await http.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await http.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Version_Production_Success_AnswersWithoutNetworkWhenCheckDisabled()
    {
        var cs = await fixture.CreateDatabaseAsync($"ver_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(cs, "Production", new Dictionary<string, string?> { ["Updates:Check"] = "false" });
        var api = new ApiClient(factory.CreateClient());

        var v = (await api.GetAsync<VersionModel>("/api/version")).Ok;
        v.Version.Should().NotBeNullOrEmpty();
        v.UpdateCheck.Should().Be("disabled");
        v.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task DemoSeed_ProductionWithoutFlag_Success_LeavesDatabaseEmpty()
    {
        var cs = await fixture.CreateDatabaseAsync($"demo_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(cs, "Production");
        DatabaseStartup.Migrate(factory.Services);
        var api = new ApiClient(factory.CreateClient());

        (await api.GetAsync<List<MaterialModel>>("/api/materials?includeInactive=true")).Ok.Should().BeEmpty();
    }

    [Fact]
    public async Task DemoSeed_ProductionWithFlag_Success_SeedsEmptyDatabaseOnlyOnce()
    {
        var cs = await fixture.CreateDatabaseAsync($"demo_{Guid.NewGuid():N}");
        var settings = new Dictionary<string, string?> { ["Demo:Seed"] = "true" };

        await using (var factory = new ApiFactory(cs, "Production", settings))
        {
            var api = new ApiClient(factory.CreateClient());
            (await api.GetAsync<List<MaterialModel>>("/api/materials?includeInactive=true")).Ok.Should().HaveCount(18);
        }

        await using (var factory = new ApiFactory(cs, "Production", settings))
        {
            var api = new ApiClient(factory.CreateClient());
            (await api.GetAsync<List<MaterialModel>>("/api/materials?includeInactive=true")).Ok.Should().HaveCount(18);
        }
    }

    [Fact]
    public async Task AutoMigrate_EmptyDatabase_Success_BecomesReadyWithSchema()
    {
        var cs = await fixture.CreateDatabaseAsync($"auto_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(cs, "Production");
        var http = factory.CreateClient();

        (await http.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
        Pending(factory).Should().Be(0);
        var api = new ApiClient(http);
        (await api.GetAsync<List<MaterialModel>>("/api/materials?includeInactive=true")).Ok.Should().BeEmpty();
    }

    [Fact]
    public async Task AutoMigrate_AlreadyMigratedDatabase_Success_StaysReady()
    {
        var cs = await fixture.CreateDatabaseAsync($"auto_{Guid.NewGuid():N}");
        await using (var first = new ApiFactory(cs, "Production"))
            (await first.CreateClient().GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var factory = new ApiFactory(cs, "Production");
        (await factory.CreateClient().GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AutoMigrate_Disabled_Failure_ReadyIs503WhileMigrationsArePendingThenReadyAfterMigrate()
    {
        var cs = await fixture.CreateDatabaseAsync($"auto_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(cs, "Production", new Dictionary<string, string?> { ["Database:AutoMigrate"] = "false" });
        var http = factory.CreateClient();

        Pending(factory).Should().BeGreaterThan(0);
        (await http.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await http.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        DatabaseStartup.MigrateCommand(factory.Services).Should().Be(0);
        (await http.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
