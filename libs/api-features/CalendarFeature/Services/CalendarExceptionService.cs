using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.CalendarFeature.Models;
using MicroMES.ApiFeatures.CalendarFeature.Repositories;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;

namespace MicroMES.ApiFeatures.CalendarFeature.Services;

/// <summary>
/// Exceptions of a calendar (shared) or of a machine. Overlaps among exceptions of the same owner are allowed and
/// reported as a warning in the response (<see cref="CalendarExceptionModel.Overlaps"/>): at calculation time the most
/// recent one wins (see <see cref="AvailabilityResolver"/>).
/// Exceptions and their slots are hard-deleted (they are not master data; no other table references them).
/// </summary>
public class CalendarExceptionService(IUnitOfWork unitOfWork, EffectiveCalendarService effective)
{
    public const int MaxRangeDays = 3660;

    /// <summary>One exception independent of the owner type (calendar or machine).</summary>
    private sealed class Row
    {
        public int Id { get; set; }
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        public CalendarExceptionKind Kind { get; set; }
        public bool Recurring { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<TimeSlot> Slots { get; set; } = [];
    }

    private interface IScope
    {
        Task<bool> OwnerExistsAsync(int ownerId);
        Task<List<Row>> ListAsync(int ownerId);
        /// <summary>Insert (Id 0) or update; the row gets its id. Returns errors or null.</summary>
        Task<IReadOnlyCollection<string>?> SaveAsync(int ownerId, Row row);
        Task<IReadOnlyCollection<string>?> DeleteAsync(int ownerId, int id);
        string OwnerNotFound { get; }
    }

    private sealed class CalendarScope(IUnitOfWork uow) : IScope
    {
        private ICalendarExceptionRepository Repo => uow.GetRepository<ICalendarExceptionRepository>();
        private ICalendarExceptionSlotRepository SlotRepo => uow.GetRepository<ICalendarExceptionSlotRepository>();
        public string OwnerNotFound => "Calendario non trovato.";

        public async Task<bool> OwnerExistsAsync(int ownerId) => await uow.GetRepository<IWorkCalendarRepository>().GetSingleAsync(ownerId) is not null;

        public async Task<List<Row>> ListAsync(int ownerId)
        {
            var rows = new List<(CalendarException E, Row R)>();
            await foreach (var e in Repo.FetchAsync(x => x.WorkCalendarId == ownerId))
                rows.Add((e, new Row { Id = e.Id, FromDate = e.FromDate, ToDate = e.ToDate, Kind = e.Kind, Recurring = e.IsRecurringYearly, Reason = e.Reason, CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt }));
            var ids = rows.Select(r => r.E.Id).ToList();
            await foreach (var s in SlotRepo.FetchAsync(x => ids.Contains(x.CalendarExceptionId)))
                rows.First(r => r.E.Id == s.CalendarExceptionId).R.Slots.Add(new TimeSlot(s.StartTime, s.EndTime));
            return rows.Select(r => r.R).ToList();
        }

        public async Task<IReadOnlyCollection<string>?> SaveAsync(int ownerId, Row row)
        {
            CalendarException? e = null;
            if (row.Id != 0) await foreach (var x in Repo.FetchAsync(y => y.Id == row.Id)) e = x;
            var isNew = e is null;
            e ??= new CalendarException { WorkCalendarId = ownerId, CreatedAt = row.CreatedAt };
            e.FromDate = row.FromDate; e.ToDate = row.ToDate; e.Kind = row.Kind; e.IsRecurringYearly = row.Recurring;
            e.Reason = row.Reason; e.UpdatedAt = row.UpdatedAt;
            var saved = isNew ? await Repo.AddAsync(e) : await Repo.UpdateAsync(e);
            if (!saved.IsSuccessful) return saved.Errors;
            row.Id = e.Id;
            foreach (var old in await SlotRepo.FetchAsync(x => x.CalendarExceptionId == e.Id).ToListAsync())
                if (!(await SlotRepo.DeleteAsync(old)).IsSuccessful) return ["Impossibile salvare le fasce."];
            foreach (var s in row.Slots)
            {
                var added = await SlotRepo.AddAsync(new CalendarExceptionSlot { CalendarExceptionId = e.Id, StartTime = s.Start, EndTime = s.End });
                if (!added.IsSuccessful) return added.Errors;
            }
            return null;
        }

