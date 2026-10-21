using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.MaterialFeature.Models;
using MicroMES.ApiFeatures.MaterialFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.MaterialFeature;

public class MaterialServiceFixture : BaseFeatureFixture
{
    private static CreateMaterialRequest NewRequest(string code, string name = "Name") => new()
    {
        Code = code, Name = name, Type = MaterialType.RawMaterial, UnitOfMeasure = UnitOfMeasure.Kilogram
    };

    private static UpdateMaterialRequest UpdateRequest(string code, bool active = true) => new()
    {
        Code = code, Name = "Name", Type = MaterialType.RawMaterial, UnitOfMeasure = UnitOfMeasure.Piece, IsActive = active
    };

    [Fact]
    public async Task CreateAsync_CodeWithSpacesAndLowerCase_Success_NormalizesCodeAndTrimsName()
    {
        var result = await Service<MaterialService>().CreateAsync(NewRequest("  tub-q30  ", "  Tubolare  "));

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Code.Should().Be("TUB-Q30");
        result.Data.Name.Should().Be("Tubolare");
        result.Data.IsActive.Should().BeTrue();
        Query(db => db.Materials.Single().Code).Should().Be("TUB-Q30");
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("abc")]
    [InlineData("  Abc ")]
    public async Task CreateAsync_DuplicateCode_Failure_ConflictWhateverTheCase(string code)
    {
        AddMaterial("ABC");

        var result = await Service<MaterialService>().CreateAsync(NewRequest(code));

        result.IsSuccessful.Should().BeFalse();
        result.Failure.Should().Be(MaterialFailure.Conflict);
        Query(db => db.Materials.Count()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_CodeOfInactiveMaterial_Failure_ConflictBecauseTheIndexCoversIt()
    {
        AddMaterial("OLD", active: false);

        var result = await Service<MaterialService>().CreateAsync(NewRequest("OLD"));

        result.Failure.Should().Be(MaterialFailure.Conflict);
    }

    [Fact]
    public async Task UpdateAsync_SameCodeOnItself_Success()
    {
        var id = AddMaterial("ABC");

        var result = await Service<MaterialService>().UpdateAsync(id, UpdateRequest("abc"));

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Code.Should().Be("ABC");
    }

    [Fact]
    public async Task UpdateAsync_CodeOfAnotherMaterial_Failure_Conflict()
    {
        AddMaterial("ABC");
        var id = AddMaterial("XYZ");

        var result = await Service<MaterialService>().UpdateAsync(id, UpdateRequest("abc"));

        result.Failure.Should().Be(MaterialFailure.Conflict);
        Query(db => db.Materials.Single(m => m.Id == id).Code).Should().Be("XYZ");
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_Failure_NotFound()
    {
        var result = await Service<MaterialService>().UpdateAsync(999, UpdateRequest("ABC"));

        result.Failure.Should().Be(MaterialFailure.NotFound);
    }

    [Fact]
    public async Task DeactivateAsync_ActiveMaterial_Success_SoftDeletesAndIsIdempotent()
    {
        var id = AddMaterial("ABC");

        var first = await Service<MaterialService>().DeactivateAsync(id);
        var second = await Service<MaterialService>().DeactivateAsync(id);

        first.IsSuccessful.Should().BeTrue();
        first.Data!.IsActive.Should().BeFalse();
        second.IsSuccessful.Should().BeTrue();
        Query(db => db.Materials.Count(m => m.Id == id)).Should().Be(1, "the record is never deleted");
    }

    [Fact]
    public async Task GetAllAsync_ByDefault_Success_ExcludesInactiveAndSortsByCode()
    {
        AddMaterial("B");
        AddMaterial("A");
        AddMaterial("Z", active: false);

        var active = await Service<MaterialService>().GetAllAsync(includeInactive: false);
        var all = await Service<MaterialService>().GetAllAsync(includeInactive: true);

        active.Data!.Select(m => m.Code).Should().Equal("A", "B");
        all.Data!.Select(m => m.Code).Should().Equal("A", "B", "Z");
    }
}
