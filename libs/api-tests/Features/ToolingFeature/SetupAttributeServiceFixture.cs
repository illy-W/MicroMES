using FluentAssertions;
using MicroMES.ApiFeatures.ToolingFeature.Models;
using MicroMES.ApiFeatures.ToolingFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.ToolingFeature;

public class SetupAttributeServiceFixture : BaseFeatureFixture
{
    private static CreateSetupAttributeRequest New(string code, int? minutes = 10)
        => new() { Code = code, Name = " Colore ", ChangeMinutes = minutes };

    [Fact]
    public async Task CreateAsync_LowerCaseCode_Success_Normalized()
    {
        var result = await Service<SetupAttributeService>().CreateAsync(New(" col "));

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        result.Data!.Code.Should().Be("COL");
        result.Data.Name.Should().Be("Colore");
        result.Data.ChangeMinutes.Should().Be(10);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_Failure_Conflict()
    {
        await Service<SetupAttributeService>().CreateAsync(New("COL"));

        var result = await Service<SetupAttributeService>().CreateAsync(New("col"));

        result.Failure.Should().Be(ToolingFailure.Conflict);
        Query(db => db.SetupAttributes.Count()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_NegativeMinutes_Failure_Invalid()
    {
        var result = await Service<SetupAttributeService>().CreateAsync(New("COL", -1));

        result.Failure.Should().Be(ToolingFailure.Invalid);
    }

    [Fact]
    public async Task UpdateDeactivateReactivate_Success_AndNotFound()
    {
        var id = (await Service<SetupAttributeService>().CreateAsync(New("COL"))).Data!.Id;

        var updated = await Service<SetupAttributeService>().UpdateAsync(id,
            new UpdateSetupAttributeRequest { Code = "dim", Name = "Dimensione", ChangeMinutes = 25, IsActive = true });
        updated.Data!.Code.Should().Be("DIM");
        updated.Data.ChangeMinutes.Should().Be(25);

        (await Service<SetupAttributeService>().DeactivateAsync(id)).Data!.IsActive.Should().BeFalse();
        (await Service<SetupAttributeService>().GetAllAsync(null, true)).Data!.Should().BeEmpty();
        (await Service<SetupAttributeService>().ReactivateAsync(id)).Data!.IsActive.Should().BeTrue();
        (await Service<SetupAttributeService>().GetAsync(404)).Failure.Should().Be(ToolingFailure.NotFound);
    }
}