        public async Task<IReadOnlyCollection<string>?> DeleteAsync(int ownerId, int id)
        {
            foreach (var s in await SlotRepo.FetchAsync(x => x.CalendarExceptionId == id).ToListAsync())
                if (!(await SlotRepo.DeleteAsync(s)).IsSuccessful) return ["Impossibile eliminare le fasce."];
            CalendarException? found = null;
            await foreach (var e in Repo.FetchAsync(x => x.Id == id && x.WorkCalendarId == ownerId)) found = e;
            if (found is null) return null;
            var deleted = await Repo.DeleteAsync(found);
            return deleted.IsSuccessful ? null : deleted.Errors;
        }
    }

    private sealed class EquipmentScope(IUnitOfWork uow) : IScope
    {
        private IEquipmentExceptionRepository Repo => uow.GetRepository<IEquipmentExceptionRepository>();
        private IEquipmentExceptionSlotRepository SlotRepo => uow.GetRepository<IEquipmentExceptionSlotRepository>();
        public string OwnerNotFound => "Macchina o postazione non trovata.";

        public async Task<bool> OwnerExistsAsync(int ownerId) => await uow.GetRepository<IEquipmentRepository>().GetSingleAsync(ownerId) is not null;

        public async Task<List<Row>> ListAsync(int ownerId)
        {
            var rows = new List<Row>();
            await foreach (var e in Repo.FetchAsync(x => x.EquipmentId == ownerId))
                rows.Add(new Row { Id = e.Id, FromDate = e.FromDate, ToDate = e.ToDate, Kind = e.Kind, Recurring = e.IsRecurringYearly, Reason = e.Reason, CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt });
            var ids = rows.Select(r => r.Id).ToList();
            await foreach (var s in SlotRepo.FetchAsync(x => ids.Contains(x.EquipmentExceptionId)))
                rows.First(r => r.Id == s.EquipmentExceptionId).Slots.Add(new TimeSlot(s.StartTime, s.EndTime));
            return rows;
        }

        public async Task<IReadOnlyCollection<string>?> SaveAsync(int ownerId, Row row)
        {
            EquipmentException? e = null;
            if (row.Id != 0) await foreach (var x in Repo.FetchAsync(y => y.Id == row.Id)) e = x;
            var isNew = e is null;
            e ??= new EquipmentException { EquipmentId = ownerId, CreatedAt = row.CreatedAt };
            e.FromDate = row.FromDate; e.ToDate = row.ToDate; e.Kind = row.Kind; e.IsRecurringYearly = row.Recurring;
            e.Reason = row.Reason; e.UpdatedAt = row.UpdatedAt;
            var saved = isNew ? await Repo.AddAsync(e) : await Repo.UpdateAsync(e);
            if (!saved.IsSuccessful) return saved.Errors;
            row.Id = e.Id;
            foreach (var old in await SlotRepo.FetchAsync(x => x.EquipmentExceptionId == e.Id).ToListAsync())
                if (!(await SlotRepo.DeleteAsync(old)).IsSuccessful) return ["Impossibile salvare le fasce."];
            foreach (var s in row.Slots)
            {
                var added = await SlotRepo.AddAsync(new EquipmentExceptionSlot { EquipmentExceptionId = e.Id, StartTime = s.Start, EndTime = s.End });
                if (!added.IsSuccessful) return added.Errors;
            }
            return null;
        }

        public async Task<IReadOnlyCollection<string>?> DeleteAsync(int ownerId, int id)
        {
            foreach (var s in await SlotRepo.FetchAsync(x => x.EquipmentExceptionId == id).ToListAsync())
                if (!(await SlotRepo.DeleteAsync(s)).IsSuccessful) return ["Impossibile eliminare le fasce."];
            EquipmentException? found = null;
            await foreach (var e in Repo.FetchAsync(x => x.Id == id && x.EquipmentId == ownerId)) found = e;
            if (found is null) return null;
            var deleted = await Repo.DeleteAsync(found);
            return deleted.IsSuccessful ? null : deleted.Errors;
        }
    }

