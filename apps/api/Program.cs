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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

// Apply migrations only in Development, never during OpenAPI document generation.
if (app.Environment.IsDevelopment() && !isOpenApiEnabled)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    scope.ServiceProvider.GetRequiredService<MasterDataDevelopmentSeeder>().SeedAsync().GetAwaiter().GetResult();
    // The unit of work cannot reopen a transaction within the same scope: the calendar seeder gets its own.
    using (var calendarScope = app.Services.CreateScope())
        calendarScope.ServiceProvider.GetRequiredService<CalendarDevelopmentSeeder>().SeedAsync().GetAwaiter().GetResult();
    scope.ServiceProvider.GetRequiredService<ProductionOrderDevelopmentSeeder>().SeedAsync().GetAwaiter().GetResult();
    // After calendars and orders; own scope (the unit of work cannot reopen a transaction within the same scope).
    using (var planningScope = app.Services.CreateScope())
        planningScope.ServiceProvider.GetRequiredService<PlanningDevelopmentSeeder>().SeedAsync().GetAwaiter().GetResult();
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

app.Run();

// Exposes the entry point to WebApplicationFactory in the integration tests.
public partial class Program { }
