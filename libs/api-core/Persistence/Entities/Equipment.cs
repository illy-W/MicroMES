using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>A machine or work station (soft-delete through IsActive).</summary>
public class Equipment : IEntity<int>
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Own calendar; null = company default calendar.</summary>
    public int? WorkCalendarId { get; set; }
    /// <summary>Simultaneous jobs (data only for now, no conflict rule).</summary>
    public int Capacity { get; set; } = 1;
    /// <summary>100 = standard speed, 80 = slower, 120 = faster. Used only to compute real durations.</summary>
    public decimal EfficiencyPercent { get; set; } = 100m;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
