using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// True only when the build runs with -p:OpenApiGenerateDocuments=true (document emission).
var isOpenApiEnabled = string.Equals(
    Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "OpenApiGenerateDocuments")?.Value,
    "true",
    StringComparison.OrdinalIgnoreCase);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment() || isOpenApiEnabled)
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
