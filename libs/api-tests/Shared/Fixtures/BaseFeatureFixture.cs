using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.BomFeature;
using MicroMES.ApiFeatures.MaterialFeature;
using MicroMES.ApiFeatures.ProcessSegmentFeature;
using MicroMES.ApiFeatures.ProductionOrderFeature;
using MicroMES.ApiFeatures.ScrapFeature;
using MicroMES.ApiFeatures.RoutingFeature;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiTests.Shared.Fixtures;

/// <summary>
/// Base for the service tests: a private in-memory SQLite database (foreign keys on) per test instance and the
/// real feature registrations, so services get the same repositories and unit of work as in the API.
/// Every <see cref="Service{T}"/> call opens a new scope (like a request): the unit of work cannot reopen a
/// transaction within the same scope.
/// </summary>
public abstract class BaseFeatureFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly List<IServiceScope> _scopes = [];

    protected BaseFeatureFixture()
    {
        // A ":memory:" database lives as long as its connection is open and is private to it.
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddMaterialFeature();
        services.AddBomFeature();
        services.AddProcessSegmentFeature();
        services.AddRoutingFeature();
        services.AddScrapFeature();
        services.AddProductionOrderFeature();
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    /// <summary>
    /// Resolves a service from a fresh scope. The previous scopes are disposed first: the single SQLite
    /// connection is shared, and a committed transaction stays attached to it until its scope is disposed
    /// (SQLite has no nested transactions). Await each call before requesting the next service.
    /// </summary>
    protected T Service<T>() where T : notnull
    {
        DisposeScopes();
        var scope = _provider.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>Reads the database directly (fresh context, so nothing comes from a change tracker).</summary>
    protected T Query<T>(Func<AppDbContext, T> read)
    {
        DisposeScopes();
        using var scope = _provider.CreateScope();
        return read(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected int AddMaterial(string code, MaterialType type = MaterialType.FinishedGood, bool active = true)
    {
        DisposeScopes();
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var material = new Material
        {
            Code = code, Name = code, Type = type, UnitOfMeasure = UnitOfMeasure.Piece,
            IsActive = active, CreatedAt = now, UpdatedAt = now
        };
        db.Materials.Add(material);
        db.SaveChanges();
        return material.Id;
    }

    protected int AddSegment(string code, bool active = true, decimal setup = 0, decimal run = 0)
    {
        DisposeScopes();
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var segment = new ProcessSegment
        {
            Code = code, Name = code, SetupMinutes = setup, RunMinutesPerUnit = run,
            IsActive = active, CreatedAt = now, UpdatedAt = now
        };
        db.ProcessSegments.Add(segment);
        db.SaveChanges();
        return segment.Id;
    }

    /// <summary>Adds a routing step; <paramref name="afterStepId"/> makes it a successor of that step.</summary>
    protected int AddStep(int materialId, int segmentId, int operation, int? afterStepId = null)
    {
        DisposeScopes();
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var step = new RoutingStep
        {
            MaterialId = materialId, ProcessSegmentId = segmentId, OperationNumber = operation, CreatedAt = now, UpdatedAt = now
        };
        db.RoutingSteps.Add(step);
        db.SaveChanges();
        if (afterStepId is { } pred)
        {
            db.RoutingStepDependencies.Add(new RoutingStepDependency { PredecessorStepId = pred, SuccessorStepId = step.Id });
            db.SaveChanges();
        }
        return step.Id;
    }

    /// <summary>Arranges data directly through the DbContext (fresh scope, saved at the end).</summary>
    protected void Arrange(Action<AppDbContext> action)
    {
        DisposeScopes();
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        action(db);
        db.SaveChanges();
    }

    private void DisposeScopes()
    {
        foreach (var scope in _scopes) scope.Dispose();
        _scopes.Clear();
    }

    public void Dispose()
    {
        DisposeScopes();
        _provider.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
