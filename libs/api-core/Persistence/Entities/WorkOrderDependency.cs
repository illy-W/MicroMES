using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

public class WorkOrderDependency : IEntity<int>
{
    public int Id { get; set; }
    public int PredecessorWorkOrderId { get; set; }
    public int SuccessorWorkOrderId { get; set; }
}
