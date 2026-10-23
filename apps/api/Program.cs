using MicroMES.ApiFeatures.ToolingFeature.Seeding;
using System.Reflection;
using System.Text.Json.Serialization;
using MicroMES.Api;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Violations;
using MicroMES.ApiFeatures.BomFeature;
using MicroMES.ApiFeatures.CalendarFeature;
using MicroMES.ApiFeatures.CalendarFeature.Seeding;
using MicroMES.ApiFeatures.EquipmentFeature;
using MicroMES.ApiFeatures.ToolingFeature;
using MicroMES.ApiFeatures.MaterialFeature;
using MicroMES.ApiFeatures.ProcessSegmentFeature;
using MicroMES.ApiFeatures.PlanningFeature;
using MicroMES.ApiFeatures.PlanningFeature.Seeding;
using MicroMES.ApiFeatures.ProductionOrderFeature;
using MicroMES.ApiFeatures.ProductionOrderFeature.Seeding;
using MicroMES.ApiFeatures.RoutingFeature;
using MicroMES.ApiFeatures.ScrapFeature;
using MicroMES.ApiFeatures.SampleDataFeature;
using MicroMES.ApiFeatures.VersionFeature;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Outside Development the logs go to stdout as JSON (one line per entry, ready for log collectors).
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
}

// True only when the build runs with -p:OpenApiGenerateDocuments=true (document emission).
var isOpenApiEnabled = string.Equals(
    Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "OpenApiGenerateDocuments")?.Value,
    "true",
    StringComparison.OrdinalIgnoreCase);

// Add services to the container.

builder.Services
    .AddControllers(o => o.Filters.Add<ConstraintViolationResultFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Strict: numbers are plain integers in the OpenAPI schema (no integer|string with pattern).
        o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    });

// DataAnnotations validation failures answer 422 instead of the default 400.
builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = ctx =>
    {
        var problem = new ValidationProblemDetails(ctx.ModelState)
        {
            Status = StatusCodes.Status422UnprocessableEntity
        };
        return new UnprocessableEntityObjectResult(problem);
    };
});

// OpenAPI schema generation reads the minimal-API JSON options (not the MVC ones): enums as strings there too.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

builder.Services.AddOpenApi();

// The connection string is only needed at runtime: the lazy provider config lets the OpenAPI
// document generation start without a database.
builder.Services.AddConstraintViolationTracking();
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")
            ?? "Host=localhost;Database=micromes")
        .UseConstraintViolationTracking(sp));

// Unique/foreign-key violations that escape as exceptions answer 409 (see ConstraintViolationHandling).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ConstraintViolationExceptionHandler>();

builder.Services.AddMaterialFeature();
builder.Services.AddBomFeature();
builder.Services.AddProcessSegmentFeature();
builder.Services.AddCalendarFeature();
builder.Services.AddEquipmentFeature();
builder.Services.AddToolingFeature();
builder.Services.AddScrapFeature();
builder.Services.AddRoutingFeature();
builder.Services.AddProductionOrderFeature();
builder.Services.AddPlanningFeature();
builder.Services.AddSampleDataFeature();
builder.Services.AddVersionFeature();

// Liveness: the process answers. Readiness: the database answers too.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

// "Api.dll migrate": applies the pending migrations and exits (0 ok, 1 failure) without starting the web host.
if (args.FirstOrDefault() == "migrate")
    return DatabaseStartup.MigrateCommand(app.Services);

// Development applies the migrations and the sample data; elsewhere only the explicit Demo:Seed=true
// seeds the sample data (migrations are applied by "migrate"), and only on an empty database.
// Never during OpenAPI document generation.
if (!isOpenApiEnabled)
{
    if (app.Environment.IsDevelopment())
    {
        DatabaseStartup.Migrate(app.Services);
        DatabaseStartup.SeedSampleData(app.Services);
    }
    else if (app.Configuration.GetValue("Demo:Seed", false))
    {
        DatabaseStartup.Migrate(app.Services);
        DatabaseStartup.SeedSampleData(app.Services);
    }
}

// Configure the HTTP request pipeline.

// Outside Development the exception handler middleware answers ProblemDetails (violations -> 409).
// In Development it is not installed so the developer exception page keeps showing every other exception;
// the same handler still maps violations to 409 through a thin middleware.
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        try { await next(); }
        catch (Exception ex) when (!context.Response.HasStarted && ConstraintViolation.From(ex) is not null)
        {
            context.Response.Clear();
            var handler = context.RequestServices.GetServices<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>()
                .OfType<ConstraintViolationExceptionHandler>().First();
            await handler.TryHandleAsync(context, ex, context.RequestAborted);
        }
    });
}
else
{
    app.UseExceptionHandler();
}

if (app.Environment.IsDevelopment() || isOpenApiEnabled)
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

// Not part of the public contract: excluded from the OpenAPI document.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = c => c.Tags.Contains("live") }).ExcludeFromDescription();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).ExcludeFromDescription();

app.Run();
return 0;

// Exposes the entry point to WebApplicationFactory in the integration tests.
public partial class Program { }
