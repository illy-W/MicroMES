using System.ComponentModel.DataAnnotations;

namespace MicroMES.ApiFeatures.ProcessSegmentFeature.Models;

public class UpdateProcessSegmentRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, 99999999)]
    public decimal SetupMinutes { get; set; }

    [Range(0, 999999)]
    public decimal RunMinutesPerUnit { get; set; }

    [Required]
    public bool? IsActive { get; set; }

    /// <summary>
    /// Machines enabled for the segment. Missing (null) = links unchanged; present (even empty) = replaces the set.
    /// </summary>
    public List<int>? EquipmentIds { get; set; }

    /// <summary>
    /// Default machine of the segment. Used only when <see cref="EquipmentIds"/> is present: it then replaces the current default
    /// (null = no default) and must be one of the listed machines (422 otherwise). Ignored when EquipmentIds is omitted (default unchanged).
    /// </summary>
    public int? DefaultEquipmentId { get; set; }
}
