using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>Tooling required by a routing step (one unit; the pair is unique).</summary>
public class RoutingStepTooling : IEntity<int>
{
    public int Id { get; set; }
    public int RoutingStepId { get; set; }
    public int ToolingId { get; set; }
}
