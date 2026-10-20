using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

public class BomItem : IEntity<int>
{
    public int Id { get; set; }
    public int ParentMaterialId { get; set; }
    public int ComponentMaterialId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
