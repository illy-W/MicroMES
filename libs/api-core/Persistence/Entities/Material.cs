using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

public enum MaterialType
{
    RawMaterial,
    Intermediate,
    FinishedGood,
    Consumable
}

public enum UnitOfMeasure
{
    Piece,
    Gram,
    Kilogram,
    Meter,
    Liter
}

public class Material : IEntity<int>
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MaterialType Type { get; set; }
    public UnitOfMeasure UnitOfMeasure { get; set; }
    public string? Vendor { get; set; }
    public string? VendorMaterialId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
