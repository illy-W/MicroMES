using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.CalendarFeature.Repositories;

public class WorkCalendarSlotRepository(AppDbContext context)
    : GenericRepository<WorkCalendarSlot, int, AppDbContext>(context), IWorkCalendarSlotRepository
{
}
