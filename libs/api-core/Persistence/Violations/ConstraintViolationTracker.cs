using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MicroMES.ApiCore.Persistence.Violations;

/// <summary>
/// Per-request record of the last constraint violation seen by the DbContext. The generic repositories turn
/// SaveChanges exceptions into failed results, so the exception itself never reaches the HTTP pipeline:
/// the web layer uses this record to answer 409 instead of 500.
/// </summary>
public sealed class ConstraintViolationTracker
{
    public ConstraintViolation? Last { get; private set; }

    public void Record(ConstraintViolation violation) => Last = violation;
    public void Clear() => Last = null;
}

public sealed class ConstraintViolationInterceptor(ConstraintViolationTracker tracker) : SaveChangesInterceptor
{
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Track(eventData);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Track(eventData);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Track(DbContextErrorEventData eventData)
    {
        if (ConstraintViolation.From(eventData.Exception) is { } violation) tracker.Record(violation);
    }
}
