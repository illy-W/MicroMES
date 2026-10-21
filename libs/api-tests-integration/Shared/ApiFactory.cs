using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MicroMES.ApiTests.Integration.Shared;

/// <summary>
/// Hosts the real API (<c>Program</c>) in memory against a given PostgreSQL connection string.
/// The environment decides the startup behaviour: "Development" applies the migrations and the seeders
/// (the application does that only there), anything else leaves the database to the test fixture.
/// </summary>
public sealed class ApiFactory(string connectionString, string environment) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Default", connectionString);
    }
}
