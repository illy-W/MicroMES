using illyW.Framework.EFCore.Extensions;
using MicroMES.ApiFeatures.MaterialFeature.Repositories;
using MicroMES.ApiFeatures.MaterialFeature.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiFeatures.MaterialFeature;

public static class Registrar
{
    public static IServiceCollection AddMaterialFeature(this IServiceCollection services)
    {
        services.AddRepository<IMaterialRepository, MaterialRepository>();
        services.AddScoped<MaterialService>();
        return services;
    }
}
