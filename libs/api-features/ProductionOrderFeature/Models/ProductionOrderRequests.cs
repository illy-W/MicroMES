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

/// <summary>Body of pause and stop: quantities of the session being closed.</summary>
public class CloseSessionRequest
{
    [Range(0, 1_000_000_000_000d)]
    public decimal GoodQuantity { get; set; }

    [Range(0, 1_000_000_000_000d)]
    public decimal ScrapQuantity { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
