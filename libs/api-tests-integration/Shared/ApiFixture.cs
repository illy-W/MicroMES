using MicroMES.ApiCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace MicroMES.ApiTests.Integration.Shared;

/// <summary>
/// One PostgreSQL container for the whole run (same image as docker-compose), one in-memory API on top of it
/// with the migrations applied. <see cref="ResetAsync"/> empties the data between tests, keeping the migration history.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    // Same image as docker-compose.yml: it is already local, no pull needed.
    private const string PostgresImage = "postgres:17.6-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder().WithImage(PostgresImage).Build();
    private Respawner _respawner = null!;
    private ApiFactory _factory = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public ApiClient Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // "Testing": the app does not migrate on its own, the fixture does it (no seeders: tests start from an empty database).
        _factory = new ApiFactory(ConnectionString, "Testing");
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        Api = new ApiClient(_factory.CreateClient());

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    /// <summary>Runs code with a scoped <see cref="AppDbContext"/> of the test API (direct access to verify or arrange data).</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Creates an empty database in the same container and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync(string name)
    {
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using (var command = admin.CreateCommand())
        {
            command.CommandText = $"CREATE DATABASE \"{name}\"";
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}

/// <summary>Base of the tests: resets the database before each test (tests of the collection never run in parallel).</summary>
[Collection(ApiCollection.Name)]
public abstract class IntegrationTest(ApiFixture fixture) : IAsyncLifetime
{
    protected ApiFixture Fixture { get; } = fixture;

    protected ApiClient Api => Fixture.Api;

    public Task InitializeAsync() => Fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
