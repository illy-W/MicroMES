using System.ComponentModel.DataAnnotations;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Models;

public class CreateProductionOrderRequest
{
    [Required]
    public int? MaterialId { get; set; }

    [Required, Range(0.0001, 1_000_000_000_000d)]
    public decimal? Quantity { get; set; }

    public int Priority { get; set; }

    public DateTime? RequestedStart { get; set; }

    public DateTime? RequestedEnd { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateProductionOrderRequest
{
    [Required, Range(0.0001, 1_000_000_000_000d)]
    public decimal? Quantity { get; set; }

    public int Priority { get; set; }

    public DateTime? RequestedStart { get; set; }

    public DateTime? RequestedEnd { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}

public class AbortRequest
{
    [StringLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>Optional body of start and resume: the machine used by the new session.</summary>
public class OpenSessionRequest
{
    /// <summary>
    /// Machine of the session. Missing: start uses the assigned machine, resume the one of the last session
    /// (fallback: the assigned one). Required (422) when the phase has active linked machines and none can be inferred.
    /// </summary>
    public int? EquipmentId { get; set; }
}

/// <summary>Body of the assignment of a machine to a work order.</summary>
public class AssignEquipmentRequest
{
    /// <summary>The machine (active and linked to the phase) or null to remove the assignment.</summary>
    public int? EquipmentId { get; set; }
}

/// <summary>Body of pause and stop: quantities of the session being closed.</summary>
public class CloseSessionRequest
{
    [Range(0, 1_000_000_000_000d)]
    public decimal GoodQuantity { get; set; }

    [Range(0, 1_000_000_000_000d)]
    public decimal ScrapQuantity { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Scrap split by reason; the positive quantities must add up to ScrapQuantity. Missing or empty: all the scrap is "no reason".
    /// </summary>
    public List<ScrapEntryRequest>? ScrapEntries { get; set; }
}

/// <summary>One scrap row: reason (null = no reason) and quantity.</summary>
public class ScrapEntryRequest
{
    public int? ScrapReasonId { get; set; }

    [Range(0, 1_000_000_000_000d)]
    public decimal Quantity { get; set; }
}

/// <summary>Body of the correction of a closed session: the new values replace the previous ones.</summary>
public class CorrectSessionRequest
{
    [Range(0, 1_000_000_000_000d)]
    public decimal GoodQuantity { get; set; }

    [Range(0, 1_000_000_000_000d)]
    public decimal ScrapQuantity { get; set; }

    public List<ScrapEntryRequest>? ScrapEntries { get; set; }

    /// <summary>New machine of the session (active and linked to the phase). Missing: the machine is unchanged.</summary>
    public int? EquipmentId { get; set; }

    /// <summary>Why the session is corrected (mandatory).</summary>
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Notes { get; set; } = string.Empty;
}
