using System.Text.RegularExpressions;

namespace MicroMES.ApiFeatures.VersionFeature.Services;

/// <summary>Minimal semver 2.0 value (build metadata ignored) with precedence comparison.</summary>
public sealed partial class SemanticVersion : IComparable<SemanticVersion>
{
    [GeneratedRegex(@"^v?(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$")]
    private static partial Regex Pattern();

    private SemanticVersion(int major, int minor, int patch, string? preRelease)
    {
        Major = major; Minor = minor; Patch = patch; PreRelease = preRelease;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public string? PreRelease { get; }
    public bool IsPreRelease => PreRelease is not null;

    public static SemanticVersion? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Pattern().Match(text.Trim());
        if (!m.Success) return null;
        if (!int.TryParse(m.Groups[1].Value, out var major) || !int.TryParse(m.Groups[2].Value, out var minor)
            || !int.TryParse(m.Groups[3].Value, out var patch)) return null;
        return new SemanticVersion(major, minor, patch, m.Groups[4].Success ? m.Groups[4].Value : null);
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;
        var c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;
        if (PreRelease is null && other.PreRelease is null) return 0;
        if (PreRelease is null) return 1;
        if (other.PreRelease is null) return -1;

        var a = PreRelease.Split('.');
        var b = other.PreRelease.Split('.');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var aNum = int.TryParse(a[i], out var an);
            var bNum = int.TryParse(b[i], out var bn);
            c = aNum && bNum ? an.CompareTo(bn)
                : aNum ? -1
                : bNum ? 1
                : string.CompareOrdinal(a[i], b[i]);
            if (c != 0) return c;
        }
        return a.Length.CompareTo(b.Length);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}{(PreRelease is null ? "" : "-" + PreRelease)}";
}
