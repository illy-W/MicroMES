using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.CalendarFeature.Repositories;

public class CalendarExceptionRepository(AppDbContext context)
    : GenericRepository<CalendarException, int, AppDbContext>(context), ICalendarExceptionRepository
{
}
