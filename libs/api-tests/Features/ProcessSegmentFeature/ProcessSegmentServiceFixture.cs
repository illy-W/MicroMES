using FluentAssertions;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Models;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.ProcessSegmentFeature;

public class ProcessSegmentServiceFixture : BaseFeatureFixture
{
    private static CreateProcessSegmentRequest NewRequest(string code) => new()
    {
        Code = code, Name = " Saldatura ", SetupMinutes = 15, RunMinutesPerUnit = 12.5m
    };

    private static UpdateProcessSegmentRequest UpdateRequest(string code) => new()
    {
        Code = code, Name = "Saldatura", SetupMinutes = 1, RunMinutesPerUnit = 2, IsActive = true
    };

    [Fact]
    public async Task CreateAsync_CodeWithSpacesAndLowerCase_Success_NormalizesCodeAndTrimsName()
    {
        var result = await Service<ProcessSegmentService>().CreateAsync(NewRequest(" sal-1 "));

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Code.Should().Be("SAL-1");
        result.Data.Name.Should().Be("Saldatura");
        result.Data.RunMinutesPerUnit.Should().Be(12.5m);
    }

    [Theory]
    [InlineData("SAL")]
    [InlineData("sal")]
    public async Task CreateAsync_DuplicateCode_Failure_Conflict(string code)
    {
        AddSegment("SAL");

        var result = await Service<ProcessSegmentService>().CreateAsync(NewRequest(code));

        result.Failure.Should().Be(ProcessSegmentFailure.Conflict);
        Query(db => db.ProcessSegments.Count()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_CodeOfInactiveSegment_Failure_Conflict()
    {
        AddSegment("SAL", active: false);

        var result = await Service<ProcessSegmentService>().CreateAsync(NewRequest("SAL"));

        result.Failure.Should().Be(ProcessSegmentFailure.Conflict);
    }

    [Fact]
    public async Task UpdateAsync_CodeOfAnotherSegment_Failure_Conflict()
    {
        AddSegment("SAL");
        var id = AddSegment("SBA");

        var result = await Service<ProcessSegmentService>().UpdateAsync(id, UpdateRequest("sal"));

        result.Failure.Should().Be(ProcessSegmentFailure.Conflict);
    }

    [Fact]
    public async Task UpdateAsync_SameCodeOnItself_Success()
    {
        var id = AddSegment("SAL");

        var result = await Service<ProcessSegmentService>().UpdateAsync(id, UpdateRequest("sal"));

        result.IsSuccessful.Should().BeTrue();
        result.Data!.SetupMinutes.Should().Be(1);
    }

    [Fact]
    public async Task DeactivateAsync_UnknownId_Failure_NotFound()
    {
        var result = await Service<ProcessSegmentService>().DeactivateAsync(404);

        result.Failure.Should().Be(ProcessSegmentFailure.NotFound);
    }

    [Fact]
    public async Task DeactivateAsync_ActiveSegment_Success_SoftDeletes()
    {
        var id = AddSegment("SAL");

        var result = await Service<ProcessSegmentService>().DeactivateAsync(id);

        result.IsSuccessful.Should().BeTrue();
        Query(db => db.ProcessSegments.Single(s => s.Id == id).IsActive).Should().BeFalse();
    }
}
