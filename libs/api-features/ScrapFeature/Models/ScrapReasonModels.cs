using System.ComponentModel.DataAnnotations;

namespace MicroMES.ApiFeatures.ScrapFeature.Models;

public class ScrapReasonSegmentModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
}

public class ScrapReasonModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required bool IsActive { get; set; }
    /// <summary>Linked process segments; empty means the reason is valid for every segment.</summary>
    public required List<ScrapReasonSegmentModel> ProcessSegments { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
}

public class CreateScrapReasonRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>Empty or missing = valid for every process segment.</summary>
    public List<int>? ProcessSegmentIds { get; set; }
}

public class UpdateScrapReasonRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public bool? IsActive { get; set; }

    public List<int>? ProcessSegmentIds { get; set; }
}
