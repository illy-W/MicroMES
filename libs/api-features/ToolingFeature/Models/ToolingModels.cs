using System.ComponentModel.DataAnnotations;

namespace MicroMES.ApiFeatures.ToolingFeature.Models;

/// <summary>Compatible machine of a tooling.</summary>
public class ToolingEquipmentModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required bool IsActive { get; set; }
}

/// <summary>Where a tooling is used (a routing step or a work order): id, code and name of the referencing item.</summary>
public class ToolingUsageModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
}

public class ToolingModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    /// <summary>Available pieces, at least 1.</summary>
    public required int Quantity { get; set; }
    /// <summary>Minutes to mount the tooling.</summary>
    public required int SetupMinutes { get; set; }
    public required bool IsActive { get; set; }
    /// <summary>Number of compatible machines (0 = compatible with any machine).</summary>
    public required int EquipmentCount { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
}

public class ToolingDetailModel : ToolingModel
{
    /// <summary>Compatible machines (empty = compatible with any machine).</summary>
    public required List<ToolingEquipmentModel> Equipment { get; set; }
    /// <summary>Routing steps using this tooling (empty until routing steps reference toolings).</summary>
    public required List<ToolingUsageModel> RoutingSteps { get; set; }
    /// <summary>Work orders using this tooling (empty until work orders reference toolings).</summary>
    public required List<ToolingUsageModel> WorkOrders { get; set; }
}

public class CreateToolingRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>At least 1; missing = 1.</summary>
    public int? Quantity { get; set; }

    /// <summary>Not negative; missing = 0.</summary>
    public int? SetupMinutes { get; set; }

    /// <summary>Compatible active machines; empty or missing = any machine.</summary>
    public List<int>? EquipmentIds { get; set; }
}

public class UpdateToolingRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public bool? IsActive { get; set; }

    /// <summary>At least 1; missing = unchanged.</summary>
    public int? Quantity { get; set; }

    /// <summary>Not negative; missing = unchanged.</summary>
    public int? SetupMinutes { get; set; }

    /// <summary>Replaces the compatible machines (only active ones, or already linked); empty or missing = any machine.</summary>
    public List<int>? EquipmentIds { get; set; }
}
