using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>Links a scrap reason to a process segment. A reason with no links is valid for every segment.</summary>
public class ScrapReasonProcessSegment : IEntity<int>
{
    public int Id { get; set; }
    public int ScrapReasonId { get; set; }
    public int ProcessSegmentId { get; set; }
}
