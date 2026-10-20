using illyW.Framework.EFCore.Extensions;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Repositories;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiFeatures.ProcessSegmentFeature;

public static class Registrar
{
    public static IServiceCollection AddProcessSegmentFeature(this IServiceCollection services)
    {
        services.AddRepository<IProcessSegmentRepository, ProcessSegmentRepository>();
        services.AddScoped<ProcessSegmentService>();
        return services;
    }
}
