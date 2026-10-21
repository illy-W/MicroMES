using illyW.Framework.Core.RepositoryPattern;
using illyW.Framework.EFCore.Extensions;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiFeatures.CalendarFeature;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;
using MicroMES.ApiFeatures.EquipmentFeature.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiFeatures.EquipmentFeature;

public static class Registrar
{
    public static IServiceCollection AddEquipmentFeature(this IServiceCollection services)
    {
        services.AddCalendarFeature();
        services.AddRepository<IEquipmentRepository, EquipmentRepository>();
        services.AddRepository<IEquipmentProcessSegmentRepository, EquipmentProcessSegmentRepository>();
        // IUnitOfWork is shared with the other features: register it only if nobody did yet.
        if (!services.Any(d => d.ServiceType == typeof(IUnitOfWork)))
            services.AddUnitOfWork<AppDbContext>();
        services.AddScoped<EquipmentSegmentLinker>();
        services.AddScoped<EquipmentSelector>();
        services.AddScoped<EquipmentService>();
        return services;
    }
}
