using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.RoutingFeature.Repositories;

public class RoutingStepToolingRepository(AppDbContext context)
    : GenericRepository<RoutingStepTooling, int, AppDbContext>(context), IRoutingStepToolingRepository
{
}
