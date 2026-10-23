using System.Net.Http.Json;
using System.Text.Json.Serialization;
using illyW.Framework.Core.ResultPattern;
using MicroMES.ApiFeatures.VersionFeature.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MicroMES.ApiFeatures.VersionFeature.Services;

/// <summary>
/// Installed version plus the update check against the GitHub releases of the app (tags "app-vX.Y.Z", latest non-prerelease).
/// The outcome is cached in memory for 24 hours (15 minutes after a failure); network errors never fail the call.
/// </summary>
public class VersionService(
    BuildInfo build,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<VersionService> logger)
{
    public const string HttpClientName = "updates";
    public const string TagPrefix = "app-v";
    public const string DefaultRepository = "illy-W/MicroMES";

    private static readonly TimeSpan SuccessTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan FailureTtl = TimeSpan.FromMinutes(15);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private (string Repository, DateTimeOffset ExpiresAt, SemanticVersion? Latest)? _cache;

    public async Task<IResult<VersionModel>> GetVersionAsync(CancellationToken ct = default)
    {
        var model = new VersionModel
        {
            Version = build.Version,
            Commit = build.Commit,
            BuildDate = build.BuildDate,
            UpdateCheck = "disabled"
        };

        if (configuration.GetValue("Updates:Check", true))
        {
            var repository = configuration["Updates:Repository"];
            if (string.IsNullOrWhiteSpace(repository)) repository = DefaultRepository;

            var latest = await GetLatestAsync(repository, ct);
            if (latest is null)
            {
                model.UpdateCheck = "unknown";
            }
            else
            {
                model.UpdateCheck = "ok";
                model.LatestVersion = latest.ToString();
                var current = SemanticVersion.TryParse(build.Version);
                model.UpdateAvailable = current is not null && latest.CompareTo(current) > 0;
            }
        }

        var result = new Result<VersionModel>();
        result.Succeed(model);
        return result;
    }

    private async Task<SemanticVersion?> GetLatestAsync(string repository, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (_cache is { } c && c.Repository == repository && c.ExpiresAt > now) return c.Latest;

        await _gate.WaitAsync(ct);
        try
        {
            now = clock.GetUtcNow();
            if (_cache is { } c2 && c2.Repository == repository && c2.ExpiresAt > now) return c2.Latest;

            var latest = await FetchLatestAsync(repository, ct);
            _cache = (repository, now + (latest is null ? FailureTtl : SuccessTtl), latest);
            return latest;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SemanticVersion?> FetchLatestAsync(string repository, CancellationToken ct)
    {
        try
        {
            var http = httpClientFactory.CreateClient(HttpClientName);
            var releases = await http.GetFromJsonAsync<List<GitHubRelease>>(
                $"https://api.github.com/repos/{repository}/releases?per_page=100", ct);

            return releases?
                .Where(r => !r.Draft && !r.Prerelease && r.TagName is not null && r.TagName.StartsWith(TagPrefix, StringComparison.Ordinal))
                .Select(r => SemanticVersion.TryParse(r.TagName![TagPrefix.Length..]))
                .Where(v => v is { IsPreRelease: false })
                .OrderDescending()
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Controllo aggiornamenti non riuscito: {Reason}", ex.Message);
            return null;
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
    }
}
