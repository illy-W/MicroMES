using illyW.Framework.Core.RepositoryPattern;
using illyW.Framework.EFCore.Extensions;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiFeatures.CalendarFeature.Repositories;
using MicroMES.ApiFeatures.CalendarFeature.Seeding;
using MicroMES.ApiFeatures.CalendarFeature.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiFeatures.CalendarFeature;

public static class Registrar
{
    /// <summary>Idempotent: also called by <c>AddEquipmentFeature</c>. Needs a <see cref="TimeProvider"/> (registered by the production order feature).</summary>
    public static IServiceCollection AddCalendarFeature(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(CalendarService))) return services;
        services.AddRepository<IWorkCalendarRepository, WorkCalendarRepository>();
        services.AddRepository<IWorkCalendarSlotRepository, WorkCalendarSlotRepository>();
        services.AddRepository<ICalendarExceptionRepository, CalendarExceptionRepository>();
        services.AddRepository<ICalendarExceptionSlotRepository, CalendarExceptionSlotRepository>();
        services.AddRepository<IEquipmentExceptionRepository, EquipmentExceptionRepository>();
        services.AddRepository<IEquipmentExceptionSlotRepository, EquipmentExceptionSlotRepository>();
        services.AddRepository<ISystemSettingRepository, SystemSettingRepository>();
        if (!services.Any(d => d.ServiceType == typeof(IUnitOfWork)))
            services.AddUnitOfWork<AppDbContext>();
        services.AddScoped<EffectiveCalendarService>();
        services.AddScoped<CalendarService>();
        services.AddScoped<CalendarExceptionService>();
        services.AddScoped<CalendarDevelopmentSeeder>();
        return services;
    }
}
