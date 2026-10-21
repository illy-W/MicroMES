using illyW.Framework.EFCore.Extensions;
using MicroMES.ApiFeatures.ScrapFeature.Repositories;
using MicroMES.ApiFeatures.ScrapFeature.Services;
using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiFeatures.ScrapFeature;

public static class Registrar
{
    public static IServiceCollection AddScrapFeature(this IServiceCollection services)
    {
        services.AddRepository<IScrapReasonRepository, ScrapReasonRepository>();
        services.AddRepository<IScrapReasonProcessSegmentRepository, ScrapReasonProcessSegmentRepository>();
        // IUnitOfWork is shared with the other features: register it only if nobody did yet.
        if (!services.Any(d => d.ServiceType == typeof(IUnitOfWork)))
            services.AddUnitOfWork<AppDbContext>();
        services.AddScoped<ScrapReasonService>();
        services.AddScoped<ScrapReportService>();
        return services;
    }
}
