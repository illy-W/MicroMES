using illyW.Framework.EFCore.Extensions;
using MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;
using MicroMES.ApiFeatures.ProductionOrderFeature.Seeding;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MicroMES.ApiFeatures.ProductionOrderFeature;

public static class Registrar
{
    public static IServiceCollection AddProductionOrderFeature(this IServiceCollection services)
    {
        services.AddRepository<IProductionOrderRepository, ProductionOrderRepository>();
        services.AddRepository<IWorkOrderRepository, WorkOrderRepository>();
        services.AddRepository<IWorkOrderDependencyRepository, WorkOrderDependencyRepository>();
        services.AddRepository<IWorkOrderMaterialRepository, WorkOrderMaterialRepository>();
        services.AddRepository<IWorkOrderResponseRepository, WorkOrderResponseRepository>();
        // IUnitOfWork is shared with RoutingFeature: register it only if nobody did yet.
        if (!services.Any(d => d.ServiceType == typeof(IUnitOfWork)))
            services.AddUnitOfWork<AppDbContext>();
        // Passes through to the system clock; only the development seeder moves it (past timestamps).
        services.TryAddSingleton<OverridableTimeProvider>();
        services.TryAddSingleton<TimeProvider>(sp => sp.GetRequiredService<OverridableTimeProvider>());
        services.AddScoped<ProductionOrderDevelopmentSeeder>();
        services.AddScoped<ProductionDataLoader>();
        services.AddScoped<ProductionOrderStatusUpdater>();
        services.AddScoped<WorkOrderService>();
        services.AddScoped<ProductionOrderService>();
        services.AddScoped<ProductionOrderOverviewService>();
        return services;
    }
}
