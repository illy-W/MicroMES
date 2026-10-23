using MicroMES.ApiCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MicroMES.Api;

/// <summary>Readiness: the database accepts connections (infrastructure check, no business data read).</summary>
public sealed class DatabaseHealthCheck(AppDbContext db, DatabaseStartupState state) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (state.Error is not null)
            return HealthCheckResult.Unhealthy($"Migration non riuscita: {state.Error}");
        if (!state.Completed)
            return HealthCheckResult.Unhealthy("Avvio del database in corso (migration).");

        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Database non raggiungibile.");
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            return pending == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Migration in sospeso: {pending}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database non raggiungibile.", ex);
        }
    }
}
