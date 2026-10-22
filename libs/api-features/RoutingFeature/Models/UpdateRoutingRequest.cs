using System.ComponentModel.DataAnnotations;

namespace MicroMES.ApiFeatures.RoutingFeature.Models;

public class UpdateRoutingRequest
{
    [Required]
    public List<UpdateRoutingStepRequest> Steps { get; set; } = [];

    [Required]
    public List<UpdateRoutingDependencyRequest> Dependencies { get; set; } = [];
}

public class UpdateRoutingStepRequest
{
    /// <summary>Client key, unique within the request. Existing steps may use their id as string.</summary>
    [Required, StringLength(100, MinimumLength = 1)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Id of an existing step to update; null creates a new step.</summary>
    public int? Id { get; set; }

    [Required]
    public int? ProcessSegmentId { get; set; }

    [Required]
    public int? OperationNumber { get; set; }

    [Range(0, 99999999)]
    public decimal? SetupMinutes { get; set; }

    [Range(0, 999999)]
    public decimal? RunMinutesPerUnit { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public double? PositionX { get; set; }
    public double? PositionY { get; set; }

    /// <summary>Toolings required by the step (one unit each; duplicates ignored). Null = unchanged (empty for a new step), empty = none.</summary>
    public List<int>? ToolingIds { get; set; }

    /// <summary>Setup attribute values of the step. Null = unchanged (none for a new step); an empty value removes the attribute.</summary>
    public List<UpdateRoutingSetupValueRequest>? SetupValues { get; set; }
}

public class UpdateRoutingSetupValueRequest
{
    [Required]
    public int? AttributeId { get; set; }

    [StringLength(200)]
    public string? Value { get; set; }
}

public class UpdateRoutingDependencyRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string PredecessorKey { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string SuccessorKey { get; set; } = string.Empty;
}
