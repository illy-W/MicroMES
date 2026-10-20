using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MicroMES.ApiCore.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Design-time only (dotnet ef): matches the local docker-compose database.
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=micromes;Username=micromes;Password=micromes");

        return new AppDbContext(optionsBuilder.Options);
    }
}
