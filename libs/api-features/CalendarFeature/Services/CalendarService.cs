using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.CalendarFeature.Models;
using MicroMES.ApiFeatures.CalendarFeature.Repositories;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;

namespace MicroMES.ApiFeatures.CalendarFeature.Services;

/// <summary>Calendars and their weekly slots. Invariant: exactly one default calendar, always active.</summary>
public class CalendarService(IUnitOfWork unitOfWork, EffectiveCalendarService effective)
{
    private IWorkCalendarRepository Calendars => unitOfWork.GetRepository<IWorkCalendarRepository>();
    private IWorkCalendarSlotRepository Slots => unitOfWork.GetRepository<IWorkCalendarSlotRepository>();

    public async Task<CalendarResult<List<WorkCalendarModel>>> GetAllAsync(bool includeInactive)
    {
        var items = new List<WorkCalendar>();
        await foreach (var c in Calendars.FetchAsync(x => includeInactive || x.IsActive)) items.Add(c);
        var slots = await LoadSlotsAsync(null);
        var models = items.OrderByDescending(c => c.IsDefault).ThenBy(c => c.Code, StringComparer.Ordinal)
            .Select(c => ToModel(c, slots.Where(s => s.WorkCalendarId == c.Id))).ToList();
        return CalendarResult<List<WorkCalendarModel>>.Ok(models);
    }

    public async Task<CalendarResult<WorkCalendarModel>> GetAsync(int id)
    {
        var c = await FindAsync(id);
        return c is null ? NotFound() : CalendarResult<WorkCalendarModel>.Ok(ToModel(c, await LoadSlotsAsync(id)));
    }

    public Task<CalendarResult<WorkCalendarModel>> CreateAsync(CreateWorkCalendarRequest request)
        => InTransactionAsync(async () =>
    {
        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, null)) return CodeConflict();
        var slots = ToSlots(request.Slots);
        if (Validate(slots, active: true) is { } error) return CalendarResult<WorkCalendarModel>.Invalid(error);

