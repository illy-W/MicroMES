using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiFeatures.RoutingFeature.Models;
using MicroMES.ApiFeatures.RoutingFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.RoutingFeature;

public class RoutingToolingFixture : BaseFeatureFixture
{
    private readonly int _material;
    private readonly int _segment;
    private readonly int _tooling;
    private readonly int _attribute;

    public RoutingToolingFixture()
    {
        _material = AddMaterial("PF");
        _segment = AddSegment("SEG", setup: 5, run: 1);
        var now = DateTime.UtcNow;
        var tooling = new Tooling { Code = "ST-1", Name = "Stampo", SetupMinutes = 10, CreatedAt = now, UpdatedAt = now };
        var attribute = new SetupAttribute { Code = "COL", Name = "Colore", ChangeMinutes = 5, CreatedAt = now, UpdatedAt = now };
        Arrange(db => { db.Tooling.Add(tooling); db.SetupAttributes.Add(attribute); });
        _tooling = tooling.Id;
        _attribute = attribute.Id;
    }

    private Task<RoutingResult<RoutingModel>> Put(List<int>? tooling, string? value, int? id = null)
        => Service<RoutingService>().ReplaceAsync(_material, new UpdateRoutingRequest
        {
            Steps = [new UpdateRoutingStepRequest
            {
                Key = "a", Id = id, OperationNumber = 10, ProcessSegmentId = _segment, ToolingIds = tooling,
                SetupValues = value is null ? null : [new UpdateRoutingSetupValueRequest { AttributeId = _attribute, Value = value }]
            }],
            Dependencies = []
        });

    [Fact]
    public async Task ReplaceAsync_ToolingAndValue_Success_NormalizesValueAndIgnoresDuplicates()
    {
        var result = await Put([_tooling, _tooling], "  Rosso   scuro ");

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Steps.Single().Tooling.Should().ContainSingle();
        result.Data.Steps.Single().SetupValues.Single().Value.Should().Be("Rosso scuro");
        Query(db => db.RoutingStepTooling.Count()).Should().Be(1);
    }

    [Fact]
    public async Task ReplaceAsync_NullListsOnUpdate_Success_KeepsLinks_EmptyValueRemoves()
    {
        var first = await Put([_tooling], "rosso");
        var id = first.Data!.Steps.Single().Id;

        var kept = await Put(null, null, id);
        kept.Data!.Steps.Single().Tooling.Should().ContainSingle();
        kept.Data.Steps.Single().SetupValues.Should().ContainSingle();

        var removed = await Put([], "  ", id);
        removed.Data!.Steps.Single().Tooling.Should().BeEmpty();
        removed.Data.Steps.Single().SetupValues.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_InactiveToolingAdded_Failure_InvalidNamingIt_ButKeptWhenAlreadyLinked()
    {
        var first = await Put([_tooling], null);
        Arrange(db => db.Tooling.Single().IsActive = false);

        (await Put([_tooling], null, first.Data!.Steps.Single().Id)).IsSuccessful.Should().BeTrue("already linked");
        var newMaterial = AddMaterial("PF2");
        var added = await Service<RoutingService>().ReplaceAsync(newMaterial, new UpdateRoutingRequest
        {
            Steps = [new UpdateRoutingStepRequest { Key = "a", OperationNumber = 10, ProcessSegmentId = _segment, ToolingIds = [_tooling] }],
            Dependencies = []
        });
        added.IsSuccessful.Should().BeFalse();
        ((RoutingResult<RoutingModel>)added).Failure.Should().Be(RoutingFailure.Invalid);
        added.Errors.Should().Contain(e => e.Contains("ST-1"));
    }

    [Fact]
    public async Task ReplaceAsync_UnknownIds_Failure_NotFound()
    {
        var result = await Put([999], null);
        ((RoutingResult<RoutingModel>)result).Failure.Should().Be(RoutingFailure.NotFound);
    }

    [Fact]
    public async Task ReleaseAsync_InactiveToolingRequired_Failure_NamesTooling_NothingPersisted()
    {
        await Put([_tooling], "rosso");
        Arrange(db => db.Tooling.Single().IsActive = false);
        var order = new ProductionOrder { Code = "OP-1", MaterialId = _material, Quantity = 1, Status = ProductionOrderStatus.Planned, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        Arrange(db => db.ProductionOrders.Add(order));

        var result = await Service<ProductionOrderService>().ReleaseAsync(order.Id);

        result.IsSuccessful.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ST-1") && e.Contains("disattivata"));
        Query(db => db.WorkOrders.Count()).Should().Be(0);
    }

    [Fact]
    public async Task ReleaseAsync_ToolingAndValues_Success_SnapshotCopied()
    {
        await Put([_tooling], "rosso");
        var order = new ProductionOrder { Code = "OP-1", MaterialId = _material, Quantity = 1, Status = ProductionOrderStatus.Planned, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        Arrange(db => db.ProductionOrders.Add(order));

        var result = await Service<ProductionOrderService>().ReleaseAsync(order.Id);

        result.IsSuccessful.Should().BeTrue();
        Query(db => db.WorkOrderTooling.Single()).Should().Match<WorkOrderTooling>(t => t.ToolingCode == "ST-1" && t.ToolingSetupMinutes == 10);
        Query(db => db.WorkOrderSetupValues.Single()).Should().Match<WorkOrderSetupValue>(v => v.AttributeCode == "COL" && v.AttributeChangeMinutes == 5 && v.Value == "rosso");
    }
}
