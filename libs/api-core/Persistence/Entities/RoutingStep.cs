using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

public class RoutingStep : IEntity<int>
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public int ProcessSegmentId { get; set; }
    public int OperationNumber { get; set; }
    public decimal? SetupMinutes { get; set; }
    public decimal? RunMinutesPerUnit { get; set; }
    public string? Notes { get; set; }
    public double? PositionX { get; set; }
    public double? PositionY { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
