using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>A characteristic (color, size...) whose change between two jobs costs a setup time (soft-delete through IsActive).</summary>
public class SetupAttribute : IEntity<int>
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Minutes lost when the value changes (not negative).</summary>
    public int ChangeMinutes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
