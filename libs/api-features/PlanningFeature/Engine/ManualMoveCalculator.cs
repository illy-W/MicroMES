using System.Globalization;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.PlanningFeature.Models;

namespace MicroMES.ApiFeatures.PlanningFeature.Engine;

/// <summary>Outcome of the timing of a manual move: the end and the working minutes, or the (Italian) reason why the start is not valid.</summary>
public sealed record MoveTiming(DateTime? End, decimal WorkMinutes, string? Error);

/// <summary>A work order seen by the conflict check (planned or real interval).</summary>
public sealed record MoveJobRef(int Id, string Code, DateTime? Start, DateTime? End);

/// <summary>
/// Pure rules of the manual move: validity of the start on the calendar, end computation (same <see cref="WorkingTimeCalculator"/>
/// as the engine and the estimator) and conflicts of the result. The start is never shifted: an invalid one is an error with a suggestion.
/// </summary>
public static class ManualMoveCalculator
{
    /// <param name="label">Who is closed in messages: the machine code, or "Il calendario aziendale".</param>
    public static MoveTiming Evaluate(WorkingTimeCalculator calc, IAvailabilitySource source, string label, DateTime startAt,
        decimal standardMinutes, decimal efficiencyPercent)
    {
        var next = calc.NextWorkingInstant(source, startAt);
        if (!next.IsSuccess) return new MoveTiming(null, 0, $"{label}: {next.Error}");
        if (next.Value != startAt)
        {
            var reason = Reason(calc, source, label, startAt);
            return new MoveTiming(null, 0, $"{reason}. Prossimo orario disponibile: {PlanningDateFormat.Compact(calc.Zone, next.Value)}");
        }

        var minutes = WorkingTimeCalculator.ApplyEfficiency(standardMinutes, efficiencyPercent);
        var end = calc.AddWorkingMinutes(source, startAt, minutes);
        return end.IsSuccess ? new MoveTiming(end.Value, minutes, null) : new MoveTiming(null, minutes, $"{label}: {end.Error}");
    }

    /// <summary>Why the instant is not working: closure with its reason, a day without shifts, or out of the shifts of the day.</summary>
    public static string Reason(WorkingTimeCalculator calc, IAvailabilitySource source, string label, DateTime instant)
    {
        var date = calc.LocalDate(instant);
        var day = AvailabilityResolver.Resolve(source, date);
        var dd = date.ToString("dd/MM", CultureInfo.InvariantCulture);
        if (day.Exception is { Kind: CalendarExceptionKind.Closed } closed)
            return $"{label} è chiusa il {dd}{(string.IsNullOrWhiteSpace(closed.Reason) ? "" : $": {closed.Reason}")}";
        if (day.Slots.Count == 0)
            return $"{label} non lavora il {dd}: giorno senza turni";
        var slots = string.Join(", ", day.Slots.Select(s => $"{s.Start:HH\\:mm}-{s.End:HH\\:mm}"));
        return $"{label} è fuori turno il {dd}: turni {slots}";
    }

