namespace MicroMES.Api;

/// <summary>Outcome of the startup database work, read by the readiness check.</summary>
public sealed class DatabaseStartupState
{
    // Completed by default: when the service is not registered (OpenAPI generation) nothing is pending.
    public volatile bool Completed = true;
    private volatile string? _error;

    public string? Error { get => _error; set => _error = value; }
}

/// <summary>
/// Applies the migrations (and the optional sample data) before the host takes traffic. On failure the host stays up
/// but not ready (/health/ready answers 503 with the reason, /health/live stays 200) and the work is retried every
/// 10 seconds: a database that is still starting (rolling update, bundled PostgreSQL) recovers without a restart,
/// and a broken one never serves traffic nor takes the pod down in a crash loop.
/// </summary>
public sealed class DatabaseStartupService(
    IServiceProvider services,
    IConfiguration configuration,
    IHostEnvironment environment,
    DatabaseStartupState state,
    ILogger<DatabaseStartupService> logger) : IHostedService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
    private readonly CancellationTokenSource _stop = new();
    private Task _retry = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        state.Completed = false;
        if (!TryRun())
            _retry = RetryAsync(_stop.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stop.CancelAsync();
        await Task.WhenAny(_retry, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    private bool TryRun()
    {
        try
        {
            DatabaseStartup.Startup(services, configuration, environment.IsDevelopment());
            state.Error = null;
            state.Completed = true;
            logger.LogInformation("Database pronto: migration applicate.");
            return true;
        }
        catch (Exception ex)
        {
            state.Error = ex.GetBaseException().Message;
            logger.LogError(ex, "Migration all'avvio non riuscita: {Message}. L'API resta attiva ma non pronta; nuovo tentativo tra {Seconds} secondi.",
                state.Error, RetryDelay.TotalSeconds);
            return false;
        }
    }

    private async Task RetryAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                await Task.Delay(RetryDelay, token);
                if (TryRun()) return;
            }
        }
        catch (OperationCanceledException) { }
    }
}
