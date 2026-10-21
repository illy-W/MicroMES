using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.CalendarFeature.Models;
using MicroMES.ApiFeatures.CalendarFeature.Repositories;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;

namespace MicroMES.ApiFeatures.CalendarFeature.Services;

/// <summary>Read-only: loads the effective calendars (repositories injected directly, no transaction) and the company time zone.</summary>
public class EffectiveCalendarService(
    IWorkCalendarRepository calendars,
    IWorkCalendarSlotRepository calendarSlots,
    ICalendarExceptionRepository calendarExceptions,
    ICalendarExceptionSlotRepository calendarExceptionSlots,
    IEquipmentExceptionRepository equipmentExceptions,
    IEquipmentExceptionSlotRepository equipmentExceptionSlots,
    IEquipmentRepository equipment,
    ISystemSettingRepository settings,
    TimeProvider timeProvider)
{
    public const string TimeZoneKey = "CompanyTimeZone";
    public const string DefaultTimeZoneId = "Europe/Rome";

    public async Task<string> GetTimeZoneIdAsync()
    {
        await foreach (var s in settings.FetchAsync(x => x.Key == TimeZoneKey)) return s.Value;
        return DefaultTimeZoneId;
    }

    public async Task<TimeZoneInfo> GetTimeZoneAsync()
    {
        var id = await GetTimeZoneIdAsync();
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
        }
    }

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>Loads every calendar and the exceptions of the given machines (all machines when null).</summary>
    public async Task<AvailabilityDirectory> LoadAsync(IEnumerable<int>? equipmentIds = null)
    {
        var zone = await GetTimeZoneAsync();
        var cals = new Dictionary<int, WorkCalendar>();
        await foreach (var c in calendars.FetchAsync(_ => true)) cals[c.Id] = c;

        var weekly = new Dictionary<int, List<WeeklySlot>>();
        await foreach (var s in calendarSlots.FetchAsync(_ => true))
            (weekly.TryGetValue(s.WorkCalendarId, out var l) ? l : weekly[s.WorkCalendarId] = []).Add(new WeeklySlot(s.DayOfWeek, s.StartTime, s.EndTime));

        var calSlotsByException = new Dictionary<int, List<TimeSlot>>();
        await foreach (var s in calendarExceptionSlots.FetchAsync(_ => true))
            (calSlotsByException.TryGetValue(s.CalendarExceptionId, out var l) ? l : calSlotsByException[s.CalendarExceptionId] = []).Add(new TimeSlot(s.StartTime, s.EndTime));
        var calExc = new Dictionary<int, List<AvailabilityException>>();
        await foreach (var e in calendarExceptions.FetchAsync(_ => true))
            (calExc.TryGetValue(e.WorkCalendarId, out var l) ? l : calExc[e.WorkCalendarId] = [])
                .Add(ToDomain(e.Id, e.FromDate, e.ToDate, e.Kind, e.IsRecurringYearly, e.Reason, e.CreatedAt, AvailabilityLevel.Calendar,
                    calSlotsByException.GetValueOrDefault(e.Id)));

        var ids = equipmentIds?.Distinct().ToList();
        var eq = new Dictionary<int, Equipment>();
        await foreach (var e in equipment.FetchAsync(x => ids == null || ids.Contains(x.Id))) eq[e.Id] = e;
        var eqIds = eq.Keys.ToList();

        var eqSlotsByException = new Dictionary<int, List<TimeSlot>>();
        await foreach (var s in equipmentExceptionSlots.FetchAsync(_ => true))
            (eqSlotsByException.TryGetValue(s.EquipmentExceptionId, out var l) ? l : eqSlotsByException[s.EquipmentExceptionId] = []).Add(new TimeSlot(s.StartTime, s.EndTime));
        var eqExc = new Dictionary<int, List<AvailabilityException>>();
        await foreach (var e in equipmentExceptions.FetchAsync(x => eqIds.Contains(x.EquipmentId)))
            (eqExc.TryGetValue(e.EquipmentId, out var l) ? l : eqExc[e.EquipmentId] = [])
                .Add(ToDomain(e.Id, e.FromDate, e.ToDate, e.Kind, e.IsRecurringYearly, e.Reason, e.CreatedAt, AvailabilityLevel.Resource,
                    eqSlotsByException.GetValueOrDefault(e.Id)));

        return new AvailabilityDirectory(zone, cals, weekly, calExc, eq, eqExc);
    }

    public static AvailabilityException ToDomain(int id, DateOnly from, DateOnly to, CalendarExceptionKind kind, bool recurring,
        string reason, DateTime createdAt, AvailabilityLevel level, List<TimeSlot>? slots)
        => new()
        {
            Id = id, FromDate = from, ToDate = to, Kind = kind, IsRecurringYearly = recurring, Reason = reason,
            CreatedAt = createdAt, Level = level, Slots = slots ?? []
        };

    /// <summary>Effective calendar summaries (own or default + next closures) of the given machines.</summary>
    public async Task<Dictionary<int, EffectiveCalendarModel>> SummariesAsync(IReadOnlyCollection<Equipment> machines)
    {
        var result = new Dictionary<int, EffectiveCalendarModel>();
        if (machines.Count == 0) return result;
        var directory = await LoadAsync(machines.Select(m => m.Id));
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, directory.Zone));
        foreach (var m in machines)
        {
            var calendar = directory.CalendarOf(m.Id);
            if (calendar is null) continue;
            var source = directory.ForEquipment(m.Id);
            var closures = source.Exceptions.Where(e => e.Kind == CalendarExceptionKind.Closed)
                .SelectMany(e => Upcoming(e, today))
                .OrderBy(c => c.FromDate).ThenBy(c => c.Scope).Take(5).ToList();
            result[m.Id] = new EffectiveCalendarModel
            {
                CalendarId = calendar.Id, Code = calendar.Code, Name = calendar.Name,
                IsInherited = calendar.Id != m.WorkCalendarId,
                WeeklyHours = directory.WeeklyHours(calendar.Id), UpcomingClosures = closures
            };
        }
        return result;
    }

    private static IEnumerable<UpcomingClosureModel> Upcoming(AvailabilityException e, DateOnly today)
    {
        var scope = e.Level == AvailabilityLevel.Resource ? ExceptionScope.Equipment : ExceptionScope.Calendar;
        var horizon = today.AddDays(365);
        if (!e.IsRecurringYearly)
        {
            if (e.ToDate >= today && e.FromDate <= horizon)
                yield return new UpcomingClosureModel { FromDate = e.FromDate, ToDate = e.ToDate, Reason = e.Reason, Scope = scope };
            yield break;
        }
        for (var year = today.Year; year <= today.Year + 4; year++)
        {
            if (year < e.FromDate.Year) continue;
            if (e.FromDate.Month == 2 && e.FromDate.Day == 29 && !DateTime.IsLeapYear(year)) continue;
            var d = new DateOnly(year, e.FromDate.Month, e.FromDate.Day);
            if (d < today) continue;
            if (d <= horizon)
                yield return new UpcomingClosureModel { FromDate = d, ToDate = d, Reason = e.Reason, Scope = scope };
            yield break;
        }
    }

    /// <summary>Day-by-day availability of a calendar or machine source (month view).</summary>
    public static List<CalendarDayModel> Days(IAvailabilitySource source, DateOnly from, DateOnly to)
    {
        var days = new List<CalendarDayModel>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var r = AvailabilityResolver.Resolve(source, d);
            var status = r.Exception is null
                ? (r.Slots.Count > 0 ? CalendarDayStatus.Working : CalendarDayStatus.NonWorking)
                : (r.Exception.Kind == CalendarExceptionKind.Closed ? CalendarDayStatus.Closed : CalendarDayStatus.Custom);
            days.Add(new CalendarDayModel
            {
                Date = d, DayOfWeek = d.DayOfWeek, Status = status,
                ExceptionScope = r.Exception is null ? null : r.Source == DaySource.ResourceException ? Models.ExceptionScope.Equipment : Models.ExceptionScope.Calendar,
                Reason = r.Exception?.Reason,
                Slots = r.Slots.Select(s => new TimeSlotModel { StartTime = s.Start, EndTime = s.End }).ToList()
            });
        }
        return days;
    }
}
