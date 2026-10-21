using System.ComponentModel.DataAnnotations;

namespace MicroMES.ApiFeatures.BomFeature.Models;

public class UpdateBomItemRequest
{
    [Required, Range(0.0001, 99999999999999.0)]
    public decimal? Quantity { get; set; }

    /// <summary>Routing step of the parent where the component is consumed; null = automatic (first start step).</summary>
    public int? ConsumedInStepId { get; set; }
}
