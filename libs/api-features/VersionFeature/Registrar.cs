using System.Reflection;
using MicroMES.ApiFeatures.VersionFeature.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MicroMES.ApiFeatures.VersionFeature;

public static class Registrar
{
    public static IServiceCollection AddVersionFeature(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(_ => BuildInfo.FromAssembly(Assembly.GetEntryAssembly()));
        services.AddHttpClient(VersionService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MicroMES-UpdateCheck");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        });
        services.AddSingleton<VersionService>();
        return services;
    }
}
