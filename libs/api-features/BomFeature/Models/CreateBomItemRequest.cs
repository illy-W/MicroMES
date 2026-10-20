using System.ComponentModel.DataAnnotations;

namespace MicroMES.ApiFeatures.BomFeature.Models;

public class CreateBomItemRequest
{
    [Required]
    public int? ComponentMaterialId { get; set; }

    [Required, Range(0.0001, 99999999999999.0)]
    public decimal? Quantity { get; set; }
}
