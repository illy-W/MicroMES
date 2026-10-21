using System.ComponentModel.DataAnnotations;

namespace MicroMES.ApiFeatures.ProcessSegmentFeature.Models;

public class CreateProcessSegmentRequest
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

    /// <summary>
    /// Machines enabled for the segment. Missing (null) = links unchanged; present (even empty) = replaces the set.
    /// </summary>
    public List<int>? EquipmentIds { get; set; }
}
