using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.MaterialFeature.Models;
using MicroMES.ApiTests.Integration.Shared;
using Microsoft.EntityFrameworkCore;

namespace MicroMES.ApiTests.Integration.Features;

public class MaterialApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task Material_CreateReadUpdateDeactivate_Success_FullCrudCycle()
    {
        var created = await Api.CreateMaterialRawAsync("  tub-30 ", MaterialType.RawMaterial, "Tubolare");
        created.Status.Should().Be(HttpStatusCode.Created);
        created.Ok.Code.Should().Be("TUB-30", "codes are trimmed and upper-cased");
        var id = created.Ok.Id;

        (await Api.GetAsync<MaterialModel>($"/api/materials/{id}")).Ok.Name.Should().Be("Tubolare");

        var updated = await Api.PutAsync<MaterialModel>($"/api/materials/{id}", new UpdateMaterialRequest
        {
            Code = "TUB-30", Name = "Tubolare 30x30", Type = MaterialType.RawMaterial,
            UnitOfMeasure = UnitOfMeasure.Meter, IsActive = true
        });
        updated.Ok.Name.Should().Be("Tubolare 30x30");
        updated.Ok.UnitOfMeasure.Should().Be(UnitOfMeasure.Meter);

        (await Api.DeleteAsync($"/api/materials/{id}")).Status.Should().Be(HttpStatusCode.NoContent);

        (await Api.GetAsync<List<MaterialModel>>("/api/materials")).Ok.Should().BeEmpty("inactive materials are hidden by default");
        var all = (await Api.GetAsync<List<MaterialModel>>("/api/materials?includeInactive=true")).Ok;
        all.Should().ContainSingle().Which.IsActive.Should().BeFalse("soft-delete keeps the row");
    }

    [Fact]
    public async Task Material_DuplicateCode_Failure_409AndNoSecondRow()
    {
        await Api.CreateMaterialAsync("DUP", MaterialType.RawMaterial);

        var duplicate = await Api.CreateMaterialRawAsync("dup", MaterialType.Consumable);

        duplicate.Status.Should().Be(HttpStatusCode.Conflict);
        duplicate.Raw.Should().Contain("DUP");
        (await Api.GetAsync<List<MaterialModel>>("/api/materials?includeInactive=true")).Ok.Should().HaveCount(1);
    }

    [Fact]
    public async Task Material_InvalidBody_Failure_422()
    {
        var response = await Api.CreateMaterialRawAsync("", MaterialType.RawMaterial);

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Material_ParallelCreateWithSameCode_Failure_UniqueIndexLetsOnlyOneThrough()
    {
        var calls = Enumerable.Range(0, 6).Select(_ => Api.CreateMaterialRawAsync("RACE", MaterialType.RawMaterial));

        var results = await Task.WhenAll(calls);

        results.Count(r => r.Status == HttpStatusCode.Created).Should().Be(1);
        results.Where(r => r.Status != HttpStatusCode.Created).Should().OnlyContain(r => !r.IsSuccess);
        var rows = await Fixture.WithDbAsync(db => db.Materials.CountAsync(m => m.Code == "RACE"));
        rows.Should().Be(1, "the unique index is the last line of defence against the check-then-insert race");
    }
}
