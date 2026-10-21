using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ScrapFeature.Repositories;

public class ScrapReasonProcessSegmentRepository(AppDbContext context)
    : GenericRepository<ScrapReasonProcessSegment, int, AppDbContext>(context), IScrapReasonProcessSegmentRepository
{
}
