using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.RoutingFeature.Repositories;

public class RoutingStepSetupValueRepository(AppDbContext context)
    : GenericRepository<RoutingStepSetupValue, int, AppDbContext>(context), IRoutingStepSetupValueRepository
{
}
