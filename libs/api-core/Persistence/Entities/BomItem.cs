using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

public class BomItem : IEntity<int>
{
    public int Id { get; set; }
    public int ParentMaterialId { get; set; }
    public int ComponentMaterialId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Routing step of the parent where the component is consumed; null = first start step (automatic).</summary>
    public int? ConsumedInStepId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
