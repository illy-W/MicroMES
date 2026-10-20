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
}

public class UpdateRoutingDependencyRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string PredecessorKey { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string SuccessorKey { get; set; } = string.Empty;
}
