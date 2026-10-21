using illyW.Framework.Core.RepositoryPattern;
using illyW.Framework.Core.ResultPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.CalendarFeature.Repositories;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;

namespace MicroMES.ApiFeatures.CalendarFeature.Seeding;

/// <summary>
/// Sample calendars for local development: DOPPIO-TURNO, the Italian holidays of this and next year and a summer closure
/// on STANDARD (the default calendar created by the migration), a maintenance stop and efficiency/capacity/calendar values
/// on some of the sample machines (looked up by code: missing machines are skipped, no machine is created).
/// Runs once: it does nothing when DOPPIO-TURNO already exists. Only repositories of the unit of work, one transaction.
/// </summary>
public class CalendarDevelopmentSeeder(IUnitOfWork unitOfWork, TimeProvider clock)
{
    private static async Task<T> Save<T>(Task<IResult<T>> add) where T : class
    {
        var result = await add;
        if (!result.IsSuccessful || result.Data is null)
            throw new InvalidOperationException($"Sample data not saved: {string.Join("; ", result.Errors)}");
        return result.Data;
    }

    public async Task SeedAsync()
    {
        var calendars = unitOfWork.GetRepository<IWorkCalendarRepository>();
        await foreach (var _ in calendars.FetchAsync(x => x.Code == "DOPPIO-TURNO")) return;

        unitOfWork.BeginTransaction();
        try
        {
            await SeedCoreAsync();
            unitOfWork.Commit();
        }
        catch
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            throw;
        }
    }

    private async Task SeedCoreAsync()
    {
        var calendars = unitOfWork.GetRepository<IWorkCalendarRepository>();
        var slots = unitOfWork.GetRepository<IWorkCalendarSlotRepository>();
        var exceptions = unitOfWork.GetRepository<ICalendarExceptionRepository>();
        var equipmentExceptions = unitOfWork.GetRepository<IEquipmentExceptionRepository>();
        var equipment = unitOfWork.GetRepository<IEquipmentRepository>();

        var now = clock.GetUtcNow().UtcDateTime;
        var year = now.Year;
        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

        async Task<WorkCalendar> Calendar(string code, string name, bool isDefault, params (int From, int To)[] shifts)
        {
            var calendar = await Save(calendars.AddAsync(new WorkCalendar
            {
                Code = code, Name = name, IsActive = true, IsDefault = isDefault, CreatedAt = now, UpdatedAt = now
            }));
            foreach (var day in weekdays)
                foreach (var (from, to) in shifts)
                    await Save(slots.AddAsync(new WorkCalendarSlot
                    {
                        WorkCalendarId = calendar.Id, DayOfWeek = day, StartTime = new TimeOnly(from, 0), EndTime = new TimeOnly(to, 0)
                    }));
            return calendar;
        }

        WorkCalendar? standard = null;
        await foreach (var c in calendars.FetchAsync(x => x.Code == "STANDARD")) standard = c;
        var hasDefault = false;
        await foreach (var _ in calendars.FetchAsync(x => x.IsDefault)) hasDefault = true;
        standard ??= await Calendar("STANDARD", "Standard 8-12 / 13-17, lun-ven", !hasDefault, (8, 12), (13, 17));
        var doppio = await Calendar("DOPPIO-TURNO", "Doppio turno 6-14 / 14-22, lun-ven", false, (6, 14), (14, 22));

        async Task Closed(DateOnly from, DateOnly to, string reason)
            => await Save(exceptions.AddAsync(new CalendarException
            {
                WorkCalendarId = standard.Id, FromDate = from, ToDate = to, Kind = CalendarExceptionKind.Closed,
                Reason = reason, CreatedAt = now, UpdatedAt = now
            }));

        foreach (var holiday in ItalianHolidays.ForYear(year).Concat(ItalianHolidays.ForYear(year + 1)))
            await Closed(holiday.Date, holiday.Date, holiday.Name);
        await Closed(new DateOnly(year, 8, 17), new DateOnly(year, 8, 28), "Ferie estive");

        var machines = new Dictionary<string, Equipment>();
        await foreach (var e in equipment.FetchAsync(_ => true)) machines[e.Code] = e;

        async Task Tune(string code, Action<Equipment> change)
        {
            if (!machines.TryGetValue(code, out var machine)) return;
            change(machine);
            machine.UpdatedAt = now;
            await Save(equipment.UpdateAsync(machine));
        }

        await Tune("TAG-TUB-2", m => m.EfficiencyPercent = 85m);
        await Tune("IMB-T1", m => m.Capacity = 2);
        await Tune("VER-POL-1", m => m.WorkCalendarId = doppio.Id);

        if (machines.TryGetValue("TAG-LAM-1", out var press))
        {
            var start = DateOnly.FromDateTime(now).AddDays(14);
            await Save(equipmentExceptions.AddAsync(new EquipmentException
            {
                EquipmentId = press.Id, FromDate = start, ToDate = start.AddDays(2), Kind = CalendarExceptionKind.Closed,
                Reason = "Manutenzione programmata", CreatedAt = now, UpdatedAt = now
            }));
        }
    }
}