    public static List<MoveConflictModel> FindConflicts(
        TimeZoneInfo zone, MoveJobRef self, DateTime start, DateTime end,
        IReadOnlyList<MoveJobRef> predecessors, IReadOnlyList<MoveJobRef> successors,
        int? equipmentId, string? equipmentCode, int capacity, IReadOnlyList<MoveJobRef> occupants, DateTime? requestedEnd)
    {
        var result = new List<MoveConflictModel>();

        foreach (var p in predecessors.Where(p => p.End is { } e && e > start).OrderBy(p => p.Id))
        {
            var minutes = (int)Math.Ceiling((p.End!.Value - start).TotalMinutes);
            result.Add(new MoveConflictModel
            {
                Code = MoveConflictCode.PredecessorEndsAfterStart,
                Message = $"Il predecessore {p.Code} finisce il {PlanningDateFormat.Compact(zone, p.End.Value)}, dopo il nuovo inizio di {self.Code} ({PlanningDateFormat.Compact(zone, start)}): {minutes} min di conflitto",
                WorkOrderIds = [self.Id, p.Id], WorkOrderCodes = [self.Code, p.Code], Minutes = minutes
            });
        }

        var stale = successors.Where(s => s.Start is { } st && st < end).OrderBy(s => s.Id).ToList();
        if (stale.Count > 0)
            result.Add(new MoveConflictModel
            {
                Code = MoveConflictCode.SuccessorsStartBeforeEnd,
                Message = $"I successori {string.Join(", ", stale.Select(s => s.Code))} iniziano prima della nuova fine di {self.Code} ({PlanningDateFormat.Compact(zone, end)}): il loro piano diventa obsoleto",
                WorkOrderIds = stale.Select(s => s.Id).ToList(), WorkOrderCodes = stale.Select(s => s.Code).ToList()
            });

        if (equipmentId is not null) result.AddRange(CapacityConflicts(zone, self, start, end, equipmentId.Value, equipmentCode, capacity, occupants));

        if (requestedEnd is { } due && end > due)
        {
            var days = (int)Math.Ceiling((end - due).TotalDays);
            result.Add(new MoveConflictModel
            {
                Code = MoveConflictCode.Late,
                Message = $"{self.Code} finisce il {PlanningDateFormat.Compact(zone, end)}, oltre la fine richiesta dell'ordine ({PlanningDateFormat.Compact(zone, due)}): {days} giorni di ritardo",
                WorkOrderIds = [self.Id], WorkOrderCodes = [self.Code], LateDays = days
            });
        }
        return result;
    }

    private static List<MoveConflictModel> CapacityConflicts(TimeZoneInfo zone, MoveJobRef self, DateTime start, DateTime end,
        int equipmentId, string? equipmentCode, int capacity, IReadOnlyList<MoveJobRef> occupants)
    {
        capacity = Math.Max(1, capacity);
        var overlapping = occupants.Where(o => o.Start is { } s && o.End is { } e && s < end && e > start).ToList();
        if (overlapping.Count == 0 || end <= start) return [];

        var points = new SortedSet<DateTime> { start, end };
        foreach (var o in overlapping)
        {
            if (o.Start!.Value > start) points.Add(o.Start.Value);
            if (o.End!.Value < end) points.Add(o.End.Value);
        }
        var ordered = points.ToList();

        var result = new List<MoveConflictModel>();
        DateTime? runFrom = null;
        DateTime runTo = default;
        var involved = new Dictionary<int, MoveJobRef>();

        void Flush()
        {
            if (runFrom is null) return;
            var list = involved.Values.OrderBy(o => o.Id).ToList();
            var minutes = (int)Math.Ceiling((runTo - runFrom.Value).TotalMinutes);
            result.Add(new MoveConflictModel
            {
                Code = MoveConflictCode.MachineOverCapacity,
                Message = $"La macchina {equipmentCode} supera la capacità ({capacity}) dal {PlanningDateFormat.Compact(zone, runFrom.Value)} al {PlanningDateFormat.Compact(zone, runTo)} con {string.Join(", ", list.Select(o => o.Code))}: {minutes} min di conflitto",
                WorkOrderIds = new[] { self.Id }.Concat(list.Select(o => o.Id)).ToList(),
                WorkOrderCodes = new[] { self.Code }.Concat(list.Select(o => o.Code)).ToList(),
                EquipmentId = equipmentId, Minutes = minutes, From = runFrom, To = runTo
            });
            runFrom = null;
            involved = [];
        }

        for (var i = 0; i + 1 < ordered.Count; i++)
        {
            var a = ordered[i];
            var b = ordered[i + 1];
            var active = overlapping.Where(o => o.Start!.Value < b && o.End!.Value > a).ToList();
            if (active.Count + 1 > capacity)
            {
                runFrom ??= a;
                runTo = b;
                foreach (var o in active) involved[o.Id] = o;
            }
            else Flush();
        }
        Flush();
        return result;
    }
}
