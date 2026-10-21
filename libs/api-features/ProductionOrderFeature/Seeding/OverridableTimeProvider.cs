namespace MicroMES.ApiFeatures.ProductionOrderFeature.Seeding;

/// <summary>
/// Clock that follows the system time unless a fixed instant is set. Only the development seeder sets it,
/// so that the sessions of the sample orders carry plausible past timestamps.
/// </summary>
public sealed class OverridableTimeProvider : TimeProvider
{
    private DateTimeOffset? _fixed;

    public void Set(DateTime utc) => _fixed = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));

    public void Reset() => _fixed = null;

    public override DateTimeOffset GetUtcNow() => _fixed ?? base.GetUtcNow();
}
