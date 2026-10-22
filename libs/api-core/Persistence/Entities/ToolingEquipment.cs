using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>Machine a tooling is compatible with. A tooling with no links is compatible with any machine.</summary>
public class ToolingEquipment : IEntity<int>
{
    public int Id { get; set; }
    public int ToolingId { get; set; }
    public int EquipmentId { get; set; }
}