    private IScope CalendarOwner => new CalendarScope(unitOfWork);
    private IScope EquipmentOwner => new EquipmentScope(unitOfWork);

    // ---- calendar exceptions -------------------------------------------------------------------------

    public Task<CalendarResult<List<CalendarExceptionModel>>> ListForCalendarAsync(int calendarId, int? year) => ListAsync(CalendarOwner, calendarId, year);
    public Task<CalendarResult<CalendarExceptionModel>> CreateForCalendarAsync(int calendarId, SaveCalendarExceptionRequest r) => SaveAsync(CalendarOwner, calendarId, null, r);
    public Task<CalendarResult<CalendarExceptionModel>> UpdateForCalendarAsync(int calendarId, int id, SaveCalendarExceptionRequest r) => SaveAsync(CalendarOwner, calendarId, id, r);
    public Task<CalendarResult<bool>> DeleteForCalendarAsync(int calendarId, int id) => DeleteAsync(CalendarOwner, calendarId, id);

    // ---- machine exceptions ----------------------------------------------------------------------------

    public Task<CalendarResult<List<CalendarExceptionModel>>> ListForEquipmentAsync(int equipmentId, int? year) => ListAsync(EquipmentOwner, equipmentId, year);
    public Task<CalendarResult<CalendarExceptionModel>> CreateForEquipmentAsync(int equipmentId, SaveCalendarExceptionRequest r) => SaveAsync(EquipmentOwner, equipmentId, null, r);
    public Task<CalendarResult<CalendarExceptionModel>> UpdateForEquipmentAsync(int equipmentId, int id, SaveCalendarExceptionRequest r) => SaveAsync(EquipmentOwner, equipmentId, id, r);
    public Task<CalendarResult<bool>> DeleteForEquipmentAsync(int equipmentId, int id) => DeleteAsync(EquipmentOwner, equipmentId, id);

    // ---- Italian holidays -----------------------------------------------------------------------------

    public async Task<CalendarResult<ItalianHolidaysPreviewModel>> PreviewItalianHolidaysAsync(int calendarId, int year)
    {
        if (year is < 1970 or > 2200) return CalendarResult<ItalianHolidaysPreviewModel>.Invalid("L'anno deve essere compreso tra 1970 e 2200.");
        var scope = CalendarOwner;
        if (!await scope.OwnerExistsAsync(calendarId)) return CalendarResult<ItalianHolidaysPreviewModel>.NotFound(scope.OwnerNotFound);
        var existing = await scope.ListAsync(calendarId);
        return CalendarResult<ItalianHolidaysPreviewModel>.Ok(new ItalianHolidaysPreviewModel
        {
            Year = year,
            Holidays = ItalianHolidays.ForYear(year)
                .Select(h => new ItalianHolidayModel { Date = h.Date, Name = h.Name, AlreadyPresent = existing.Any(e => ToDomain(e).Covers(h.Date)) })
                .ToList()
        });
    }

    public async Task<CalendarResult<ImportItalianHolidaysResultModel>> ImportItalianHolidaysAsync(int calendarId, ImportItalianHolidaysRequest request)
    {
        var scope = CalendarOwner;
        unitOfWork.BeginTransaction();
        try
        {
            var result = await ImportCoreAsync(scope, calendarId, request);
            if (result.IsSuccessful) unitOfWork.Commit(); else unitOfWork.Rollback();
            return result;
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return CalendarResult<ImportItalianHolidaysResultModel>.Failed(CalendarFailure.None, "Impossibile importare le festività.", [ex.Message]);
        }
    }

