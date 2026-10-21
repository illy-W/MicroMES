using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.CalendarFeature.Repositories;

public class CalendarExceptionSlotRepository(AppDbContext context)
    : GenericRepository<CalendarExceptionSlot, int, AppDbContext>(context), ICalendarExceptionSlotRepository
{
}
