using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Models;
using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

public class CalendarApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static TimeOnly T(int h, int m = 0) => new(h, m);
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    private static WorkCalendarSlotRequest Slot(DayOfWeek day, int from, int to)
        => new() { DayOfWeek = day, StartTime = T(from), EndTime = T(to) };

    private static List<WorkCalendarSlotRequest> MonFri(int from = 8, int to = 17)
        => new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            .Select(d => Slot(d, from, to)).ToList();

    private Task<ApiResult<WorkCalendarModel>> CreateRaw(string code, List<WorkCalendarSlotRequest>? slots = null)
        => Api.PostAsync<WorkCalendarModel>("/api/calendars",
            new CreateWorkCalendarRequest { Code = code, Name = $"Calendario {code}", Slots = slots ?? MonFri() });

    private async Task<WorkCalendarModel> Create(string code, List<WorkCalendarSlotRequest>? slots = null)
        => (await CreateRaw(code, slots)).Ok;

    private async Task<List<WorkCalendarModel>> All(bool inactive = true)
        => (await Api.GetAsync<List<WorkCalendarModel>>($"/api/calendars?includeInactive={inactive.ToString().ToLowerInvariant()}")).Ok;

    private Task<ApiResult<CalendarExceptionModel>> AddException(string scope, int id, DateOnly from, DateOnly to, CalendarExceptionKind kind,
        string reason = "Motivo", bool recurring = false, params (TimeOnly, TimeOnly)[] slots)
        => Api.PostAsync<CalendarExceptionModel>($"/api/{scope}/{id}/exceptions", new SaveCalendarExceptionRequest
        {
            FromDate = from, ToDate = to, Kind = kind, Reason = reason, IsRecurringYearly = recurring,
            Slots = slots.Select(s => new TimeSlotRequest { StartTime = s.Item1, EndTime = s.Item2 }).ToList()
        });

    // ---- default calendar -------------------------------------------------------------------------------------

    [Fact]
    public async Task GetCalendars_AfterMigration_Success_StandardIsTheOnlyDefaultWithWeeklyHours()
    {
        var list = await All();
        var standard = list.Should().ContainSingle(c => c.IsDefault).Subject;
        standard.Code.Should().Be("STANDARD");
        standard.IsActive.Should().BeTrue();
        standard.WeeklyHours.Should().Be(40);
        standard.Slots.Should().HaveCount(10);
    }

    // ---- CRUD ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateCalendar_ValidRequest_Success_201NormalizedCodeAndHours()
    {
        var r = await CreateRaw(" turno-a ", [Slot(DayOfWeek.Monday, 6, 14), Slot(DayOfWeek.Monday, 14, 22)]);

        r.Status.Should().Be(HttpStatusCode.Created);
        r.Ok.Code.Should().Be("TURNO-A");
        r.Ok.IsDefault.Should().BeFalse();
        r.Ok.WeeklyHours.Should().Be(16);
        r.Ok.Slots.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("TURNO-A")]
    [InlineData("turno-a")]
    [InlineData("standard")]
    public async Task CreateCalendar_DuplicateCode_Failure_409WithSpecificMessage(string code)
    {
        await Create("TURNO-A");

        var r = await CreateRaw(code);

        r.Status.Should().Be(code == "standard" || code == "TURNO-A" || code == "turno-a" ? HttpStatusCode.Conflict : HttpStatusCode.Created);
        r.Raw.Should().Contain("Esiste già un calendario con questo codice");
    }

    [Fact]
    public async Task CreateCalendar_Invalid_Failure_422()
    {
        (await CreateRaw("A", [])).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CreateRaw("B", [Slot(DayOfWeek.Monday, 12, 8)])).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CreateRaw("C", [Slot(DayOfWeek.Monday, 8, 12), Slot(DayOfWeek.Monday, 11, 15)])).Raw.Should().Contain("si sovrappongono");
        var midnight = (await CreateRaw("D", [new WorkCalendarSlotRequest { DayOfWeek = DayOfWeek.Monday, StartTime = T(22), EndTime = T(0) }]));
        midnight.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        midnight.Raw.Should().Contain("mezzanotte");
        (await Api.PostAsync<WorkCalendarModel>("/api/calendars", new { code = "", name = "x", slots = Array.Empty<object>() }))
            .Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await All()).Should().ContainSingle("nothing was saved by the failed requests");
    }

    [Fact]
    public async Task UpdateCalendar_ReplacesSlotsAndFields_Success()
    {
        var c = await Create("X");

        var r = await Api.PutAsync<WorkCalendarModel>($"/api/calendars/{c.Id}", new UpdateWorkCalendarRequest
        {
            Code = "x2", Name = "Nuovo", Description = "d", IsActive = true, Slots = [Slot(DayOfWeek.Saturday, 8, 12)]
        });

        r.Ok.Code.Should().Be("X2");
        r.Ok.Slots.Should().ContainSingle().Which.DayOfWeek.Should().Be(DayOfWeek.Saturday);
        r.Ok.WeeklyHours.Should().Be(4);
        (await Api.GetAsync<WorkCalendarModel>($"/api/calendars/{c.Id}")).Ok.Slots.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateCalendar_Errors_Failure_404_409_422()
    {
        var a = await Create("A");
        var b = await Create("B");
        var request = (string code, bool active, List<WorkCalendarSlotRequest> slots)
            => new UpdateWorkCalendarRequest { Code = code, Name = "n", IsActive = active, Slots = slots };

        (await Api.PutAsync<WorkCalendarModel>("/api/calendars/9999", request("A", true, MonFri()))).Status.Should().Be(HttpStatusCode.NotFound);
        (await Api.PutAsync<WorkCalendarModel>($"/api/calendars/{b.Id}", request("a", true, MonFri()))).Status.Should().Be(HttpStatusCode.Conflict);
        (await Api.PutAsync<WorkCalendarModel>($"/api/calendars/{a.Id}", request("A", true, []))).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        // An inactive calendar may have no slots (nothing to run), an active one may not.
        (await Api.PutAsync<WorkCalendarModel>($"/api/calendars/{a.Id}", request("A", false, []))).Status.Should().Be(HttpStatusCode.OK);
        (await Api.GetAsync<WorkCalendarModel>("/api/calendars/9999")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DuplicateCalendar_Success_CopiesSlotsNotDefaultNorExceptions()
    {
        var c = await Create("ORIG", [Slot(DayOfWeek.Monday, 7, 15)]);
        await AddException("calendars", c.Id, D(2026, 8, 17), D(2026, 8, 28), CalendarExceptionKind.Closed);

        var r = await Api.PostAsync<WorkCalendarModel>($"/api/calendars/{c.Id}/duplicate", new DuplicateWorkCalendarRequest { Code = "copia", Name = "Copia" });

        r.Status.Should().Be(HttpStatusCode.Created);
        r.Ok.Code.Should().Be("COPIA");
        r.Ok.IsDefault.Should().BeFalse();
        r.Ok.Slots.Should().ContainSingle(s => s.StartTime == T(7) && s.EndTime == T(15));
        (await Api.GetAsync<List<CalendarExceptionModel>>($"/api/calendars/{r.Ok.Id}/exceptions")).Ok.Should().BeEmpty();
        (await Api.PostAsync<WorkCalendarModel>($"/api/calendars/{c.Id}/duplicate", new DuplicateWorkCalendarRequest { Code = "COPIA", Name = "x" }))
            .Status.Should().Be(HttpStatusCode.Conflict);
        (await Api.PostAsync<WorkCalendarModel>("/api/calendars/9999/duplicate", new DuplicateWorkCalendarRequest { Code = "ZZ", Name = "x" }))
            .Status.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- default rules ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task SetDefaultCalendar_Success_ExactlyOneDefaultAtAnyTime()
    {
        var c = await Create("NUOVO");

        var r = await Api.PostAsync<WorkCalendarModel>($"/api/calendars/{c.Id}/set-default");

        r.Ok.IsDefault.Should().BeTrue();
        (await All()).Where(x => x.IsDefault).Select(x => x.Code).Should().Equal("NUOVO");
        // Idempotent.
        (await Api.PostAsync<WorkCalendarModel>($"/api/calendars/{c.Id}/set-default")).Ok.IsDefault.Should().BeTrue();
        (await All()).Count(x => x.IsDefault).Should().Be(1);
        (await Api.PostAsync<WorkCalendarModel>("/api/calendars/9999/set-default")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetDefaultCalendar_InactiveCalendar_Failure_409AndDefaultUnchanged()
    {
        var c = await Create("OLD");
        (await Api.DeleteAsync($"/api/calendars/{c.Id}")).Status.Should().Be(HttpStatusCode.NoContent);

        var r = await Api.PostAsync<WorkCalendarModel>($"/api/calendars/{c.Id}/set-default");

        r.Status.Should().Be(HttpStatusCode.Conflict);
        (await All()).Single(x => x.IsDefault).Code.Should().Be("STANDARD");
    }

    [Fact]
    public async Task DeactivateCalendar_Default_Failure_409_BothDeleteAndUpdate()
    {
        var standard = (await All()).Single(x => x.IsDefault);

        var delete = await Api.DeleteAsync($"/api/calendars/{standard.Id}");
        var update = await Api.PutAsync<WorkCalendarModel>($"/api/calendars/{standard.Id}", new UpdateWorkCalendarRequest
        {
            Code = standard.Code, Name = standard.Name, IsActive = false,
            Slots = standard.Slots.Select(s => Slot(s.DayOfWeek, s.StartTime.Hour, s.EndTime.Hour)).ToList()
        });

        delete.Status.Should().Be(HttpStatusCode.Conflict);
        delete.Raw.Should().Contain("predefinito");
        update.Status.Should().Be(HttpStatusCode.Conflict);
        (await All(false)).Single(x => x.IsDefault).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateCalendar_UsedByActiveMachine_Failure_409UntilTheMachineMoves()
    {
        var c = await Create("MACCH");
        var machine = (await Api.PostAsync<EquipmentModel>("/api/equipment",
            new CreateEquipmentRequest { Code = "M1", Name = "M1", WorkCalendarId = c.Id })).Ok;

        var blocked = await Api.DeleteAsync($"/api/calendars/{c.Id}");

        blocked.Status.Should().Be(HttpStatusCode.Conflict);
        blocked.Raw.Should().Contain("M1");

        (await Api.DeleteAsync($"/api/equipment/{machine.Id}")).Status.Should().Be(HttpStatusCode.NoContent);
        (await Api.DeleteAsync($"/api/calendars/{c.Id}")).Status.Should().Be(HttpStatusCode.NoContent);
        (await Api.DeleteAsync($"/api/calendars/{c.Id}")).Status.Should().Be(HttpStatusCode.NoContent, "idempotent");
        (await All(false)).Should().NotContain(x => x.Id == c.Id);
        (await All()).Should().Contain(x => x.Id == c.Id && !x.IsActive);
        (await Api.DeleteAsync("/api/calendars/9999")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- exceptions ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task CalendarExceptions_ClosedCustomAndRecurring_Success_201AndList()
    {
        var c = await Create("EXC");

        var closed = await AddException("calendars", c.Id, D(2026, 8, 17), D(2026, 8, 28), CalendarExceptionKind.Closed, " Ferie estive ");
        var custom = await AddException("calendars", c.Id, D(2026, 12, 24), D(2026, 12, 24), CalendarExceptionKind.Custom, "Vigilia", false, (T(8), T(12)));
        var recurring = await AddException("calendars", c.Id, D(2026, 5, 1), D(2026, 5, 1), CalendarExceptionKind.Closed, "1 maggio", true);
        var leap = await AddException("calendars", c.Id, D(2028, 2, 29), D(2028, 2, 29), CalendarExceptionKind.Closed, "Bisestile", true);

        closed.Status.Should().Be(HttpStatusCode.Created);
        closed.Ok.Reason.Should().Be("Ferie estive");
        closed.Ok.Slots.Should().BeEmpty();
        closed.Ok.Overlaps.Should().BeEmpty();
        closed.Ok.OwnerId.Should().Be(c.Id);
        custom.Ok.Slots.Should().ContainSingle(s => s.StartTime == T(8) && s.EndTime == T(12));
        recurring.Ok.IsRecurringYearly.Should().BeTrue();
        leap.Status.Should().Be(HttpStatusCode.Created);

        var list = (await Api.GetAsync<List<CalendarExceptionModel>>($"/api/calendars/{c.Id}/exceptions")).Ok;
        list.Select(e => e.FromDate).Should().BeInAscendingOrder().And.HaveCount(4);
        (await Api.GetAsync<List<CalendarExceptionModel>>($"/api/calendars/{c.Id}/exceptions?year=2026")).Ok
            .Select(e => e.Reason).Should().BeEquivalentTo("Ferie estive", "Vigilia", "1 maggio");
        (await Api.GetAsync<List<CalendarExceptionModel>>($"/api/calendars/{c.Id}/exceptions?year=2027")).Ok
            .Select(e => e.Reason).Should().Equal("1 maggio");
    }

    [Fact]
    public async Task CalendarExceptions_Invalid_Failure_422AndUnknownOwner404()
    {
        var c = await Create("EXC");

        (await AddException("calendars", c.Id, D(2026, 8, 20), D(2026, 8, 10), CalendarExceptionKind.Closed)).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await AddException("calendars", c.Id, D(2026, 5, 1), D(2026, 5, 3), CalendarExceptionKind.Closed, "x", true)).Raw.Should().Contain("un solo giorno");
        (await AddException("calendars", c.Id, D(2026, 5, 1), D(2026, 5, 1), CalendarExceptionKind.Custom)).Raw.Should().Contain("almeno una fascia");
        (await AddException("calendars", c.Id, D(2026, 5, 1), D(2026, 5, 1), CalendarExceptionKind.Custom, "x", false, (T(8), T(12)), (T(11), T(13))))
            .Raw.Should().Contain("si sovrappongono");
        (await AddException("calendars", c.Id, D(2026, 5, 1), D(2026, 5, 1), CalendarExceptionKind.Closed, "")).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await AddException("calendars", 9999, D(2026, 5, 1), D(2026, 5, 1), CalendarExceptionKind.Closed)).Status.Should().Be(HttpStatusCode.NotFound);
        (await Api.GetAsync<List<CalendarExceptionModel>>($"/api/calendars/{c.Id}/exceptions")).Ok.Should().BeEmpty();
    }

    [Fact]
    public async Task CalendarExceptions_Overlap_Success_SavedWithWarningInOverlaps()
    {
        var c = await Create("EXC");
        var first = (await AddException("calendars", c.Id, D(2026, 8, 10), D(2026, 8, 20), CalendarExceptionKind.Closed, "Ferie")).Ok;

        var second = await AddException("calendars", c.Id, D(2026, 8, 18), D(2026, 8, 25), CalendarExceptionKind.Closed, "Chiusura");
        var apart = await AddException("calendars", c.Id, D(2026, 9, 1), D(2026, 9, 2), CalendarExceptionKind.Closed, "Altro");

        second.Status.Should().Be(HttpStatusCode.Created, "overlaps are warnings, not errors");
        second.Ok.Overlaps.Should().ContainSingle().Which.Should().Match<ExceptionOverlapModel>(o => o.Id == first.Id && o.Reason == "Ferie");
        apart.Ok.Overlaps.Should().BeEmpty();
        (await Api.GetAsync<List<CalendarExceptionModel>>($"/api/calendars/{c.Id}/exceptions")).Ok.Should().HaveCount(3);

        // A fixed holiday on Ferragosto overlaps the closure above.
        var holiday = await AddException("calendars", c.Id, D(2026, 8, 15), D(2026, 8, 15), CalendarExceptionKind.Closed, "Ferragosto", true);
        holiday.Ok.Overlaps.Should().ContainSingle().Which.Reason.Should().Be("Ferie");
    }

    [Fact]
    public async Task CalendarExceptions_UpdateAndDelete_Success_ThenNotFound()
    {
        var c = await Create("EXC");
        var e = (await AddException("calendars", c.Id, D(2026, 8, 10), D(2026, 8, 12), CalendarExceptionKind.Closed, "Ferie")).Ok;

        var updated = await Api.PutAsync<CalendarExceptionModel>($"/api/calendars/{c.Id}/exceptions/{e.Id}", new SaveCalendarExceptionRequest
        {
            FromDate = D(2026, 8, 10), ToDate = D(2026, 8, 10), Kind = CalendarExceptionKind.Custom, Reason = "Mezza giornata",
            Slots = [new TimeSlotRequest { StartTime = T(8), EndTime = T(12) }]
        });

        updated.Ok.Kind.Should().Be(CalendarExceptionKind.Custom);
        updated.Ok.Slots.Should().ContainSingle();
        updated.Ok.Overlaps.Should().BeEmpty("it never overlaps itself");
        (await Api.DeleteAsync($"/api/calendars/{c.Id}/exceptions/{e.Id}")).Status.Should().Be(HttpStatusCode.NoContent);
        (await Api.DeleteAsync($"/api/calendars/{c.Id}/exceptions/{e.Id}")).Status.Should().Be(HttpStatusCode.NotFound);
        (await Api.PutAsync<CalendarExceptionModel>($"/api/calendars/{c.Id}/exceptions/{e.Id}", new SaveCalendarExceptionRequest
        {
            FromDate = D(2026, 8, 10), ToDate = D(2026, 8, 10), Kind = CalendarExceptionKind.Closed, Reason = "x"
        })).Status.Should().Be(HttpStatusCode.NotFound);
        Fixture.WithDbAsync(db => Task.FromResult(db.CalendarExceptionSlots.Count())).Result.Should().Be(0, "slots of a deleted exception go away");
    }

    [Fact]
    public async Task EquipmentExceptions_CrudAndOverlap_Success_SeparateFromCalendarOnes()
    {
        var m = (await Api.PostAsync<EquipmentModel>("/api/equipment", new CreateEquipmentRequest { Code = "M1", Name = "M1" })).Ok;

        var maintenance = await AddException("equipment", m.Id, D(2026, 3, 12), D(2026, 3, 14), CalendarExceptionKind.Closed, "Manutenzione");
        var breakdown = await AddException("equipment", m.Id, D(2026, 3, 14), D(2026, 3, 15), CalendarExceptionKind.Closed, "Guasto");

        maintenance.Status.Should().Be(HttpStatusCode.Created);
        maintenance.Ok.OwnerId.Should().Be(m.Id);
        breakdown.Ok.Overlaps.Should().ContainSingle().Which.Reason.Should().Be("Manutenzione");
        (await Api.GetAsync<List<CalendarExceptionModel>>($"/api/equipment/{m.Id}/exceptions?year=2026")).Ok.Should().HaveCount(2);
        (await Api.GetAsync<List<CalendarExceptionModel>>($"/api/calendars/{(await All()).Single(c => c.IsDefault).Id}/exceptions")).Ok
            .Should().BeEmpty("machine exceptions never leak into the calendar");
        (await AddException("equipment", 9999, D(2026, 3, 12), D(2026, 3, 14), CalendarExceptionKind.Closed)).Status.Should().Be(HttpStatusCode.NotFound);
        (await AddException("equipment", m.Id, D(2026, 3, 20), D(2026, 3, 20), CalendarExceptionKind.Custom, "x", false, (T(9), T(10))))
            .Ok.Slots.Should().ContainSingle();
        (await Api.DeleteAsync($"/api/equipment/{m.Id}/exceptions/{maintenance.Ok.Id}")).Status.Should().Be(HttpStatusCode.NoContent);
        (await Api.DeleteAsync($"/api/equipment/{m.Id}/exceptions/{maintenance.Ok.Id}")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- Italian holidays --------------------------------------------------------------------------------------------

    [Fact]
    public async Task ItalianHolidays_PreviewConfirmAndRepeat_Success_NoDuplicates()
    {
        var c = await Create("FEST");

        var preview = (await Api.GetAsync<ItalianHolidaysPreviewModel>($"/api/calendars/{c.Id}/holidays/italy?year=2026")).Ok;
        preview.Holidays.Should().HaveCount(12).And.OnlyContain(h => !h.AlreadyPresent);
        preview.Holidays.Should().Contain(h => h.Date == D(2026, 4, 5) && h.Name == "Pasqua");
        preview.Holidays.Should().Contain(h => h.Date == D(2026, 4, 6));
        (await Api.GetAsync<List<CalendarExceptionModel>>($"/api/calendars/{c.Id}/exceptions")).Ok.Should().BeEmpty("preview saves nothing");

        // Confirm with two holidays deselected.
        var chosen = preview.Holidays.Select(h => h.Date).Where(d => d != D(2026, 1, 6) && d != D(2026, 11, 1)).ToList();
        var import = (await Api.PostAsync<ImportItalianHolidaysResultModel>($"/api/calendars/{c.Id}/holidays/italy",
            new ImportItalianHolidaysRequest { Year = 2026, Dates = chosen })).Ok;
        import.Created.Should().HaveCount(10);
        import.Skipped.Should().Be(0);
        import.Created.Should().OnlyContain(e => e.Kind == CalendarExceptionKind.Closed && !e.IsRecurringYearly && e.FromDate == e.ToDate);

        var again = (await Api.GetAsync<ItalianHolidaysPreviewModel>($"/api/calendars/{c.Id}/holidays/italy?year=2026")).Ok;
        again.Holidays.Count(h => h.AlreadyPresent).Should().Be(10);

        // Importing everything again: 200, only the two missing ones are created, the others are skipped.
        var rest = (await Api.PostAsync<ImportItalianHolidaysResultModel>($"/api/calendars/{c.Id}/holidays/italy",
            new ImportItalianHolidaysRequest { Year = 2026 })).Ok;
        rest.Created.Should().HaveCount(2);
        rest.Skipped.Should().Be(10);
        (await Api.PostAsync<ImportItalianHolidaysResultModel>($"/api/calendars/{c.Id}/holidays/italy",
            new ImportItalianHolidaysRequest { Year = 2026 })).Ok.Created.Should().BeEmpty();
        (await Api.GetAsync<List<CalendarExceptionModel>>($"/api/calendars/{c.Id}/exceptions")).Ok.Should().HaveCount(12);
    }

    [Fact]
    public async Task ItalianHolidays_Invalid_Failure_404And422()
    {
        var c = await Create("FEST");

        (await Api.GetAsync<ItalianHolidaysPreviewModel>($"/api/calendars/{c.Id}/holidays/italy?year=1800")).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.GetAsync<ItalianHolidaysPreviewModel>("/api/calendars/9999/holidays/italy?year=2026")).Status.Should().Be(HttpStatusCode.NotFound);
        (await Api.PostAsync<ImportItalianHolidaysResultModel>($"/api/calendars/{c.Id}/holidays/italy",
            new ImportItalianHolidaysRequest { Year = 2026, Dates = [D(2026, 3, 3)] })).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.PostAsync<ImportItalianHolidaysResultModel>($"/api/calendars/{c.Id}/holidays/italy",
            new ImportItalianHolidaysRequest { Year = 3000 })).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.PostAsync<ImportItalianHolidaysResultModel>("/api/calendars/9999/holidays/italy",
            new ImportItalianHolidaysRequest { Year = 2026 })).Status.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- preview and days -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task PreviewCalendar_Success_100MinutesFromMonday8EndAt940()
    {
        var standard = (await All()).Single(c => c.IsDefault);

        var p = (await Api.GetAsync<CalendarPreviewModel>($"/api/calendars/{standard.Id}/preview?start=2026-01-05T07:00:00Z&minutes=100")).Ok;

        p.TimeZoneId.Should().Be("Europe/Rome");
        p.Start.Should().Be(new DateTime(2026, 1, 5, 7, 0, 0, DateTimeKind.Utc));
        p.End.Should().Be(new DateTime(2026, 1, 5, 8, 40, 0, DateTimeKind.Utc));
        p.FirstWorkingInstant.Should().Be(p.Start);
        p.WorkingMinutes.Should().Be(100);
        p.EfficiencyPercent.Should().Be(100);
    }

    [Fact]
    public async Task PreviewCalendar_StartInPauseAndWithClosure_Success_UsesExceptions()
    {
        var c = await Create("PREV");
        await AddException("calendars", c.Id, D(2026, 1, 6), D(2026, 1, 6), CalendarExceptionKind.Closed);

        // Monday 16:00 local (15:00Z) + 120 min: 60 Monday, Tuesday closed, 60 on Wednesday from 08:00 -> 09:00 local = 08:00Z.
        var p = (await Api.GetAsync<CalendarPreviewModel>($"/api/calendars/{c.Id}/preview?start=2026-01-05T15:00:00Z&minutes=120")).Ok;
        p.End.Should().Be(new DateTime(2026, 1, 7, 8, 0, 0, DateTimeKind.Utc));

        // Saturday: first working instant is Monday 08:00 local.
        var weekend = (await Api.GetAsync<CalendarPreviewModel>($"/api/calendars/{c.Id}/preview?start=2026-01-10T10:00:00Z&minutes=30")).Ok;
        weekend.FirstWorkingInstant.Should().Be(new DateTime(2026, 1, 12, 7, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task PreviewCalendar_Errors_Failure_404And422()
    {
        var standard = (await All()).Single(c => c.IsDefault);
        (await Api.GetAsync<CalendarPreviewModel>("/api/calendars/9999/preview?start=2026-01-05T07:00:00Z&minutes=1")).Status.Should().Be(HttpStatusCode.NotFound);
        (await Api.GetAsync<CalendarPreviewModel>($"/api/calendars/{standard.Id}/preview?start=2026-01-05T07:00:00Z&minutes=-5")).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.GetAsync<CalendarPreviewModel>("/api/equipment/9999/calendar/preview?start=2026-01-05T07:00:00Z&minutes=1")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PreviewCalendar_NoWorkingTimeInHorizon_Failure_422WithExplicitMessage()
    {
        var c = await Create("CHIUSO");
        await AddException("calendars", c.Id, D(2026, 1, 1), D(2035, 12, 31), CalendarExceptionKind.Closed, "Chiuso 10 anni");

        var r = await Api.GetAsync<CalendarPreviewModel>($"/api/calendars/{c.Id}/preview?start=2026-01-05T07:00:00Z&minutes=10");

        r.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        r.Raw.Should().Contain("non ha ore lavorabili");
    }

    [Fact]
    public async Task PreviewEquipment_EfficiencyCalendarAndMachineException_Success()
    {
        var m = (await Api.PostAsync<EquipmentModel>("/api/equipment",
            new CreateEquipmentRequest { Code = "LENTA", Name = "Lenta", EfficiencyPercent = 50m })).Ok;

        // 60 standard minutes at 50% = 120 real minutes: Monday 08:00 local -> 10:00 local (09:00Z).
        var p = (await Api.GetAsync<CalendarPreviewModel>($"/api/equipment/{m.Id}/calendar/preview?start=2026-01-05T07:00:00Z&minutes=60")).Ok;
        p.EquipmentId.Should().Be(m.Id);
        p.EfficiencyPercent.Should().Be(50);
        p.StandardMinutes.Should().Be(60);
        p.WorkingMinutes.Should().Be(120);
        p.End.Should().Be(new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc));

        await AddException("equipment", m.Id, D(2026, 1, 5), D(2026, 1, 5), CalendarExceptionKind.Closed, "Guasto");
        var after = (await Api.GetAsync<CalendarPreviewModel>($"/api/equipment/{m.Id}/calendar/preview?start=2026-01-05T07:00:00Z&minutes=60")).Ok;
        after.FirstWorkingInstant.Should().Be(new DateTime(2026, 1, 6, 7, 0, 0, DateTimeKind.Utc));
        after.End.Should().Be(new DateTime(2026, 1, 6, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetCalendarDays_MonthView_Success_StatusReasonAndSlots()
    {
        var c = await Create("DAYS");
        await AddException("calendars", c.Id, D(2026, 1, 6), D(2026, 1, 6), CalendarExceptionKind.Closed, "Epifania");
        await AddException("calendars", c.Id, D(2026, 1, 10), D(2026, 1, 10), CalendarExceptionKind.Custom, "Straordinario", false, (T(8), T(12)));

        var days = (await Api.GetAsync<List<CalendarDayModel>>($"/api/calendars/{c.Id}/days?from=2026-01-05&to=2026-01-11")).Ok;

        days.Should().HaveCount(7);
        days[0].Status.Should().Be(CalendarDayStatus.Working);
        days[0].Slots.Should().ContainSingle(s => s.StartTime == T(8) && s.EndTime == T(17));
        days[1].Status.Should().Be(CalendarDayStatus.Closed);
        days[1].Reason.Should().Be("Epifania");
        days[1].ExceptionScope.Should().Be(ExceptionScope.Calendar);
        days[5].Status.Should().Be(CalendarDayStatus.Custom);
        days[6].Status.Should().Be(CalendarDayStatus.NonWorking);
        days[6].Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEquipmentCalendarDays_Success_MachineExceptionDistinctFromCompanyOne()
    {
        var standard = (await All()).Single(c => c.IsDefault);
        await AddException("calendars", standard.Id, D(2026, 1, 6), D(2026, 1, 6), CalendarExceptionKind.Closed, "Epifania");
        var m = (await Api.PostAsync<EquipmentModel>("/api/equipment", new CreateEquipmentRequest { Code = "M1", Name = "M1" })).Ok;
        await AddException("equipment", m.Id, D(2026, 1, 7), D(2026, 1, 7), CalendarExceptionKind.Closed, "Manutenzione");

        var days = (await Api.GetAsync<List<CalendarDayModel>>($"/api/equipment/{m.Id}/calendar/days?from=2026-01-05&to=2026-01-08")).Ok;

        days[1].ExceptionScope.Should().Be(ExceptionScope.Calendar);
        days[2].ExceptionScope.Should().Be(ExceptionScope.Equipment);
        days[2].Reason.Should().Be("Manutenzione");
        (await Api.GetAsync<List<CalendarDayModel>>($"/api/calendars/{standard.Id}/days?from=2026-01-05&to=2026-01-08")).Ok[2].Status
            .Should().Be(CalendarDayStatus.Working, "the calendar alone does not know the machine stop");
    }

    [Fact]
    public async Task GetCalendarDays_Errors_Failure_404And422()
    {
        var standard = (await All()).Single(c => c.IsDefault);
        (await Api.GetAsync<List<CalendarDayModel>>($"/api/calendars/{standard.Id}/days?from=2026-01-10&to=2026-01-05")).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.GetAsync<List<CalendarDayModel>>($"/api/calendars/{standard.Id}/days?from=2026-01-01&to=2027-12-31")).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.GetAsync<List<CalendarDayModel>>("/api/calendars/9999/days?from=2026-01-05&to=2026-01-06")).Status.Should().Be(HttpStatusCode.NotFound);
        (await Api.GetAsync<List<CalendarDayModel>>("/api/equipment/9999/calendar/days?from=2026-01-05&to=2026-01-06")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- time zone ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task CompanyTimeZone_DefaultSetAndInvalid_Success()
    {
        (await Api.GetAsync<CompanyTimeZoneModel>("/api/calendars/time-zone")).Ok.TimeZoneId.Should().Be("Europe/Rome");
        (await Api.PutAsync<CompanyTimeZoneModel>("/api/calendars/time-zone", new UpdateCompanyTimeZoneRequest { TimeZoneId = "Nowhere/City" }))
            .Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.PutAsync<CompanyTimeZoneModel>("/api/calendars/time-zone", new UpdateCompanyTimeZoneRequest { TimeZoneId = "UTC" })).Ok.TimeZoneId.Should().Be("UTC");
        (await Api.PutAsync<CompanyTimeZoneModel>("/api/calendars/time-zone", new UpdateCompanyTimeZoneRequest { TimeZoneId = "Europe/Rome" })).Ok.TimeZoneId.Should().Be("Europe/Rome");

        // With UTC as company zone a 08:00Z start is working time of the 08-17 calendar.
        await Api.PutAsync<CompanyTimeZoneModel>("/api/calendars/time-zone", new UpdateCompanyTimeZoneRequest { TimeZoneId = "UTC" });
        var standard = (await All()).Single(c => c.IsDefault);
        (await Api.GetAsync<CalendarPreviewModel>($"/api/calendars/{standard.Id}/preview?start=2026-01-05T08:00:00Z&minutes=60")).Ok.End
            .Should().Be(new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc));
    }

    // ---- equipment fields -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateEquipment_Defaults_Success_Capacity1Efficiency100DefaultCalendarInherited()
    {
        var m = (await Api.PostAsync<EquipmentModel>("/api/equipment", new CreateEquipmentRequest { Code = "M1", Name = "M1" })).Ok;

        m.WorkCalendarId.Should().BeNull();
        m.Capacity.Should().Be(1);
        m.EfficiencyPercent.Should().Be(100);
        m.EffectiveCalendar.Should().NotBeNull();
        m.EffectiveCalendar!.Code.Should().Be("STANDARD");
        m.EffectiveCalendar.IsInherited.Should().BeTrue();
        m.EffectiveCalendar.WeeklyHours.Should().Be(40);
    }

    [Fact]
    public async Task CreateAndUpdateEquipment_CalendarCapacityEfficiency_Success_NullMeansUnchanged()
    {
        var c = await Create("DOPPIO", MonFri(6, 22));
        var m = (await Api.PostAsync<EquipmentModel>("/api/equipment",
            new CreateEquipmentRequest { Code = "M1", Name = "M1", WorkCalendarId = c.Id, Capacity = 3, EfficiencyPercent = 85.5m })).Ok;

        m.WorkCalendarId.Should().Be(c.Id);
        m.Capacity.Should().Be(3);
        m.EfficiencyPercent.Should().Be(85.5m);
        m.EffectiveCalendar!.Code.Should().Be("DOPPIO");
        m.EffectiveCalendar.IsInherited.Should().BeFalse();

        // A client that does not know the new fields keeps capacity and efficiency; the calendar is replaced (null = default).
        var u = (await Api.PutAsync<EquipmentModel>($"/api/equipment/{m.Id}",
            new UpdateEquipmentRequest { Code = "M1", Name = "M1 bis", IsActive = true })).Ok;
        u.Capacity.Should().Be(3);
        u.EfficiencyPercent.Should().Be(85.5m);
        u.WorkCalendarId.Should().BeNull();
        u.EffectiveCalendar!.Code.Should().Be("STANDARD");

        var v = (await Api.PutAsync<EquipmentModel>($"/api/equipment/{m.Id}",
            new UpdateEquipmentRequest { Code = "M1", Name = "M1", IsActive = true, Capacity = 2, EfficiencyPercent = 120m, WorkCalendarId = c.Id })).Ok;
        (v.Capacity, v.EfficiencyPercent, v.WorkCalendarId).Should().Be((2, 120m, c.Id));
        (await Api.GetAsync<List<EquipmentModel>>("/api/equipment")).Ok.Single().EffectiveCalendar!.Code.Should().Be("DOPPIO");
    }

    [Fact]
    public async Task EquipmentFields_Invalid_Failure_422()
    {
        var inactive = await Create("OLD");
        await Api.DeleteAsync($"/api/calendars/{inactive.Id}");
        var m = (await Api.PostAsync<EquipmentModel>("/api/equipment", new CreateEquipmentRequest { Code = "M1", Name = "M1" })).Ok;

        (await Api.PostAsync<EquipmentModel>("/api/equipment", new CreateEquipmentRequest { Code = "M2", Name = "M2", WorkCalendarId = 9999 }))
            .Raw.Should().Contain("non esiste");
        (await Api.PostAsync<EquipmentModel>("/api/equipment", new CreateEquipmentRequest { Code = "M2", Name = "M2", WorkCalendarId = inactive.Id }))
            .Raw.Should().Contain("disattivato");
        (await Api.PostAsync<EquipmentModel>("/api/equipment", new CreateEquipmentRequest { Code = "M2", Name = "M2", Capacity = 0 }))
            .Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.PostAsync<EquipmentModel>("/api/equipment", new CreateEquipmentRequest { Code = "M2", Name = "M2", EfficiencyPercent = 0m }))
            .Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.PostAsync<EquipmentModel>("/api/equipment", new CreateEquipmentRequest { Code = "M2", Name = "M2", EfficiencyPercent = -10m }))
            .Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.PutAsync<EquipmentModel>($"/api/equipment/{m.Id}",
            new UpdateEquipmentRequest { Code = "M1", Name = "M1", IsActive = true, WorkCalendarId = inactive.Id })).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.PutAsync<EquipmentModel>($"/api/equipment/{m.Id}",
            new UpdateEquipmentRequest { Code = "M1", Name = "M1", IsActive = true, Capacity = 0 })).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.GetAsync<List<EquipmentModel>>("/api/equipment?includeInactive=true")).Ok.Should().ContainSingle();
    }

    [Fact]
    public async Task GetEquipment_UpcomingClosures_Success_CompanyAndMachineOnesInOrder()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var standard = (await All()).Single(c => c.IsDefault);
        await AddException("calendars", standard.Id, today.AddDays(20), today.AddDays(22), CalendarExceptionKind.Closed, "Ponte");
        await AddException("calendars", standard.Id, today.AddDays(-30), today.AddDays(-25), CalendarExceptionKind.Closed, "Passata");
        await AddException("calendars", standard.Id, today.AddDays(3), today.AddDays(3), CalendarExceptionKind.Custom, "Mezza giornata", false, (T(8), T(12)));
        var m = (await Api.PostAsync<EquipmentModel>("/api/equipment", new CreateEquipmentRequest { Code = "M1", Name = "M1" })).Ok;
        await AddException("equipment", m.Id, today.AddDays(10), today.AddDays(11), CalendarExceptionKind.Closed, "Manutenzione");

        var effective = (await Api.GetAsync<EquipmentModel>($"/api/equipment/{m.Id}")).Ok.EffectiveCalendar!;

        effective.UpcomingClosures.Select(u => u.Reason).Should().Equal("Manutenzione", "Ponte");
        effective.UpcomingClosures[0].Scope.Should().Be(ExceptionScope.Equipment);
        effective.UpcomingClosures[1].Scope.Should().Be(ExceptionScope.Calendar);
        effective.UpcomingClosures[0].FromDate.Should().Be(today.AddDays(10));
        effective.UpcomingClosures[0].ToDate.Should().Be(today.AddDays(11));
    }

    [Fact]
    public async Task GetEquipment_RecurringHoliday_Success_ShowsNextOccurrence()
    {
        var standard = (await All()).Single(c => c.IsDefault);
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        await AddException("calendars", standard.Id, new DateOnly(2020, tomorrow.Month, tomorrow.Day), new DateOnly(2020, tomorrow.Month, tomorrow.Day),
            CalendarExceptionKind.Closed, "Fissa", true);
        var m = (await Api.PostAsync<EquipmentModel>("/api/equipment", new CreateEquipmentRequest { Code = "M1", Name = "M1" })).Ok;

        var closure = m.EffectiveCalendar!.UpcomingClosures;
        closure.Should().ContainSingle(c => c.Reason == "Fissa");
        closure.Single(c => c.Reason == "Fissa").FromDate.Should().Be(tomorrow);
    }

    [Fact]
    public async Task ReleasedWorkOrders_ChangingEfficiency_Success_PlannedMinutesUntouched()
    {
        var seg = (await Api.CreateSegmentAsync("SEG", setup: 10, run: 5)).Id;
        var m = (await Api.PostAsync<EquipmentModel>("/api/equipment",
            new CreateEquipmentRequest { Code = "M1", Name = "M1", ProcessSegmentIds = [seg] })).Ok;
        var material = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        await Api.PutLinearRoutingAsync(material.Id, seg);
        var order = await Api.CreateOrderAsync(material.Id, 4);
        await Api.ReleaseAsync(order.Id);
        var before = (await Api.GetOrderAsync(order.Id)).Ok.WorkOrders.Single();

        await Api.PutAsync<EquipmentModel>($"/api/equipment/{m.Id}",
            new UpdateEquipmentRequest { Code = "M1", Name = "M1", IsActive = true, ProcessSegmentIds = [seg], EfficiencyPercent = 50m, Capacity = 4 });

        var after = (await Api.GetOrderAsync(order.Id)).Ok.WorkOrders.Single();
        (after.PlannedMinutes, after.SetupMinutes, after.RunMinutesPerUnit).Should().Be((before.PlannedMinutes, before.SetupMinutes, before.RunMinutesPerUnit));
        after.PlannedMinutes.Should().Be(30);
    }
}
