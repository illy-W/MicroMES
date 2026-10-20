using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.MaterialFeature.Models;

public class MaterialModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required MaterialType Type { get; set; }
    public required UnitOfMeasure UnitOfMeasure { get; set; }
    public string? Vendor { get; set; }
    public string? VendorMaterialId { get; set; }
    public required bool IsActive { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }

    public static MaterialModel FromEntity(Material m) => new()
    {
        Id = m.Id,
        Code = m.Code,
        Name = m.Name,
        Description = m.Description,
        Type = m.Type,
        UnitOfMeasure = m.UnitOfMeasure,
        Vendor = m.Vendor,
        VendorMaterialId = m.VendorMaterialId,
        IsActive = m.IsActive,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
