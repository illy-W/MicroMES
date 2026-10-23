namespace MicroMES.ApiFeatures.VersionFeature.Models;

/// <summary>Installed version and, when the check is enabled, the latest published one.</summary>
public class VersionModel
{
    /// <summary>Installed version (semver, "0.0.0-dev" in development builds).</summary>
    public required string Version { get; set; }

    public string? Commit { get; set; }

    public string? BuildDate { get; set; }

    /// <summary>Latest stable app release, null when the check is disabled or failed.</summary>
    public string? LatestVersion { get; set; }

    public bool UpdateAvailable { get; set; }

    /// <summary>"ok", "disabled" or "unknown" (check failed or no release found).</summary>
    public required string UpdateCheck { get; set; }
}
