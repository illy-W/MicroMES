using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.BomFeature.Models;

public class BomNodeModel
{
    public required int Id { get; set; }
    public required int ComponentMaterialId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required MaterialType Type { get; set; }
    public required UnitOfMeasure UnitOfMeasure { get; set; }
    public required decimal Quantity { get; set; }
    /// <summary>Explicit step of the parent routing; null = automatic.</summary>
    public int? ConsumedInStepId { get; set; }
    public BomStepModel? ConsumedInStep { get; set; }
    /// <summary>Resolved step; null when the parent has no routing.</summary>
    public BomEffectiveStepModel? EffectiveStep { get; set; }
    public required bool IsActive { get; set; }
    public required List<BomNodeModel> Children { get; set; }
}
