using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiFeatures.SampleDataFeature;

public static class Registrar
{
    public static IServiceCollection AddSampleDataFeature(this IServiceCollection services)
    {
        services.AddScoped<MasterDataDevelopmentSeeder>();
        return services;
    }
}
