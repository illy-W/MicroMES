using MicroMES.ApiCore.Persistence;
using MicroMES.ApiFeatures.CalendarFeature.Seeding;
using MicroMES.ApiFeatures.PlanningFeature.Seeding;
using MicroMES.ApiFeatures.ProductionOrderFeature.Seeding;
using MicroMES.ApiFeatures.SampleDataFeature;
using MicroMES.ApiFeatures.ToolingFeature.Seeding;
using Microsoft.EntityFrameworkCore;

namespace MicroMES.Api;

/// <summary>Startup database operations (migrations and sample data): the only place of the host that touches the DbContext.</summary>
public static class DatabaseStartup
{
    public static void Migrate(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }

    public static int PendingMigrations(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetPendingMigrations().Count();
    }

    /// <summary>
    /// Startup work of the host: migrations (unless Database:AutoMigrate=false) and then, in Development or with
    /// Demo:Seed=true, the sample data (only on an empty database, only when no migration is pending).
    /// </summary>
    public static void Startup(IServiceProvider services, IConfiguration configuration, bool isDevelopment)
    {
        if (configuration.GetValue("Database:AutoMigrate", true))
            Migrate(services);

        if ((isDevelopment || configuration.GetValue("Demo:Seed", false)) && PendingMigrations(services) == 0)
            SeedSampleData(services);
    }

    /// <summary>CLI command "migrate": returns the process exit code (0 ok, 1 error) and writes a clear message.</summary>
    public static int MigrateCommand(IServiceProvider services)
    {
        try
        {
            Console.WriteLine("Applicazione delle migration al database in corso...");
            Migrate(services);
            Console.WriteLine("Database aggiornato: nessuna migration in sospeso.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Migration non riuscita: {ex.GetBaseException().Message}");
            return 1;
        }
    }

    /// <summary>Sample data in dependency order. Each seeder runs only when its own tables are empty (an existing database is left alone).</summary>
    public static void SeedSampleData(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<MasterDataDevelopmentSeeder>().SeedAsync().GetAwaiter().GetResult();
        // The unit of work cannot reopen a transaction within the same scope: the calendar seeder gets its own.
        using (var calendarScope = services.CreateScope())
            calendarScope.ServiceProvider.GetRequiredService<CalendarDevelopmentSeeder>().SeedAsync().GetAwaiter().GetResult();
        // Before the order seeder: on a fresh database the released orders take the tooling snapshots. Own scope (transaction).
        using (var toolingScope = services.CreateScope())
            toolingScope.ServiceProvider.GetRequiredService<ToolingDevelopmentSeeder>().SeedAsync().GetAwaiter().GetResult();
        scope.ServiceProvider.GetRequiredService<ProductionOrderDevelopmentSeeder>().SeedAsync().GetAwaiter().GetResult();
        // After calendars and orders; own scope (transaction).
        using (var planningScope = services.CreateScope())
            planningScope.ServiceProvider.GetRequiredService<PlanningDevelopmentSeeder>().SeedAsync().GetAwaiter().GetResult();
    }
}
