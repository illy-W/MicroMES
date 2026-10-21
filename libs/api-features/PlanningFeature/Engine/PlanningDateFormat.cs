using System.Globalization;

namespace MicroMES.ApiFeatures.PlanningFeature.Engine;

/// <summary>Formats instants in the company time zone for user-facing planning messages (alerts and reasons).</summary>
public static class PlanningDateFormat
{
    /// <summary>Token the pure engine puts in its messages; the service replaces it with <see cref="Short"/>.</summary>
    public const string UntilToken = "{until}";

    /// <summary>"dd/MM/yyyy HH:mm" in the given zone.</summary>
    public static string Local(TimeZoneInfo zone, DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utc), zone).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>"dd/MM alle HH:mm" in the given zone (to be preceded by "il" / "fino al").</summary>
    public static string Short(TimeZoneInfo zone, DateTime utc)
    {
        var l = TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utc), zone);
        return $"{l.ToString("dd/MM", CultureInfo.InvariantCulture)} alle {l.ToString("HH:mm", CultureInfo.InvariantCulture)}";
    }

    /// <summary>Replaces the token with the date of <paramref name="until"/>; without a date the message is returned as is.</summary>
    public static string Resolve(string message, DateTime? until, TimeZoneInfo zone)
        => until is { } u ? message.Replace(UntilToken, Short(zone, u), StringComparison.Ordinal) : message;

    private static DateTime AsUtc(DateTime t) => t.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : t;
}
