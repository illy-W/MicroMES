using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>Value of a setup attribute for a routing step (trimmed, inner spaces collapsed; the pair is unique).</summary>
public class RoutingStepSetupValue : IEntity<int>
{
    public int Id { get; set; }
    public int RoutingStepId { get; set; }
    public int SetupAttributeId { get; set; }
    public string Value { get; set; } = string.Empty;
}
