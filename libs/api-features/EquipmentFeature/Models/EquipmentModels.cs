using System.ComponentModel.DataAnnotations;
using MicroMES.ApiFeatures.CalendarFeature.Models;

namespace MicroMES.ApiFeatures.EquipmentFeature.Models;

public class EquipmentSegmentModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required bool IsActive { get; set; }
    /// <summary>True when this machine is the default one of the segment.</summary>
    public required bool IsDefault { get; set; }
}

public class EquipmentModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required bool IsActive { get; set; }
    /// <summary>Own calendar; null = follows the company default calendar.</summary>
    public int? WorkCalendarId { get; set; }
    /// <summary>Simultaneous jobs (data only: no conflict rule yet).</summary>
    public required int Capacity { get; set; }
    /// <summary>100 = standard speed, 80 = slower, 120 = faster.</summary>
    public required decimal EfficiencyPercent { get; set; }
    /// <summary>The calendar actually followed (own or default) with the next closures.</summary>
    public EffectiveCalendarModel? EffectiveCalendar { get; set; }
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

    /// <summary>Own calendar (must be active); missing = company default.</summary>
    public int? WorkCalendarId { get; set; }

    /// <summary>Simultaneous jobs, at least 1; missing = 1.</summary>
    [Range(1, 1000)]
    public int? Capacity { get; set; }

    /// <summary>Greater than 0 (100 = standard); missing = 100.</summary>
    [Range(typeof(decimal), "0.01", "1000")]
    public decimal? EfficiencyPercent { get; set; }

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

    /// <summary>Own calendar (must be active); missing = company default.</summary>
    public int? WorkCalendarId { get; set; }

    /// <summary>Simultaneous jobs, at least 1; missing = unchanged.</summary>
    [Range(1, 1000)]
    public int? Capacity { get; set; }

    /// <summary>Greater than 0 (100 = standard); missing = unchanged.</summary>
    [Range(typeof(decimal), "0.01", "1000")]
    public decimal? EfficiencyPercent { get; set; }

    /// <summary>Replaces the set of linked segments; empty or missing = no segments.</summary>
    public List<int>? ProcessSegmentIds { get; set; }
}
