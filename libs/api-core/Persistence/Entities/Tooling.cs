using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>A tooling (die, fixture, mould...) mounted on a machine for an operation (soft-delete through IsActive).</summary>
public class Tooling : IEntity<int>
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Available pieces of this tooling, at least 1.</summary>
    public int Quantity { get; set; } = 1;
    /// <summary>Minutes needed to mount the tooling (not negative).</summary>
    public int SetupMinutes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
