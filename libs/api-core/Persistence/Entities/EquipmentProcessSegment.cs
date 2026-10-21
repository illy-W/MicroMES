using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>Links a machine to a process segment it can perform. A segment with no links has no constraint.</summary>
public class EquipmentProcessSegment : IEntity<int>
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public int ProcessSegmentId { get; set; }
    /// <summary>The usual machine of the segment (at most one per segment); the flag drops with the link.</summary>
    public bool IsDefault { get; set; }
}
