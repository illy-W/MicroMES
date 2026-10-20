using System.ComponentModel.DataAnnotations;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.MaterialFeature.Models;

public class UpdateMaterialRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required, EnumDataType(typeof(MaterialType))]
    public MaterialType? Type { get; set; }

    [Required, EnumDataType(typeof(UnitOfMeasure))]
    public UnitOfMeasure? UnitOfMeasure { get; set; }

    [StringLength(200)]
    public string? Vendor { get; set; }

    [StringLength(100)]
    public string? VendorMaterialId { get; set; }

    [Required]
    public bool? IsActive { get; set; }
}
