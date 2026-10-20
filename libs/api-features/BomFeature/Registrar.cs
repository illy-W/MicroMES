using illyW.Framework.EFCore.Extensions;
using MicroMES.ApiFeatures.BomFeature.Repositories;
using MicroMES.ApiFeatures.BomFeature.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiFeatures.BomFeature;

public static class Registrar
{
    public static IServiceCollection AddBomFeature(this IServiceCollection services)
    {
        services.AddRepository<IBomItemRepository, BomItemRepository>();
        services.AddScoped<BomService>();
        return services;
    }
}
