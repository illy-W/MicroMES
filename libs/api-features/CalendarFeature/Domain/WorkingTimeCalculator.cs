namespace MicroMES.ApiFeatures.CalendarFeature.Domain;

/// <summary>Outcome of a calculation: a value or an explicit (Italian) error, never an endless loop.</summary>
public sealed record CalcResult<T>(bool IsSuccess, T? Value, string? Error)
{
    public static CalcResult<T> Ok(T value) => new(true, value, null);
    public static CalcResult<T> Fail(string error) => new(false, default, error);
}

/// <summary>
/// Pure working-time engine (no database): converts working minutes into real instants and back, for any
/// <see cref="IAvailabilitySource"/>, in the company time zone.
/// <para>
/// DST: slots are local wall-clock times and the minutes are the real elapsed time between the two instants.
/// A local time that does not exist (spring forward) moves forward by the gap, so a 01:00-04:00 slot gives 2 real
/// hours and a slot fully inside the gap gives 0. A local time that occurs twice (fall back) is read as its first
/// occurrence, so a 01:00-04:00 slot gives 4 real hours (the repeated hour is worked twice).
/// </para>
/// Horizon: when no working time is found within <see cref="Horizon"/> the result is an error.
/// </summary>
public sealed class WorkingTimeCalculator(TimeZoneInfo zone, TimeSpan? horizon = null)
{
    public const string NoWorkingTimeMessage = "Il calendario non ha ore lavorabili nell'orizzonte considerato: aggiungi fasce di lavoro o rimuovi le chiusure.";

    public static readonly TimeSpan DefaultHorizon = TimeSpan.FromDays(731);

    public TimeZoneInfo Zone { get; } = zone;
    public TimeSpan Horizon { get; } = horizon ?? DefaultHorizon;

    /// <summary>Minutes of real work at the given efficiency: standard minutes ÷ (efficiency / 100).</summary>
    public static decimal ApplyEfficiency(decimal standardMinutes, decimal efficiencyPercent)
        => efficiencyPercent <= 0 ? standardMinutes : standardMinutes / (efficiencyPercent / 100m);

    public DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utc), Zone));

    /// <summary>End instant after consuming <paramref name="minutes"/> of working time from <paramref name="startUtc"/>.</summary>
    public CalcResult<DateTime> AddWorkingMinutes(IAvailabilitySource source, DateTime startUtc, decimal minutes)
    {
        startUtc = AsUtc(startUtc);
        if (minutes < 0) return CalcResult<DateTime>.Fail("I minuti di lavoro non possono essere negativi.");
        if (minutes == 0) return CalcResult<DateTime>.Ok(startUtc);
        if (!AvailabilityResolver.HasAnyWorkingTime(source)) return CalcResult<DateTime>.Fail(NoWorkingTimeMessage);

        var remaining = (long)Math.Ceiling(minutes * TimeSpan.TicksPerMinute);
        var cursor = startUtc;
        var limitDate = LocalDate(startUtc.Add(Horizon));
        for (var date = LocalDate(startUtc); date <= limitDate; date = date.AddDays(1))
        {
            foreach (var (s, e) in DayIntervals(source, date))
            {
                if (e <= cursor) continue;
                var from = s > cursor ? s : cursor;
                var available = (e - from).Ticks;
                if (available >= remaining) return CalcResult<DateTime>.Ok(from.AddTicks(remaining));
                remaining -= available;
                cursor = e;
            }
        }
        return CalcResult<DateTime>.Fail(NoWorkingTimeMessage);
    }

    /// <summary>Working minutes inside [from, to); 0 when to &lt;= from.</summary>
    public CalcResult<decimal> WorkingMinutesBetween(IAvailabilitySource source, DateTime fromUtc, DateTime toUtc)
    {
        fromUtc = AsUtc(fromUtc);
        toUtc = AsUtc(toUtc);
        if (toUtc <= fromUtc) return CalcResult<decimal>.Ok(0m);
        if (toUtc - fromUtc > Horizon)
            return CalcResult<decimal>.Fail("L'intervallo supera l'orizzonte massimo di calcolo (2 anni).");

        long ticks = 0;
        for (var date = LocalDate(fromUtc); date <= LocalDate(toUtc); date = date.AddDays(1))
            foreach (var (s, e) in DayIntervals(source, date))
            {
                var from = s > fromUtc ? s : fromUtc;
                var to = e < toUtc ? e : toUtc;
                if (to > from) ticks += (to - from).Ticks;
            }
        return CalcResult<decimal>.Ok((decimal)ticks / TimeSpan.TicksPerMinute);
    }

    /// <summary>The instant itself when it falls in a working slot, otherwise the start of the next one.</summary>
    public CalcResult<DateTime> NextWorkingInstant(IAvailabilitySource source, DateTime instantUtc)
    {
        instantUtc = AsUtc(instantUtc);
        if (!AvailabilityResolver.HasAnyWorkingTime(source)) return CalcResult<DateTime>.Fail(NoWorkingTimeMessage);
        var limitDate = LocalDate(instantUtc.Add(Horizon));
        for (var date = LocalDate(instantUtc); date <= limitDate; date = date.AddDays(1))
            foreach (var (s, e) in DayIntervals(source, date))
                if (e > instantUtc) return CalcResult<DateTime>.Ok(s > instantUtc ? s : instantUtc);
        return CalcResult<DateTime>.Fail(NoWorkingTimeMessage);
    }

    /// <summary>Working intervals of a local day as UTC instants, sorted; empty intervals (inside a DST gap) are dropped.</summary>
    public List<(DateTime Start, DateTime End)> DayIntervals(IAvailabilitySource source, DateOnly date)
    {
        var result = new List<(DateTime, DateTime)>();
        foreach (var slot in AvailabilityResolver.Resolve(source, date).Slots)
        {
            // A slot entirely inside the non-existent hour (spring forward) has no real time: it is dropped, not shifted.
            if (Zone.IsInvalidTime(date.ToDateTime(slot.Start)) && Zone.IsInvalidTime(date.ToDateTime(slot.End))) continue;
            var s = ToUtc(date, slot.Start);
            var e = ToUtc(date, slot.End);
            if (e > s) result.Add((s, e));
        }
        return result;
    }

    public DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        // Non-existent local time: GetUtcOffset answers the standard offset, so the time moves forward by the gap.
        var offset = Zone.IsAmbiguousTime(local)
            ? Zone.GetAmbiguousTimeOffsets(local).Max() // first occurrence = earlier instant = larger offset
            : Zone.GetUtcOffset(local);
        return DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
    }

    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc);
}
