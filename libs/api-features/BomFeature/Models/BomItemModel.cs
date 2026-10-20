using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.BomFeature.Models;

public class BomItemModel
{
    public required int Id { get; set; }
    public required int ParentMaterialId { get; set; }
    public required int ComponentMaterialId { get; set; }
    public required decimal Quantity { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }

    public static BomItemModel FromEntity(BomItem i) => new()
    {
        Id = i.Id,
        ParentMaterialId = i.ParentMaterialId,
        ComponentMaterialId = i.ComponentMaterialId,
        Quantity = i.Quantity,
        CreatedAt = i.CreatedAt,
        UpdatedAt = i.UpdatedAt
    };
}
