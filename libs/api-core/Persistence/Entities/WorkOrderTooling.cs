using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>Tooling required by a work order, copied from the routing step at release (code, name and setup time are a snapshot).</summary>
public class WorkOrderTooling : IEntity<int>
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public int ToolingId { get; set; }
    public string ToolingCode { get; set; } = string.Empty;
    public string ToolingName { get; set; } = string.Empty;
    public int ToolingSetupMinutes { get; set; }
}
