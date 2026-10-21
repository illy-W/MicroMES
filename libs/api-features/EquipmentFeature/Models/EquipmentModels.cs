using System.ComponentModel.DataAnnotations;

namespace MicroMES.ApiFeatures.EquipmentFeature.Models;

public class EquipmentSegmentModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required bool IsActive { get; set; }
}

public class EquipmentModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required bool IsActive { get; set; }
    /// <summary>Process segments this machine can perform (empty = not enabled for any segment).</summary>
    public required List<EquipmentSegmentModel> ProcessSegments { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
}

public class CreateEquipmentRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>Segments the machine is enabled for; empty or missing = none.</summary>
    public List<int>? ProcessSegmentIds { get; set; }
}

public class UpdateEquipmentRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public bool? IsActive { get; set; }

    /// <summary>Replaces the set of linked segments; empty or missing = no segments.</summary>
    public List<int>? ProcessSegmentIds { get; set; }
}
