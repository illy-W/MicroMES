using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.RoutingFeature.Repositories;

public class RoutingStepDependencyRepository(AppDbContext context)
    : GenericRepository<RoutingStepDependency, int, AppDbContext>(context), IRoutingStepDependencyRepository
{
}