        var now = DateTime.UtcNow;
        var calendar = new WorkCalendar
        {
            Code = code, Name = request.Name.Trim(), Description = request.Description,
            IsActive = true, IsDefault = false, CreatedAt = now, UpdatedAt = now
        };
        return await SaveAsync(calendar, slots, isNew: true);
    });

    public Task<CalendarResult<WorkCalendarModel>> UpdateAsync(int id, UpdateWorkCalendarRequest request)
        => InTransactionAsync(async () =>
    {
        var calendar = await FindAsync(id);
        if (calendar is null) return NotFound();
        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, id)) return CodeConflict();
        var active = request.IsActive!.Value;
        var slots = ToSlots(request.Slots);
        if (Validate(slots, active) is { } error) return CalendarResult<WorkCalendarModel>.Invalid(error);
        if (!active && await DeactivationBlockedAsync(calendar) is { } blocked) return CalendarResult<WorkCalendarModel>.Conflict(blocked);

        calendar.Code = code;
        calendar.Name = request.Name.Trim();
        calendar.Description = request.Description;
        calendar.IsActive = active;
        calendar.UpdatedAt = DateTime.UtcNow;
        return await SaveAsync(calendar, slots, isNew: false);
    });

    /// <summary>Copy with a new code and name: same slots, not default, exceptions are not copied.</summary>
    public Task<CalendarResult<WorkCalendarModel>> DuplicateAsync(int id, DuplicateWorkCalendarRequest request)
        => InTransactionAsync(async () =>
    {
        var source = await FindAsync(id);
        if (source is null) return NotFound();
        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, null)) return CodeConflict();
        var slots = (await LoadSlotsAsync(id)).Select(s => (s.DayOfWeek, s.StartTime, s.EndTime)).ToList();
        var now = DateTime.UtcNow;
        var copy = new WorkCalendar
        {
            Code = code, Name = request.Name.Trim(), Description = source.Description,
            IsActive = true, IsDefault = false, CreatedAt = now, UpdatedAt = now
        };
        return await SaveAsync(copy, slots, isNew: true);
    });

    /// <summary>Makes the calendar the company default; the previous default is released first (one default at any time).</summary>
    public Task<CalendarResult<WorkCalendarModel>> SetDefaultAsync(int id)
        => InTransactionAsync(async () =>
    {
        var calendar = await FindAsync(id);
        if (calendar is null) return NotFound();
        if (!calendar.IsActive)
            return CalendarResult<WorkCalendarModel>.Conflict("Il calendario è disattivato: riattivalo prima di impostarlo come predefinito.");
        if (!calendar.IsDefault)
        {
            var previous = new List<WorkCalendar>();
            await foreach (var c in Calendars.FetchAsync(x => x.IsDefault && x.Id != id)) previous.Add(c);
            foreach (var p in previous)
            {
                p.IsDefault = false;
                p.UpdatedAt = DateTime.UtcNow;
                var cleared = await Calendars.UpdateAsync(p);
                if (!cleared.IsSuccessful) return Fail(cleared.Errors);
            }
            calendar.IsDefault = true;
            calendar.UpdatedAt = DateTime.UtcNow;
            var set = await Calendars.UpdateAsync(calendar);
            if (!set.IsSuccessful) return Fail(set.Errors);
        }
        return CalendarResult<WorkCalendarModel>.Ok(ToModel(calendar, await LoadSlotsAsync(id)));
    });

    /// <summary>Soft-delete (idempotent). The default calendar and calendars used by active machines cannot be deactivated.</summary>
    public Task<CalendarResult<WorkCalendarModel>> DeactivateAsync(int id)
        => InTransactionAsync(async () =>
    {
        var calendar = await FindAsync(id);
        if (calendar is null) return NotFound();
        if (calendar.IsActive)
        {
            if (await DeactivationBlockedAsync(calendar) is { } blocked) return CalendarResult<WorkCalendarModel>.Conflict(blocked);
            calendar.IsActive = false;
            calendar.UpdatedAt = DateTime.UtcNow;
            var updated = await Calendars.UpdateAsync(calendar);
            if (!updated.IsSuccessful) return Fail(updated.Errors);
        }
        return CalendarResult<WorkCalendarModel>.Ok(ToModel(calendar, await LoadSlotsAsync(id)));
    });

    // ---- company time zone ---------------------------------------------------------------------------

    public async Task<CalendarResult<CompanyTimeZoneModel>> GetTimeZoneAsync()
        => CalendarResult<CompanyTimeZoneModel>.Ok(new CompanyTimeZoneModel { TimeZoneId = await effective.GetTimeZoneIdAsync() });

    public async Task<CalendarResult<CompanyTimeZoneModel>> SetTimeZoneAsync(UpdateCompanyTimeZoneRequest request)
    {
        var id = request.TimeZoneId.Trim();
        try { TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return CalendarResult<CompanyTimeZoneModel>.Invalid($"Il fuso orario «{id}» non esiste: usa un identificatore IANA come Europe/Rome.");
        }

        var repo = unitOfWork.GetRepository<ISystemSettingRepository>();
        SystemSetting? current = null;
        await foreach (var s in repo.FetchAsync(x => x.Key == EffectiveCalendarService.TimeZoneKey)) current = s;
        var saved = current is null
            ? await repo.AddAsync(new SystemSetting { Key = EffectiveCalendarService.TimeZoneKey, Value = id })
            : await repo.UpdateAsync(Set(current, id));
        return saved.IsSuccessful
            ? CalendarResult<CompanyTimeZoneModel>.Ok(new CompanyTimeZoneModel { TimeZoneId = id })
            : CalendarResult<CompanyTimeZoneModel>.Failed(CalendarFailure.None, "Impossibile salvare il fuso orario.", saved.Errors);
    }

    private static SystemSetting Set(SystemSetting s, string value) { s.Value = value; return s; }

    // ---- preview and days -----------------------------------------------------------------------------

    public async Task<CalendarResult<CalendarPreviewModel>> PreviewCalendarAsync(int calendarId, DateTime start, decimal minutes)
    {
        var directory = await effective.LoadAsync([]);
        var calendar = await FindAsync(calendarId);
        if (calendar is null) return CalendarResult<CalendarPreviewModel>.NotFound("Calendario non trovato.");
        return Preview(directory, directory.ForCalendar(calendarId), calendarId, null, start, minutes, 100m);
    }

    public async Task<CalendarResult<CalendarPreviewModel>> PreviewEquipmentAsync(int equipmentId, DateTime start, decimal minutes)
    {
        var directory = await effective.LoadAsync([equipmentId]);
        var machine = await unitOfWork.GetRepository<IEquipmentRepository>().GetSingleAsync(equipmentId);
        if (machine is null) return CalendarResult<CalendarPreviewModel>.NotFound("Macchina o postazione non trovata.");
        return Preview(directory, directory.ForEquipment(equipmentId), directory.CalendarOf(equipmentId)?.Id, equipmentId, start, minutes,
            machine.EfficiencyPercent);
    }

    private static CalendarResult<CalendarPreviewModel> Preview(AvailabilityDirectory directory, IAvailabilitySource source,
        int? calendarId, int? equipmentId, DateTime start, decimal minutes, decimal efficiency)
    {
        if (minutes < 0) return CalendarResult<CalendarPreviewModel>.Invalid("I minuti devono essere zero o più.");
        var utc = start.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(start, DateTimeKind.Utc) : start.ToUniversalTime();
        var real = WorkingTimeCalculator.ApplyEfficiency(minutes, efficiency);
        var first = directory.Calculator.NextWorkingInstant(source, utc);
        if (!first.IsSuccess) return CalendarResult<CalendarPreviewModel>.Invalid(first.Error!);
        var end = directory.Calculator.AddWorkingMinutes(source, utc, real);
        if (!end.IsSuccess) return CalendarResult<CalendarPreviewModel>.Invalid(end.Error!);
        return CalendarResult<CalendarPreviewModel>.Ok(new CalendarPreviewModel
        {
            CalendarId = calendarId, EquipmentId = equipmentId, TimeZoneId = directory.Zone.Id, Start = utc,
            FirstWorkingInstant = first.Value, StandardMinutes = minutes, EfficiencyPercent = efficiency,
            WorkingMinutes = Math.Round(real, 4), End = end.Value
        });
    }

    public const int MaxDaysRange = 400;

    public async Task<CalendarResult<List<CalendarDayModel>>> GetDaysAsync(int? calendarId, int? equipmentId, DateOnly from, DateOnly to)
    {
        if (to < from) return CalendarResult<List<CalendarDayModel>>.Invalid("La data finale deve essere uguale o successiva alla iniziale.");
        if (to.DayNumber - from.DayNumber >= MaxDaysRange)
            return CalendarResult<List<CalendarDayModel>>.Invalid($"L'intervallo massimo è di {MaxDaysRange} giorni: riduci il periodo.");
        var directory = await effective.LoadAsync(equipmentId is { } e ? [e] : []);
        IAvailabilitySource source;
        if (equipmentId is { } eid)
        {
            if (await unitOfWork.GetRepository<IEquipmentRepository>().GetSingleAsync(eid) is null)
                return CalendarResult<List<CalendarDayModel>>.NotFound("Macchina o postazione non trovata.");
            source = directory.ForEquipment(eid);
        }
        else
        {
            if (calendarId is null || await FindAsync(calendarId.Value) is null)
                return CalendarResult<List<CalendarDayModel>>.NotFound("Calendario non trovato.");
            source = directory.ForCalendar(calendarId.Value);
        }
        return CalendarResult<List<CalendarDayModel>>.Ok(EffectiveCalendarService.Days(source, from, to));
    }

    // ---- helpers ----------------------------------------------------------------------------------------

    private async Task<string?> DeactivationBlockedAsync(WorkCalendar calendar)
    {
        if (calendar.IsDefault)
            return "Il calendario è quello predefinito: imposta prima un altro calendario come predefinito.";
        await foreach (var m in unitOfWork.GetRepository<IEquipmentRepository>().FetchAsync(x => x.WorkCalendarId == calendar.Id && x.IsActive))
            return $"Il calendario è usato dalla macchina {m.Code}: cambia prima il calendario delle macchine che lo usano.";
        return null;
    }

    private static string? Validate(List<(DayOfWeek Day, TimeOnly Start, TimeOnly End)> slots, bool active)
    {
        if (active && slots.Count == 0)
            return "Un calendario attivo deve avere almeno una fascia di lavoro: aggiungi gli orari di almeno un giorno.";
        return SlotRules.Validate(slots.Select(s => ((DayOfWeek?)s.Day, s.Start, s.End)));
    }

    private static List<(DayOfWeek Day, TimeOnly Start, TimeOnly End)> ToSlots(List<WorkCalendarSlotRequest>? slots)
        => (slots ?? []).Select(s => (s.DayOfWeek!.Value, s.StartTime!.Value, s.EndTime!.Value)).ToList();

    private async Task<WorkCalendar?> FindAsync(int id)
    {
        await foreach (var c in Calendars.FetchAsync(x => x.Id == id)) return c;
        return null;
    }

    private async Task<List<WorkCalendarSlot>> LoadSlotsAsync(int? calendarId)
    {
        var list = new List<WorkCalendarSlot>();
        await foreach (var s in Slots.FetchAsync(x => calendarId == null || x.WorkCalendarId == calendarId)) list.Add(s);
        return list;
    }

    private async Task<CalendarResult<WorkCalendarModel>> SaveAsync(WorkCalendar calendar,
        List<(DayOfWeek Day, TimeOnly Start, TimeOnly End)> slots, bool isNew)
    {
        var saved = isNew ? await Calendars.AddAsync(calendar) : await Calendars.UpdateAsync(calendar);
        if (!saved.IsSuccessful || saved.Data is null) return Fail(saved.Errors);

        foreach (var old in await LoadSlotsAsync(calendar.Id))
        {
            // Weekly slots are a replaceable child set of the calendar (like the machine <-> segment links).
            var deleted = await Slots.DeleteAsync(old);
            if (!deleted.IsSuccessful) return Fail(deleted.Errors);
        }
        foreach (var s in slots)
        {
            var added = await Slots.AddAsync(new WorkCalendarSlot { WorkCalendarId = calendar.Id, DayOfWeek = s.Day, StartTime = s.Start, EndTime = s.End });
            if (!added.IsSuccessful) return Fail(added.Errors);
        }
        return CalendarResult<WorkCalendarModel>.Ok(ToModel(calendar, await LoadSlotsAsync(calendar.Id)));
    }

    private static CalendarResult<WorkCalendarModel> Fail(IReadOnlyCollection<string> errors)
        => CalendarResult<WorkCalendarModel>.Failed(CalendarFailure.None, "Impossibile salvare il calendario.", errors);

    private async Task<CalendarResult<WorkCalendarModel>> InTransactionAsync(Func<Task<CalendarResult<WorkCalendarModel>>> action)
    {
        unitOfWork.BeginTransaction();
        try
        {
            var result = await action();
            if (result.IsSuccessful) unitOfWork.Commit();
            else unitOfWork.Rollback();
            return result;
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return Fail([ex.Message]);
        }
    }

    public static WorkCalendarModel ToModel(WorkCalendar c, IEnumerable<WorkCalendarSlot> slots)
    {
        var list = slots.OrderBy(s => s.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)s.DayOfWeek).ThenBy(s => s.StartTime).ToList();
        return new WorkCalendarModel
        {
            Id = c.Id, Code = c.Code, Name = c.Name, Description = c.Description, IsActive = c.IsActive, IsDefault = c.IsDefault,
            WeeklyHours = SlotRules.WeeklyHours(list.Select(s => (s.StartTime, s.EndTime))),
            Slots = list.Select(s => new WorkCalendarSlotModel { DayOfWeek = s.DayOfWeek, StartTime = s.StartTime, EndTime = s.EndTime }).ToList(),
            CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt
        };
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private async Task<bool> CodeExistsAsync(string code, int? excludeId)
    {
        await foreach (var _ in Calendars.FetchAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId))) return true;
        return false;
    }

    private static CalendarResult<WorkCalendarModel> NotFound() => CalendarResult<WorkCalendarModel>.NotFound("Calendario non trovato.");

    private static CalendarResult<WorkCalendarModel> CodeConflict()
        => CalendarResult<WorkCalendarModel>.Conflict("Esiste già un calendario con questo codice.");
}
