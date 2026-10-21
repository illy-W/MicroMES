using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiCore.Persistence.Violations;

public static class ConstraintViolationExtensions
{
    public static IServiceCollection AddConstraintViolationTracking(this IServiceCollection services)
    {
        services.AddScoped<ConstraintViolationTracker>();
        services.AddScoped<ConstraintViolationInterceptor>();
        return services;
    }

    public static DbContextOptionsBuilder UseConstraintViolationTracking(this DbContextOptionsBuilder options, IServiceProvider sp)
        => options.AddInterceptors(sp.GetRequiredService<ConstraintViolationInterceptor>());
}
