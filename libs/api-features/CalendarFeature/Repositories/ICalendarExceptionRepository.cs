using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.CalendarFeature.Repositories;

public interface ICalendarExceptionRepository : IGenericRepository<CalendarException, int>
{
}
