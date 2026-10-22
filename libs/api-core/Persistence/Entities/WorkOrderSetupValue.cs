using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>Setup attribute value of a work order, copied from the routing step at release (attribute data are a snapshot).</summary>
public class WorkOrderSetupValue : IEntity<int>
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public int SetupAttributeId { get; set; }
    public string AttributeCode { get; set; } = string.Empty;
    public string AttributeName { get; set; } = string.Empty;
    public int AttributeChangeMinutes { get; set; }
    public string Value { get; set; } = string.Empty;
}
