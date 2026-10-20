using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

public class RoutingStepDependency : IEntity<int>
{
    public int Id { get; set; }
    public int PredecessorStepId { get; set; }
    public int SuccessorStepId { get; set; }
}
