namespace MicroMES.ApiTests.Shared.Fixtures;

/// <summary>Controllable clock: tests decide the current instant and move it forward explicitly.</summary>
public sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public FakeTimeProvider() : this(new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero))
    {
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
