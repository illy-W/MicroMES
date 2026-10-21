using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.BomFeature.Models;
using MicroMES.ApiFeatures.MaterialFeature.Models;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

public class BomApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task Bom_TwoLevelTree_Success_NestsComponentsAndKeepsInactiveOnes()
    {
        var finished = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var semi = await Api.CreateMaterialAsync("SL", MaterialType.Intermediate);
        var raw = await Api.CreateMaterialAsync("RAW", MaterialType.RawMaterial);
        (await Api.AddBomItemAsync(finished.Id, semi.Id, 2)).Status.Should().Be(HttpStatusCode.Created);
        (await Api.AddBomItemAsync(semi.Id, raw.Id, 4.5m)).Status.Should().Be(HttpStatusCode.Created);

        var tree = (await Api.GetAsync<List<BomNodeModel>>($"/api/materials/{finished.Id}/bom")).Ok;

        var semiNode = tree.Should().ContainSingle().Subject;
        semiNode.Code.Should().Be("SL");
        semiNode.Quantity.Should().Be(2);
        semiNode.Children.Should().ContainSingle().Which.Should().Match<BomNodeModel>(n => n.Code == "RAW" && n.Quantity == 4.5m);

        // Soft-delete of a component: the BOM keeps the row and flags it inactive.
        (await Api.DeleteAsync($"/api/materials/{raw.Id}")).Status.Should().Be(HttpStatusCode.NoContent);
        var after = (await Api.GetAsync<List<BomNodeModel>>($"/api/materials/{finished.Id}/bom")).Ok;
        after.Single().Children.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Bom_DuplicateSelfAndWrongParent_Failure_409And422()
    {
        var finished = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var raw = await Api.CreateMaterialAsync("RAW", MaterialType.RawMaterial);
        await Api.AddBomItemAsync(finished.Id, raw.Id, 1);

        (await Api.AddBomItemAsync(finished.Id, raw.Id, 3)).Status.Should().Be(HttpStatusCode.Conflict);
        (await Api.AddBomItemAsync(finished.Id, finished.Id, 1)).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.AddBomItemAsync(raw.Id, finished.Id, 1)).Status.Should().Be(HttpStatusCode.UnprocessableEntity);

        var tree = (await Api.GetAsync<List<BomNodeModel>>($"/api/materials/{finished.Id}/bom")).Ok;
        tree.Should().ContainSingle().Which.Quantity.Should().Be(1, "the rejected calls changed nothing");
    }
}
