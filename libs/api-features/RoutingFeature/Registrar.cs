using illyW.Framework.EFCore.Extensions;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiFeatures.RoutingFeature.Repositories;
using MicroMES.ApiFeatures.RoutingFeature.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiFeatures.RoutingFeature;

public static class Registrar
{
    public static IServiceCollection AddRoutingFeature(this IServiceCollection services)
    {
        services.AddRepository<IRoutingStepRepository, RoutingStepRepository>();
        services.AddRepository<IRoutingStepDependencyRepository, RoutingStepDependencyRepository>();
        services.AddRepository<IRoutingStepToolingRepository, RoutingStepToolingRepository>();
        services.AddRepository<IRoutingStepSetupValueRepository, RoutingStepSetupValueRepository>();
        services.AddUnitOfWork<AppDbContext>();
        services.AddScoped<RoutingService>();
        return services;
    }
}
