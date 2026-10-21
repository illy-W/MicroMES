using FluentAssertions;
using MicroMES.ApiFeatures.ScrapFeature.Models;
using MicroMES.ApiFeatures.ScrapFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.ScrapFeature;

public class ScrapReasonServiceFixture : BaseFeatureFixture
{
    private static CreateScrapReasonRequest New(string code, params int[] segments)
        => new() { Code = code, Name = " Graffio ", ProcessSegmentIds = segments.ToList() };

    private static UpdateScrapReasonRequest Upd(string code, bool active, params int[] segments)
        => new() { Code = code, Name = "Graffio", IsActive = active, ProcessSegmentIds = segments.ToList() };

    [Fact]
    public async Task CreateAsync_LowerCaseCodeWithSegments_Success_NormalizedAndLinked()
    {
        var sal = AddSegment("SAL");

        var result = await Service<ScrapReasonService>().CreateAsync(New(" gr ", sal));

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Code.Should().Be("GR");
        result.Data.Name.Should().Be("Graffio");
        result.Data.ProcessSegments.Should().ContainSingle().Which.Code.Should().Be("SAL");
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_Failure_Conflict()
    {
        await Service<ScrapReasonService>().CreateAsync(New("GR"));

        var result = await Service<ScrapReasonService>().CreateAsync(New("gr"));

        result.Failure.Should().Be(ScrapFailure.Conflict);
        Query(db => db.ScrapReasons.Count()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_UnknownSegment_Failure_Invalid()
    {
        var result = await Service<ScrapReasonService>().CreateAsync(New("GR", 404));

        result.Failure.Should().Be(ScrapFailure.Invalid);
    }

    [Fact]
    public async Task GetAllAsync_FilterBySegment_Success_LinkedAndGlobalOnly()
    {
        var sal = AddSegment("SAL");
        var ver = AddSegment("VER");
        await Service<ScrapReasonService>().CreateAsync(New("GLOBAL"));
        await Service<ScrapReasonService>().CreateAsync(New("ONLY-SAL", sal));
        await Service<ScrapReasonService>().CreateAsync(New("ONLY-VER", ver));

        var result = await Service<ScrapReasonService>().GetAllAsync(false, sal);

        result.Data!.Select(r => r.Code).Should().Equal("GLOBAL", "ONLY-SAL");
    }

    [Fact]
    public async Task UpdateAsync_ChangesLinksAndCode_Success()
    {
        var sal = AddSegment("SAL");
        var ver = AddSegment("VER");
        var id = (await Service<ScrapReasonService>().CreateAsync(New("GR", sal))).Data!.Id;

        var result = await Service<ScrapReasonService>().UpdateAsync(id, Upd("gr2", true, ver));

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        Query(db => db.ScrapReasons.Single().Code).Should().Be("GR2");
        result.Data!.Code.Should().Be("GR2");
        result.Data.ProcessSegments.Select(s => s.Code).Should().Equal("VER");
    }

    [Fact]
    public async Task DeactivateAsync_ActiveReason_Success_SoftDeletesAndListedOnlyWithInactive()
    {
        var id = (await Service<ScrapReasonService>().CreateAsync(New("GR"))).Data!.Id;

        var result = await Service<ScrapReasonService>().DeactivateAsync(id);

        result.IsSuccessful.Should().BeTrue();
        Query(db => db.ScrapReasons.Single(r => r.Id == id).IsActive).Should().BeFalse();
        (await Service<ScrapReasonService>().GetAllAsync(false, null)).Data.Should().BeEmpty();
        (await Service<ScrapReasonService>().GetAllAsync(true, null)).Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeactivateAsync_UnknownId_Failure_NotFound()
        => (await Service<ScrapReasonService>().DeactivateAsync(404)).Failure.Should().Be(ScrapFailure.NotFound);
}
