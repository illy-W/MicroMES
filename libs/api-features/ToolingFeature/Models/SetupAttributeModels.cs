using System.ComponentModel.DataAnnotations;

namespace MicroMES.ApiFeatures.ToolingFeature.Models;

public class SetupAttributeModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    /// <summary>Minutes lost when the value changes.</summary>
    public required int ChangeMinutes { get; set; }
    public required bool IsActive { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
}

public class CreateSetupAttributeRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Not negative; missing = 0.</summary>
    public int? ChangeMinutes { get; set; }
}

public class UpdateSetupAttributeRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Not negative; missing = unchanged.</summary>
    public int? ChangeMinutes { get; set; }

    [Required]
    public bool? IsActive { get; set; }
}
