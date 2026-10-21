using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Models;
using MicroMES.ApiFeatures.CalendarFeature.Seeding;
using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.SampleDataFeature;
using MicroMES.ApiTests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiTests.Integration.Features;

[Collection(ApiCollection.Name)]
public class CalendarSeedTests(ApiFixture fixture)
{
    [Fact]
    public async Task DevelopmentStartup_FreshDatabase_Success_SeedsCalendarsHolidaysAndMachineValuesIdempotently()
    {
        var connectionString = await fixture.CreateDatabaseAsync($"cal_{Guid.NewGuid():N}");
        await using var factory = new ApiFactory(connectionString, "Development");
        var api = new ApiClient(factory.CreateClient());
        var year = DateTime.UtcNow.Year;

        var calendars = (await api.GetAsync<List<WorkCalendarModel>>("/api/calendars?includeInactive=true")).Ok;
        calendars.Select(c => c.Code).Should().BeEquivalentTo("STANDARD", "DOPPIO-TURNO");
        var standard = calendars.Single(c => c.Code == "STANDARD");
        standard.IsDefault.Should().BeTrue();
        standard.WeeklyHours.Should().Be(40);
        var doppio = calendars.Single(c => c.Code == "DOPPIO-TURNO");
        doppio.IsDefault.Should().BeFalse();
        doppio.WeeklyHours.Should().Be(80);
        doppio.Slots.Should().OnlyContain(s => s.StartTime.Hour == 6 && s.EndTime.Hour == 14 || s.StartTime.Hour == 14 && s.EndTime.Hour == 22);

        var exceptions = (await api.GetAsync<List<CalendarExceptionModel>>($"/api/calendars/{standard.Id}/exceptions")).Ok;
        exceptions.Where(e => e.Reason != "Ferie estive").Should().HaveCount(24, "12 national holidays this year and next");
        exceptions.Should().Contain(e => e.Reason == "Pasqua" && e.FromDate.Year == year);
        exceptions.Should().Contain(e => e.Reason == "Pasqua" && e.FromDate.Year == year + 1);
        exceptions.Single(e => e.Reason == "Ferie estive").Should().Match<CalendarExceptionModel>(e => e.FromDate == new DateOnly(year, 8, 17) && e.ToDate == new DateOnly(year, 8, 28));
        exceptions.Should().OnlyContain(e => e.Kind == CalendarExceptionKind.Closed && e.Overlaps.Count == 0);
        (await api.GetAsync<List<CalendarExceptionModel>>($"/api/calendars/{doppio.Id}/exceptions")).Ok.Should().BeEmpty();

        var machines = (await api.GetAsync<List<EquipmentModel>>("/api/equipment?includeInactive=true")).Ok.ToDictionary(m => m.Code);
        machines.Should().HaveCount(9, "no machine is created by the calendar seeder");
        machines["TAG-TUB-2"].EfficiencyPercent.Should().Be(85m);
        machines["IMB-T1"].Capacity.Should().Be(2);
        machines["VER-POL-1"].WorkCalendarId.Should().Be(doppio.Id);
        machines["VER-POL-1"].EffectiveCalendar!.IsInherited.Should().BeFalse();
        machines.Values.Where(m => m.Code is not ("TAG-TUB-2" or "IMB-T1" or "VER-POL-1")).Should()
            .OnlyContain(m => m.Capacity == 1 && m.EfficiencyPercent == 100m && m.WorkCalendarId == null);
        var stop = (await api.GetAsync<List<CalendarExceptionModel>>($"/api/equipment/{machines["TAG-LAM-1"].Id}/exceptions")).Ok;
        stop.Should().ContainSingle().Which.Reason.Should().Be("Manutenzione programmata");

        var orders = (await api.GetOrdersAsync(includeChildren: true)).Ok;
        orders.Where(o => o.ParentOrderId is null).Should().HaveCount(8, "orders are unchanged");
        var open = (await api.GetOrderAsync(orders.First(o => o.Status == ProductionOrderStatus.InProgress).Id)).Ok;
        open.EstimatedEnd.Should().NotBeNull("an open seeded order has an estimated end");

        // Idempotence: the seeder (own scope, like at startup) adds nothing the second time.
        for (var i = 0; i < 2; i++)
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<CalendarDevelopmentSeeder>().SeedAsync();
        }
        (await api.GetAsync<List<WorkCalendarModel>>("/api/calendars?includeInactive=true")).Ok.Should().HaveCount(2);
        (await api.GetAsync<List<CalendarExceptionModel>>($"/api/calendars/{standard.Id}/exceptions")).Ok.Should().HaveCount(25);
        (await api.GetAsync<List<WorkCalendarModel>>("/api/calendars")).Ok.Count(c => c.IsDefault).Should().Be(1);
    }

    [Fact]
    public async Task Migration_DatabaseWithExistingDataButNoCalendars_Success_CreatesStandardAndSeederCompletesTheSampleData()
    {
        var connectionString = await fixture.CreateDatabaseAsync($"cal_{Guid.NewGuid():N}");
        var options = new DbContextOptionsBuilder<MicroMES.ApiCore.Persistence.AppDbContext>().UseNpgsql(connectionString).Options;
        // Database as it was before this feature: migrate to the previous migration and add a machine.
        await using (var db = new MicroMES.ApiCore.Persistence.AppDbContext(options))
        {
            var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
            await migrator.MigrateAsync("20261021143226_AddEquipmentAssignment");
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Equipment\" (\"Code\", \"Name\", \"IsActive\", \"CreatedAt\", \"UpdatedAt\") VALUES ('LEGACY-1', 'Macchina esistente', TRUE, now(), now())");
        }

        await using (var db = new MicroMES.ApiCore.Persistence.AppDbContext(options)) await db.Database.MigrateAsync();

        // "Testing" does not seed on its own: the calendar seeder runs explicitly, twice, in its own scopes.
        await using var factory = new ApiFactory(connectionString, "Testing");
        var api = new ApiClient(factory.CreateClient());
        (await api.GetAsync<List<WorkCalendarModel>>("/api/calendars?includeInactive=true")).Ok.Should().ContainSingle(c => c.Code == "STANDARD");
        for (var i = 0; i < 2; i++)
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<CalendarDevelopmentSeeder>().SeedAsync();
        }

        // The migration created the default calendar and the existing machine got the neutral values.
        var calendars = (await api.GetAsync<List<WorkCalendarModel>>("/api/calendars?includeInactive=true")).Ok;
        calendars.Should().Contain(c => c.Code == "STANDARD" && c.IsDefault && c.Slots.Count == 10);
        calendars.Count(c => c.IsDefault).Should().Be(1);
        var machine = (await api.GetAsync<List<EquipmentModel>>("/api/equipment")).Ok.Single(m => m.Code == "LEGACY-1");
        (machine.Capacity, machine.EfficiencyPercent, machine.WorkCalendarId).Should().Be((1, 100m, null));
        machine.EffectiveCalendar!.Code.Should().Be("STANDARD");
        calendars.Should().Contain(c => c.Code == "DOPPIO-TURNO");
        calendars.Should().HaveCount(2);
        (await api.GetAsync<List<CalendarExceptionModel>>($"/api/calendars/{calendars.Single(c => c.Code == "STANDARD").Id}/exceptions")).Ok.Should().HaveCount(25);
    }

    [Fact]
    public async Task Migration_RunTwiceOnRows_Success_DefaultCalendarSqlIsGuarded()
    {
        var connectionString = await fixture.CreateDatabaseAsync($"cal_{Guid.NewGuid():N}");
        var options = new DbContextOptionsBuilder<MicroMES.ApiCore.Persistence.AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new MicroMES.ApiCore.Persistence.AppDbContext(options);
        await db.Database.MigrateAsync();

        db.WorkCalendars.Count(c => c.IsDefault).Should().Be(1);
        db.WorkCalendarSlots.Count().Should().Be(10);
        // The unique filtered index refuses a second default.
        var second = () => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"WorkCalendars\" (\"Code\", \"Name\", \"IsActive\", \"IsDefault\", \"CreatedAt\", \"UpdatedAt\") VALUES ('ALTRO', 'x', TRUE, TRUE, now(), now())");
        await second.Should().ThrowAsync<Exception>();
    }
}
