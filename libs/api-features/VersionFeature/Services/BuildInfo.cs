using System.Reflection;

namespace MicroMES.ApiFeatures.VersionFeature.Services;

/// <summary>Version, commit and build date of the running application.</summary>
public sealed record BuildInfo(string Version, string? Commit, string? BuildDate)
{
    public const string DevelopmentVersion = "0.0.0-dev";

    /// <summary>Version from the assembly InformationalVersion (build metadata "+sha" stripped); commit/date from env or assembly metadata.</summary>
    public static BuildInfo FromAssembly(Assembly? assembly)
    {
        var informational = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = SemanticVersion.TryParse(informational) is { } parsed ? parsed.ToString() : DevelopmentVersion;

        string? Meta(string key) => assembly?.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;
        string? Pick(string env, string key)
        {
            var value = Environment.GetEnvironmentVariable(env);
            if (string.IsNullOrWhiteSpace(value)) value = Meta(key);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return new BuildInfo(version, Pick("MICROMES_COMMIT", "Commit"), Pick("MICROMES_BUILD_DATE", "BuildDate"));
    }
}
