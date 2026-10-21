using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.CalendarFeature.Repositories;

public class SystemSettingRepository(AppDbContext context)
    : GenericRepository<SystemSetting, int, AppDbContext>(context), ISystemSettingRepository
{
}
