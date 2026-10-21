using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.PlanningFeature.Models;

namespace MicroMES.ApiFeatures.PlanningFeature.Services;

/// <summary>Pure helpers of the timeline: lanes, non-working spans and load. They use the same calendar functions as the planning.</summary>
public static class TimelineCalculator
{
    public const int MaxWindowDays = 62;
    public const int DayLoadMaxDays = 14;

    public static TimelinePeriod PeriodFor(DateTime from, DateTime to)
        => (to - from).TotalDays <= DayLoadMaxDays ? TimelinePeriod.Day : TimelinePeriod.Week;

    /// <summary>
    /// Lane of each bar: bars sorted by start, then end, then id; each takes the lowest lane that is free at its start
    /// (a lane is free when its previous bar ends at or before the start).
    /// </summary>
    public static Dictionary<int, int> AssignLanes(IEnumerable<(int Id, DateTime Start, DateTime End)> bars)
    {
        var laneEnds = new List<DateTime>();
        var lanes = new Dictionary<int, int>();
        foreach (var b in bars.OrderBy(b => b.Start).ThenBy(b => b.End).ThenBy(b => b.Id))
        {
            var lane = laneEnds.FindIndex(end => end <= b.Start);
            if (lane < 0)
            {
                laneEnds.Add(b.End);
                lane = laneEnds.Count - 1;
            }
            else laneEnds[lane] = b.End;
            lanes[b.Id] = lane;
        }
        return lanes;
    }

    private sealed record Piece(DateTime Start, DateTime End, TimelineNonWorkingKind Kind, int? ExceptionId, string? Reason);

    /// <summary>
    /// Spans of [from, to) in which the machine does not work. Day by day (company time) the complement of the working
    /// slots is tagged with the winning exception (or "weekly"); adjacent spans with the same tag are merged, so a weekend is one span.
    /// </summary>
    public static List<TimelineNonWorkingModel> NonWorking(int? equipmentId, IAvailabilitySource source, WorkingTimeCalculator calc, DateTime from, DateTime to)
    {
        var pieces = new List<Piece>();
        for (var date = calc.LocalDate(from); date <= calc.LocalDate(to.AddTicks(-1)); date = date.AddDays(1))
        {
            var dayStart = calc.ToUtc(date, TimeOnly.MinValue);
            var dayEnd = calc.ToUtc(date.AddDays(1), TimeOnly.MinValue);
            var winner = AvailabilityResolver.Winning(source, date);
            var kind = winner is null ? TimelineNonWorkingKind.Weekly
                : winner.Level == AvailabilityLevel.Resource ? TimelineNonWorkingKind.MachineException : TimelineNonWorkingKind.CalendarException;
            var reason = winner?.Reason;
            var cursor = dayStart;
            foreach (var (s, e) in calc.DayIntervals(source, date))
            {
                if (s > cursor) pieces.Add(new Piece(cursor, s, kind, winner?.Id, reason));
                if (e > cursor) cursor = e;
            }
            if (dayEnd > cursor) pieces.Add(new Piece(cursor, dayEnd, kind, winner?.Id, reason));
        }

        var result = new List<TimelineNonWorkingModel>();
        Piece? open = null;
        DateTime openStart = default, openEnd = default;
        void Flush()
        {
            if (open is null) return;
            var s = openStart < from ? from : openStart;
            var e = openEnd > to ? to : openEnd;
            if (e > s) result.Add(new TimelineNonWorkingModel { EquipmentId = equipmentId, Start = s, End = e, Kind = open.Kind, Reason = open.Reason });
            open = null;
        }
        foreach (var p in pieces)
        {
            if (open is not null && open.Kind == p.Kind && open.ExceptionId == p.ExceptionId && openEnd == p.Start) openEnd = p.End;
            else
            {
                Flush();
                open = p;
                openStart = p.Start;
                openEnd = p.End;
            }
        }
        Flush();
        return result;
    }

    /// <summary>
    /// Load per period (day or week of company time, clipped to the window): worked minutes of the bars divided by
    /// capacity x working minutes of the machine in the period.
    /// </summary>
    public static List<TimelineLoadModel> Load(int equipmentId, int capacity, IAvailabilitySource source, WorkingTimeCalculator calc,
        IReadOnlyCollection<(DateTime Start, DateTime End)> bars, DateTime from, DateTime to, TimelinePeriod period)
    {
        decimal Minutes(DateTime s, DateTime e)
        {
            var r = calc.WorkingMinutesBetween(source, s, e);
            return r.IsSuccess ? r.Value : 0m;
        }

        var result = new List<TimelineLoadModel>();
        var first = calc.LocalDate(from);
        if (period == TimelinePeriod.Week) first = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
        var step = period == TimelinePeriod.Day ? 1 : 7;
        for (var date = first; date <= calc.LocalDate(to.AddTicks(-1)); date = date.AddDays(step))
        {
            var ps = calc.ToUtc(date, TimeOnly.MinValue);
            var pe = calc.ToUtc(date.AddDays(step), TimeOnly.MinValue);
            if (ps < from) ps = from;
            if (pe > to) pe = to;
            if (pe <= ps) continue;
            var available = Minutes(ps, pe) * Math.Max(1, capacity);
            var busy = bars.Where(b => b.Start < pe && b.End > ps).Sum(b => Minutes(b.Start > ps ? b.Start : ps, b.End < pe ? b.End : pe));
            result.Add(new TimelineLoadModel
            {
                EquipmentId = equipmentId, PeriodStart = ps, PeriodEnd = pe,
                CapacityMinutes = Math.Round(available, 2), BusyMinutes = Math.Round(busy, 2),
                LoadPercent = available > 0 ? Math.Round(busy / available * 100m, 1) : 0m
            });
        }
        return result;
    }
}
