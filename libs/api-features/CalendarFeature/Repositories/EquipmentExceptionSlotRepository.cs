using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.CalendarFeature.Repositories;

public class EquipmentExceptionSlotRepository(AppDbContext context)
    : GenericRepository<EquipmentExceptionSlot, int, AppDbContext>(context), IEquipmentExceptionSlotRepository
{
}
