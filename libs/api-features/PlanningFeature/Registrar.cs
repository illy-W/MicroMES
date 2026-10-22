using MicroMES.ApiFeatures.PlanningFeature.Engine;
using MicroMES.ApiFeatures.PlanningFeature.Seeding;
using MicroMES.ApiFeatures.PlanningFeature.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiFeatures.PlanningFeature;

public static class Registrar
{
    /// <summary>Needs the production order and equipment features (repositories, unit of work, time provider).</summary>
    public static IServiceCollection AddPlanningFeature(this IServiceCollection services)
    {
        services.AddSingleton<IPlanningEngine, ForwardFiniteCapacityEngine>();
        services.AddScoped<PlanningCatalogLoader>();
        services.AddScoped<PlanningService>();
        services.AddScoped<TimelineService>();
        services.AddScoped<EquipmentOverviewService>();
        services.AddScoped<MicroMES.ApiFeatures.EquipmentFeature.Services.IEquipmentDetailEnricher, EquipmentToolingEnricher>();
        services.AddScoped<PlanLockService>();
        services.AddScoped<PlanningDevelopmentSeeder>();
        return services;
    }
}
