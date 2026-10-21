using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.CalendarFeature.Repositories;

public class EquipmentExceptionRepository(AppDbContext context)
    : GenericRepository<EquipmentException, int, AppDbContext>(context), IEquipmentExceptionRepository
{
}