    private async Task<CalendarResult<ImportItalianHolidaysResultModel>> ImportCoreAsync(IScope scope, int calendarId, ImportItalianHolidaysRequest request)
    {
        if (request.Year is < 1970 or > 2200) return CalendarResult<ImportItalianHolidaysResultModel>.Invalid("L'anno deve essere compreso tra 1970 e 2200.");
        if (!await scope.OwnerExistsAsync(calendarId)) return CalendarResult<ImportItalianHolidaysResultModel>.NotFound(scope.OwnerNotFound);
        var holidays = ItalianHolidays.ForYear(request.Year);
        var selected = request.Dates is null ? holidays : holidays.Where(h => request.Dates.Contains(h.Date)).ToList();
        if (request.Dates is not null && request.Dates.Any(d => holidays.All(h => h.Date != d)))
            return CalendarResult<ImportItalianHolidaysResultModel>.Invalid($"Alcune date non sono festività nazionali del {request.Year}: aggiorna l'anteprima.");

        var existing = await scope.ListAsync(calendarId);
        var created = new List<CalendarExceptionModel>();
        var skipped = 0;
        foreach (var h in selected)
        {
            if (existing.Any(e => ToDomain(e).Covers(h.Date))) { skipped++; continue; }
            var now = DateTime.UtcNow;
            var row = new Row { FromDate = h.Date, ToDate = h.Date, Kind = CalendarExceptionKind.Closed, Reason = h.Name, CreatedAt = now, UpdatedAt = now };
            if (await scope.SaveAsync(calendarId, row) is { } errors)
                return CalendarResult<ImportItalianHolidaysResultModel>.Failed(CalendarFailure.None, "Impossibile salvare le festività.", errors);
            existing.Add(row);
            created.Add(ToModel(calendarId, row, []));
        }
        return CalendarResult<ImportItalianHolidaysResultModel>.Ok(new ImportItalianHolidaysResultModel { Year = request.Year, Created = created, Skipped = skipped });
    }

    // ---- shared logic ----------------------------------------------------------------------------------

    private static async Task<CalendarResult<List<CalendarExceptionModel>>> ListAsync(IScope scope, int ownerId, int? year)
    {
        if (!await scope.OwnerExistsAsync(ownerId)) return CalendarResult<List<CalendarExceptionModel>>.NotFound(scope.OwnerNotFound);
        var rows = (await scope.ListAsync(ownerId))
            .Where(r => year is not { } y || r.Recurring && y >= r.FromDate.Year || !r.Recurring && r.FromDate.Year <= y && r.ToDate.Year >= y)
            .OrderBy(r => r.FromDate).ThenBy(r => r.Id)
            .Select(r => ToModel(ownerId, r, [])).ToList();
        return CalendarResult<List<CalendarExceptionModel>>.Ok(rows);
    }

    private async Task<CalendarResult<CalendarExceptionModel>> SaveAsync(IScope scope, int ownerId, int? id, SaveCalendarExceptionRequest request)
    {
        var from = request.FromDate!.Value;
        var to = request.ToDate!.Value;
        var kind = request.Kind!.Value;
        var slots = (request.Slots ?? []).Select(s => (s.StartTime!.Value, s.EndTime!.Value)).ToList();

        if (to < from) return CalendarResult<CalendarExceptionModel>.Invalid("La data finale deve essere uguale o successiva alla data iniziale.");
        if (to.DayNumber - from.DayNumber >= MaxRangeDays) return CalendarResult<CalendarExceptionModel>.Invalid("L'intervallo è troppo lungo: massimo 10 anni.");
        if (request.IsRecurringYearly && from != to)
            return CalendarResult<CalendarExceptionModel>.Invalid("Una ricorrenza annuale vale per un solo giorno: imposta la stessa data di inizio e fine.");
        if (kind == CalendarExceptionKind.Custom)
        {
            if (slots.Count == 0) return CalendarResult<CalendarExceptionModel>.Invalid("Un'eccezione con orari diversi deve avere almeno una fascia: aggiungi gli orari o scegli «Chiuso».");
            if (SlotRules.Validate(slots.Select(s => ((DayOfWeek?)null, s.Item1, s.Item2))) is { } error)
                return CalendarResult<CalendarExceptionModel>.Invalid(error);
        }
        else slots = [];

        unitOfWork.BeginTransaction();
        try
        {
            if (!await scope.OwnerExistsAsync(ownerId)) { unitOfWork.Rollback(); return CalendarResult<CalendarExceptionModel>.NotFound(scope.OwnerNotFound); }
            var all = await scope.ListAsync(ownerId);
            var now = DateTime.UtcNow;
            Row row;
            if (id is { } existingId)
            {
                var found = all.FirstOrDefault(r => r.Id == existingId);
                if (found is null) { unitOfWork.Rollback(); return CalendarResult<CalendarExceptionModel>.NotFound("Eccezione non trovata."); }
                row = found;
            }
            else row = new Row { CreatedAt = now };
            row.FromDate = from; row.ToDate = to; row.Kind = kind; row.Recurring = request.IsRecurringYearly;
            row.Reason = request.Reason.Trim(); row.UpdatedAt = now;
            row.Slots = slots.Select(s => new TimeSlot(s.Item1, s.Item2)).ToList();
            // An edited exception becomes the most recent one at its level.
            if (id is not null) row.CreatedAt = now;

            if (await scope.SaveAsync(ownerId, row) is { } errors)
            {
                unitOfWork.Rollback();
                return CalendarResult<CalendarExceptionModel>.Failed(CalendarFailure.None, "Impossibile salvare l'eccezione.", errors);
            }
            unitOfWork.Commit();
            var overlaps = all.Where(o => o.Id != row.Id && Overlap(ToDomain(o), ToDomain(row))).OrderBy(o => o.FromDate)
                .Select(o => new ExceptionOverlapModel { Id = o.Id, FromDate = o.FromDate, ToDate = o.ToDate, Kind = o.Kind, Reason = o.Reason }).ToList();
            return CalendarResult<CalendarExceptionModel>.Ok(ToModel(ownerId, row, overlaps));
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return CalendarResult<CalendarExceptionModel>.Failed(CalendarFailure.None, "Impossibile salvare l'eccezione.", [ex.Message]);
        }
    }

