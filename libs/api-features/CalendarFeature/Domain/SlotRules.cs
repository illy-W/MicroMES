namespace MicroMES.ApiFeatures.CalendarFeature.Domain;

/// <summary>Pure validation of working slots (weekly or exception); messages are Italian and say what to do.</summary>
public static class SlotRules
{
    /// <summary>Error message for a set of (day, start, end) slots, or null when valid. Day is null for exception slots.</summary>
    public static string? Validate(IEnumerable<(DayOfWeek? Day, TimeOnly Start, TimeOnly End)> slots)
    {
        var list = slots.ToList();
        foreach (var s in list)
        {
            var label = s.Day is { } d ? $" ({DayName(d)})" : "";
            if (s.End == TimeOnly.MinValue && s.Start > TimeOnly.MinValue)
                return $"La fascia {s.Start:HH\\:mm}-00:00{label} attraversa la mezzanotte: non è supportata, usa due fasce su due giorni (fine massima 23:59).";
            if (s.End <= s.Start)
                return $"La fascia {s.Start:HH\\:mm}-{s.End:HH\\:mm}{label} non è valida: l'ora di fine deve essere dopo l'inizio.";
        }
        foreach (var group in list.GroupBy(s => s.Day))
        {
            var sorted = group.OrderBy(s => s.Start).ToList();
            for (var i = 1; i < sorted.Count; i++)
                if (sorted[i].Start < sorted[i - 1].End)
                {
                    var label = group.Key is { } d ? $" di {DayName(d)}" : "";
                    return $"Le fasce{label} {sorted[i - 1].Start:HH\\:mm}-{sorted[i - 1].End:HH\\:mm} e {sorted[i].Start:HH\\:mm}-{sorted[i].End:HH\\:mm} si sovrappongono: correggi gli orari.";
                }
        }
        return null;
    }

    public static decimal WeeklyHours(IEnumerable<(TimeOnly Start, TimeOnly End)> slots)
        => Math.Round(slots.Sum(s => (decimal)(s.End - s.Start).TotalHours), 2);

    public static string DayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "lunedì", DayOfWeek.Tuesday => "martedì", DayOfWeek.Wednesday => "mercoledì",
        DayOfWeek.Thursday => "giovedì", DayOfWeek.Friday => "venerdì", DayOfWeek.Saturday => "sabato", _ => "domenica"
    };
}
