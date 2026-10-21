using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

public class WorkOrderMaterial : IEntity<int>
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public int MaterialId { get; set; }
    public decimal RequiredQuantity { get; set; }
    public int? ChildProductionOrderId { get; set; }
}
