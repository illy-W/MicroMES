using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ScrapFeature.Repositories;

public class ScrapReasonRepository(AppDbContext context)
    : GenericRepository<ScrapReason, int, AppDbContext>(context), IScrapReasonRepository
{
}
