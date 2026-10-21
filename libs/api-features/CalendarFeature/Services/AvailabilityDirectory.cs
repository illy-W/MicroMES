using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;

namespace MicroMES.ApiFeatures.CalendarFeature.Services;

/// <summary>
/// Calendars, exceptions and machines loaded in memory: answers "what does this machine follow" without touching the database.
/// The effective availability of a machine = its own calendar or the active default, plus the calendar exceptions and
/// the machine's own exceptions (the machine wins, then the most recent).
/// </summary>
public sealed class AvailabilityDirectory
{
    private readonly Dictionary<int, WorkCalendar> _calendars;
    private readonly Dictionary<int, List<WeeklySlot>> _weekly;
    private readonly Dictionary<int, List<AvailabilityException>> _calendarExceptions;
    private readonly Dictionary<int, Equipment> _equipment;
    private readonly Dictionary<int, List<AvailabilityException>> _equipmentExceptions;
    private readonly Dictionary<int, IAvailabilitySource> _cache = [];

    public AvailabilityDirectory(
        TimeZoneInfo zone,
        Dictionary<int, WorkCalendar> calendars,
        Dictionary<int, List<WeeklySlot>> weekly,
        Dictionary<int, List<AvailabilityException>> calendarExceptions,
        Dictionary<int, Equipment> equipment,
        Dictionary<int, List<AvailabilityException>> equipmentExceptions)
    {
        Zone = zone;
        _calendars = calendars;
        _weekly = weekly;
        _calendarExceptions = calendarExceptions;
        _equipment = equipment;
        _equipmentExceptions = equipmentExceptions;
        Calculator = new WorkingTimeCalculator(zone);
    }

    public TimeZoneInfo Zone { get; }
    public WorkingTimeCalculator Calculator { get; }

    public WorkCalendar? DefaultCalendar => _calendars.Values.FirstOrDefault(c => c.IsDefault && c.IsActive);

    public WorkCalendar? CalendarOf(int? equipmentId)
    {
        if (equipmentId is { } id && _equipment.TryGetValue(id, out var e) && e.WorkCalendarId is { } cid
            && _calendars.TryGetValue(cid, out var own) && own.IsActive)
            return own;
        return DefaultCalendar;
    }

    public decimal EfficiencyOf(int? equipmentId)
        => equipmentId is { } id && _equipment.TryGetValue(id, out var e) ? e.EfficiencyPercent : 100m;

    /// <summary>Availability of a calendar alone (no machine).</summary>
    public IAvailabilitySource ForCalendar(int calendarId)
        => new EffectiveAvailability(_weekly.GetValueOrDefault(calendarId) ?? [], _calendarExceptions.GetValueOrDefault(calendarId) ?? []);

    /// <summary>Availability of a machine, or of the default calendar when null / unknown (job without a machine).</summary>
    public IAvailabilitySource ForEquipment(int? equipmentId)
    {
        var key = equipmentId ?? 0;
        if (_cache.TryGetValue(key, out var cached)) return cached;
        var calendar = CalendarOf(equipmentId);
        var exceptions = new List<AvailabilityException>();
        if (calendar is not null) exceptions.AddRange(_calendarExceptions.GetValueOrDefault(calendar.Id) ?? []);
        if (equipmentId is { } id) exceptions.AddRange(_equipmentExceptions.GetValueOrDefault(id) ?? []);
        var source = new EffectiveAvailability(calendar is null ? [] : _weekly.GetValueOrDefault(calendar.Id) ?? [], exceptions);
        return _cache[key] = source;
    }

    public decimal WeeklyHours(int calendarId)
        => SlotRules.WeeklyHours((_weekly.GetValueOrDefault(calendarId) ?? []).Select(s => (s.Start, s.End)));
}