    private async Task<CalendarResult<bool>> DeleteAsync(IScope scope, int ownerId, int id)
    {
        if (!await scope.OwnerExistsAsync(ownerId)) return CalendarResult<bool>.NotFound(scope.OwnerNotFound);
        var all = await scope.ListAsync(ownerId);
        if (all.All(r => r.Id != id)) return CalendarResult<bool>.NotFound("Eccezione non trovata.");
        unitOfWork.BeginTransaction();
        try
        {
            if (await scope.DeleteAsync(ownerId, id) is { } errors)
            {
                unitOfWork.Rollback();
                return CalendarResult<bool>.Failed(CalendarFailure.None, "Impossibile eliminare l'eccezione.", errors);
            }
            unitOfWork.Commit();
            return CalendarResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return CalendarResult<bool>.Failed(CalendarFailure.None, "Impossibile eliminare l'eccezione.", [ex.Message]);
        }
    }

    private static AvailabilityException ToDomain(Row r)
        => EffectiveCalendarService.ToDomain(r.Id, r.FromDate, r.ToDate, r.Kind, r.Recurring, r.Reason, r.CreatedAt, AvailabilityLevel.Calendar, r.Slots);

    private static CalendarExceptionModel ToModel(int ownerId, Row r, List<ExceptionOverlapModel> overlaps) => new()
    {
        Id = r.Id, OwnerId = ownerId, FromDate = r.FromDate, ToDate = r.ToDate, Kind = r.Kind, IsRecurringYearly = r.Recurring,
        Reason = r.Reason, Slots = r.Slots.OrderBy(s => s.Start).Select(s => new TimeSlotModel { StartTime = s.Start, EndTime = s.End }).ToList(),
        Overlaps = overlaps, CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt
    };

    /// <summary>True when the two exceptions share at least one day (recurring ones expanded year by year).</summary>
    public static bool Overlap(AvailabilityException a, AvailabilityException b)
    {
        var first = Math.Min(a.FromDate.Year, b.FromDate.Year);
        var last = Math.Min(Math.Max(a.ToDate.Year, b.ToDate.Year), first + 200);
        for (var year = first; year <= last; year++)
        {
            var ra = RangeIn(a, year);
            var rb = RangeIn(b, year);
            if (ra is not null && rb is not null && ra.Value.From <= rb.Value.To && rb.Value.From <= ra.Value.To) return true;
        }
        return false;
    }

    private static (DateOnly From, DateOnly To)? RangeIn(AvailabilityException e, int year)
    {
        if (!e.IsRecurringYearly) return (e.FromDate, e.ToDate);
        if (year < e.FromDate.Year) return null;
        if (e.FromDate.Month == 2 && e.FromDate.Day == 29 && !DateTime.IsLeapYear(year)) return null;
        var d = new DateOnly(year, e.FromDate.Month, e.FromDate.Day);
        return (d, d);
    }
}
