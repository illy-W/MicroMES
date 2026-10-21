using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.BomFeature.Services;

namespace MicroMES.ApiFeatures.BomFeature.Models;

public class BomItemModel
{
    public required int Id { get; set; }
    public required int ParentMaterialId { get; set; }
    public required int ComponentMaterialId { get; set; }
    public required decimal Quantity { get; set; }
    /// <summary>Explicit step of the parent routing; null = automatic.</summary>
    public int? ConsumedInStepId { get; set; }
    public BomStepModel? ConsumedInStep { get; set; }
    /// <summary>Resolved step; null when the parent has no routing.</summary>
    public BomEffectiveStepModel? EffectiveStep { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }

    public static BomItemModel FromEntity(BomItem i, BomStepResolver.Resolution r) => new()
    {
        Id = i.Id,
        ParentMaterialId = i.ParentMaterialId,
        ComponentMaterialId = i.ComponentMaterialId,
        Quantity = i.Quantity,
        ConsumedInStepId = i.ConsumedInStepId,
        ConsumedInStep = r.Consumed,
        EffectiveStep = r.Effective,
        CreatedAt = i.CreatedAt,
        UpdatedAt = i.UpdatedAt
    };
}
