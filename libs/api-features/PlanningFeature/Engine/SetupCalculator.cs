namespace MicroMES.ApiFeatures.PlanningFeature.Engine;

/// <summary>Tooling required by a job, with the minutes needed to mount it.</summary>
public sealed record JobTooling(int ToolingId, string Code, int SetupMinutes);

/// <summary>Setup attribute value of a job; changing it from the previous value costs <paramref name="ChangeMinutes"/>.</summary>
public sealed record JobSetupValue(int AttributeId, string Code, string Name, int ChangeMinutes, string Value);

/// <summary>What a job needs for its setup: tooling, attribute values and the full setup of the step (the cap).</summary>
public sealed record SetupRequirements(
    IReadOnlyList<JobTooling> Tooling, IReadOnlyList<JobSetupValue> SetupValues, decimal FullSetupMinutes)
{
    public static readonly SetupRequirements None = new([], [], 0m);
    public bool IsEmpty => Tooling.Count == 0 && SetupValues.Count == 0;
}

/// <summary>State of a machine after a job: mounted tooling and attribute values (by attribute id).</summary>
public sealed record MachineSetupState(IReadOnlySet<int> ToolingIds, IReadOnlyDictionary<int, string> Values)
{
    /// <summary>The state left by a job: its tooling set and its values.</summary>
    public static MachineSetupState After(SetupRequirements job)
        => new(job.Tooling.Select(t => t.ToolingId).ToHashSet(),
            job.SetupValues.GroupBy(v => v.AttributeId).ToDictionary(g => g.Key, g => g.Last().Value));
}

/// <param name="Minutes">Setup minutes to use in the duration.</param>
/// <param name="Reasons">Why (Italian), one entry per change; a single "attrezzaggio completo" entry for the full setup.</param>
/// <param name="IsFull">True when the result is the full setup of the step (unknown state, nothing to compare, or capped).</param>
public sealed record SetupResult(decimal Minutes, IReadOnlyList<string> Reasons, bool IsFull);

/// <summary>
/// Pure sequence-dependent setup (REQ-4), shared by estimate, engine and manual move: no database, deterministic.
/// <list type="bullet">
/// <item>State unknown (null) or job without tooling and attribute values: the full setup of the step, as before.</item>
/// <item>Otherwise: sum of the SetupMinutes of the tooling not mounted in the previous state, plus the ChangeMinutes of every
/// attribute whose value differs (absent in the previous state = different). Values are compared trimmed and case-insensitive.</item>
/// <item>Cap: when the step's full SetupMinutes is &gt; 0 the result never exceeds it; when it is 0 there is no cap
/// (a step with 0 full setup but tooling/attributes is computed freely).</item>
/// </list>
/// </summary>
public static class SetupCalculator
{
    public static SetupResult Compute(MachineSetupState? previous, SetupRequirements job)
    {
        var full = Math.Max(0m, job.FullSetupMinutes);
        if (previous is null || job.IsEmpty) return Full(full);

        decimal minutes = 0m;
        var reasons = new List<string>();
        foreach (var t in job.Tooling.OrderBy(t => t.Code, StringComparer.Ordinal).ThenBy(t => t.ToolingId))
        {
            if (previous.ToolingIds.Contains(t.ToolingId)) continue;
            minutes += t.SetupMinutes;
            reasons.Add($"cambio attrezzatura {t.Code}: {t.SetupMinutes} min");
        }
        foreach (var v in job.SetupValues.OrderBy(v => v.Code, StringComparer.Ordinal).ThenBy(v => v.AttributeId))
        {
            var has = previous.Values.TryGetValue(v.AttributeId, out var old);
            if (has && Same(old!, v.Value)) continue;
            minutes += v.ChangeMinutes;
            reasons.Add($"cambio {v.Code} ({(has ? old!.Trim() : "—")} → {v.Value.Trim()}): {v.ChangeMinutes} min");
        }

        if (full > 0m && minutes > full)
        {
            reasons.Add($"limitato all'attrezzaggio completo: {Format(full)} min");
            return new SetupResult(full, reasons, true);
        }
        return new SetupResult(minutes, reasons, false);
    }

    /// <summary>Standard minutes of a job with the computed setup in place of the full one (never negative).</summary>
    public static decimal WithSetup(decimal standardMinutes, decimal fullSetupMinutes, SetupResult setup)
        => Math.Max(0m, standardMinutes - fullSetupMinutes + setup.Minutes);

    private static SetupResult Full(decimal full)
        => new(full, [$"attrezzaggio completo: {Format(full)} min"], true);

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Format(decimal v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
