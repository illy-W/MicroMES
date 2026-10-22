using illyW.Framework.Core.RepositoryPattern;
using illyW.Framework.EFCore.Extensions;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiFeatures.EquipmentFeature;
using MicroMES.ApiFeatures.ToolingFeature.Repositories;
using MicroMES.ApiFeatures.ToolingFeature.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiFeatures.ToolingFeature;

public static class Registrar
{
    public static IServiceCollection AddToolingFeature(this IServiceCollection services)
    {
        services.AddEquipmentFeature();
        services.AddRepository<IToolingRepository, ToolingRepository>();
        services.AddRepository<IToolingEquipmentRepository, ToolingEquipmentRepository>();
        services.AddRepository<ISetupAttributeRepository, SetupAttributeRepository>();
        // IUnitOfWork is shared with the other features: register it only if nobody did yet.
        if (!services.Any(d => d.ServiceType == typeof(IUnitOfWork)))
            services.AddUnitOfWork<AppDbContext>();
        services.AddScoped<ToolingService>();
        services.AddScoped<SetupAttributeService>();
        return services;
    }
}
