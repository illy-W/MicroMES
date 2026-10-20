using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.RoutingFeature.Repositories;

public class RoutingStepRepository(AppDbContext context)
    : GenericRepository<RoutingStep, int, AppDbContext>(context), IRoutingStepRepository
{
}
