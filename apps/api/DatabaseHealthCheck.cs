using MicroMES.ApiCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MicroMES.Api;

/// <summary>Readiness: the database accepts connections (infrastructure check, no business data read).</summary>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database non raggiungibile.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database non raggiungibile.", ex);
        }
    }
}
