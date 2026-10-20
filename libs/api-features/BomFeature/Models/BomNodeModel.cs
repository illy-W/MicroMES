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
    public required bool IsActive { get; set; }
    public required List<BomNodeModel> Children { get; set; }
}
