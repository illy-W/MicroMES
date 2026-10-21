using System.Reflection;
using System.Text.Json.Serialization;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiFeatures.BomFeature;
using MicroMES.ApiFeatures.MaterialFeature;
using MicroMES.ApiFeatures.ProcessSegmentFeature;
using MicroMES.ApiFeatures.ProductionOrderFeature;
using MicroMES.ApiFeatures.ProductionOrderFeature.Seeding;
using MicroMES.ApiFeatures.RoutingFeature;
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
    .AddControllers()
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
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Database=micromes"));

builder.Services.AddMaterialFeature();
builder.Services.AddBomFeature();
builder.Services.AddProcessSegmentFeature();
builder.Services.AddRoutingFeature();
builder.Services.AddProductionOrderFeature();

var app = builder.Build();

// Apply migrations only in Development, never during OpenAPI document generation.
if (app.Environment.IsDevelopment() && !isOpenApiEnabled)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DevelopmentDataSeeder.SeedAsync(db).GetAwaiter().GetResult();
    scope.ServiceProvider.GetRequiredService<ProductionOrderDevelopmentSeeder>().SeedAsync().GetAwaiter().GetResult();
}

// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment() || isOpenApiEnabled)
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
