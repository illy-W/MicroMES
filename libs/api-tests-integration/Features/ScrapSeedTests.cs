using FluentAssertions;
using MicroMES.ApiFeatures.ProductionOrderFeature.Seeding;
using MicroMES.ApiFeatures.SampleDataFeature;
using MicroMES.ApiFeatures.ScrapFeature.Models;
using MicroMES.ApiTests.Integration.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiTests.Integration.Features;

[Collection(ApiCollection.Name)]
public class ScrapSeedTests(ApiFixture fixture)
{
    [Fact]
    public async Task DevelopmentStartup_FreshDatabase_Success_SeedsScrapCatalogRowsCorrectionAndIsIdempotent()
    {
        var connectionString = await fixture.CreateDatabaseAsync($"seed_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(connectionString, "Development");
        var api = new ApiClient(factory.CreateClient());

        var reasons = (await api.GetAsync<List<ScrapReasonModel>>("/api/scrap-reasons?includeInactive=true")).Ok;
        reasons.Should().HaveCount(9);
        reasons.Should().Contain(r => !r.IsActive);
        reasons.Should().Contain(r => r.ProcessSegments.Count == 0, "some reasons are valid for every segment");
        reasons.Should().Contain(r => r.ProcessSegments.Count > 0);

        var report = (await api.GetScrapReportAsync()).Ok;
        report.ScrapQuantity.Should().BeGreaterThan(0);
        report.ByReason.Count(r => r.Quantity > 0).Should().BeGreaterThanOrEqualTo(5);
        report.ByReason.Single(r => r.ScrapReasonId == null).Quantity.Should().BeGreaterThan(0, "one seeded session has scrap without reason");
        report.ByReason.Sum(r => r.Quantity).Should().Be(report.ScrapQuantity);
        report.ByProcessSegment.Should().NotBeEmpty();
        report.ByMaterial.Should().NotBeEmpty();

        // Every seeded session has rows that add up to its scrap; one session has several reasons; one was corrected.
        var sessions = (await api.GetWorkOrdersAsync()).Ok.Select(w => api.GetWorkOrderAsync(w.Id)).ToList();
        var details = (await Task.WhenAll(sessions)).Select(r => r.Ok).SelectMany(d => d.Sessions).ToList();
        details.Should().OnlyContain(s => s.ScrapEntries.Sum(e => e.Quantity) == s.ScrapQuantity);
        details.Should().Contain(s => s.ScrapEntries.Count(e => e.ScrapReasonId != null) >= 2);
        details.Where(s => s.IsCorrected).Should().ContainSingle();

        // Idempotence.
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<MasterDataDevelopmentSeeder>().SeedAsync();
            await scope.ServiceProvider.GetRequiredService<ProductionOrderDevelopmentSeeder>().SeedAsync();
        }
        (await api.GetAsync<List<ScrapReasonModel>>("/api/scrap-reasons?includeInactive=true")).Ok.Should().HaveCount(9);
        (await api.GetScrapReportAsync()).Ok.ScrapQuantity.Should().Be(report.ScrapQuantity);
    }
}
