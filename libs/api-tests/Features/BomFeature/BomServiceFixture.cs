using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.BomFeature.Models;
using MicroMES.ApiFeatures.BomFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.BomFeature;

public class BomServiceFixture : BaseFeatureFixture
{
    private static CreateBomItemRequest Item(int componentId, decimal quantity = 1m)
        => new() { ComponentMaterialId = componentId, Quantity = quantity };

    [Fact]
    public async Task AddAsync_FinishedGoodWithRawComponent_Success()
    {
        var parent = AddMaterial("PF", MaterialType.FinishedGood);
        var component = AddMaterial("RAW", MaterialType.RawMaterial);

        var result = await Service<BomService>().AddAsync(parent, Item(component, 2.5m));

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Quantity.Should().Be(2.5m);
        Query(db => db.BomItems.Count()).Should().Be(1);
    }

    [Theory]
    [InlineData(MaterialType.RawMaterial)]
    [InlineData(MaterialType.Consumable)]
    public async Task AddAsync_ParentNotIntermediateOrFinished_Failure_Invalid(MaterialType parentType)
    {
        var parent = AddMaterial("P", parentType);
        var component = AddMaterial("C", MaterialType.RawMaterial);

        var result = await Service<BomService>().AddAsync(parent, Item(component));

        result.Failure.Should().Be(BomFailure.Invalid);
        Query(db => db.BomItems.Count()).Should().Be(0);
    }

    [Fact]
    public async Task AddAsync_IntermediateParent_Success()
    {
        var parent = AddMaterial("SL", MaterialType.Intermediate);
        var component = AddMaterial("C", MaterialType.RawMaterial);

        var result = await Service<BomService>().AddAsync(parent, Item(component));

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task AddAsync_DuplicateComponent_Failure_Conflict()
    {
        var parent = AddMaterial("PF");
        var component = AddMaterial("C", MaterialType.RawMaterial);
        await Service<BomService>().AddAsync(parent, Item(component));

        var result = await Service<BomService>().AddAsync(parent, Item(component, 3m));

        result.Failure.Should().Be(BomFailure.Conflict);
        Query(db => db.BomItems.Count()).Should().Be(1);
    }

    [Fact]
    public async Task AddAsync_SelfAsComponent_Failure_Invalid()
    {
        var parent = AddMaterial("PF");

        var result = await Service<BomService>().AddAsync(parent, Item(parent));

        result.Failure.Should().Be(BomFailure.Invalid);
    }

    [Fact]
    public async Task AddAsync_InactiveComponent_Failure_Invalid()
    {
        var parent = AddMaterial("PF");
        var component = AddMaterial("C", MaterialType.RawMaterial, active: false);

        var result = await Service<BomService>().AddAsync(parent, Item(component));

        result.Failure.Should().Be(BomFailure.Invalid);
    }

    [Fact]
    public async Task AddAsync_UnknownParentOrComponent_Failure_NotFound()
    {
        var parent = AddMaterial("PF");

        (await Service<BomService>().AddAsync(999, Item(parent))).Failure.Should().Be(BomFailure.NotFound);
        (await Service<BomService>().AddAsync(parent, Item(999))).Failure.Should().Be(BomFailure.NotFound);
    }

    [Fact]
    public async Task AddAsync_IndirectCycle_Success_CyclesAreOnlyDetectedWhenAnOrderIsReleased()
    {
        var a = AddMaterial("A", MaterialType.Intermediate);
        var b = AddMaterial("B", MaterialType.Intermediate);
        await Service<BomService>().AddAsync(a, Item(b));

        var result = await Service<BomService>().AddAsync(b, Item(a));

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ExistingItem_Success_ChangesQuantity()
    {
        var parent = AddMaterial("PF");
        var component = AddMaterial("C", MaterialType.RawMaterial);
        var added = await Service<BomService>().AddAsync(parent, Item(component));

        var result = await Service<BomService>().UpdateAsync(added.Data!.Id, new UpdateBomItemRequest { Quantity = 7m });

        result.IsSuccessful.Should().BeTrue();
        Query(db => db.BomItems.Single().Quantity).Should().Be(7m);
    }

    [Fact]
    public async Task UpdateAsync_UnknownItem_Failure_NotFound()
    {
        var result = await Service<BomService>().UpdateAsync(999, new UpdateBomItemRequest { Quantity = 1m });

        result.Failure.Should().Be(BomFailure.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_ExistingItem_Success_RemovesTheRow()
    {
        var parent = AddMaterial("PF");
        var component = AddMaterial("C", MaterialType.RawMaterial);
        var added = await Service<BomService>().AddAsync(parent, Item(component));

        var result = await Service<BomService>().DeleteAsync(added.Data!.Id);

        result.IsSuccessful.Should().BeTrue();
        Query(db => db.BomItems.Count()).Should().Be(0);
    }

    [Fact]
    public async Task GetTreeAsync_TwoLevels_Success_NestsChildrenAndSortsByCode()
    {
        var pf = AddMaterial("PF", MaterialType.FinishedGood);
        var sl = AddMaterial("SL", MaterialType.Intermediate);
        var rawB = AddMaterial("RAW-B", MaterialType.RawMaterial);
        var rawA = AddMaterial("RAW-A", MaterialType.RawMaterial);
        await Service<BomService>().AddAsync(pf, Item(sl, 2m));
        await Service<BomService>().AddAsync(pf, Item(rawB));
        await Service<BomService>().AddAsync(sl, Item(rawA, 4m));

        var result = await Service<BomService>().GetTreeAsync(pf);

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Select(n => n.Code).Should().Equal("RAW-B", "SL");
        var slNode = result.Data.Single(n => n.Code == "SL");
        slNode.Quantity.Should().Be(2m);
        slNode.Children.Should().ContainSingle().Which.Should().Match<BomNodeModel>(n => n.Code == "RAW-A" && n.Quantity == 4m);
    }

    [Fact]
    public async Task GetTreeAsync_CyclicBom_Success_StopsAtMaximumDepthInsteadOfLooping()
    {
        var a = AddMaterial("A", MaterialType.Intermediate);
        var b = AddMaterial("B", MaterialType.Intermediate);
        await Service<BomService>().AddAsync(a, Item(b));
        await Service<BomService>().AddAsync(b, Item(a));

        var result = await Service<BomService>().GetTreeAsync(a);

        result.IsSuccessful.Should().BeTrue();
        Depth(result.Data!).Should().Be(10);

        static int Depth(List<BomNodeModel> nodes) => nodes.Count == 0 ? 0 : 1 + nodes.Max(n => Depth(n.Children));
    }

    [Fact]
    public async Task GetTreeAsync_UnknownMaterial_Failure_NotFound()
    {
        var result = await Service<BomService>().GetTreeAsync(999);

        result.Failure.Should().Be(BomFailure.NotFound);
    }
}
