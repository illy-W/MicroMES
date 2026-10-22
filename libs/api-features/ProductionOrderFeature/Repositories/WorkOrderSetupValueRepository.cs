using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

public class WorkOrderSetupValueRepository(AppDbContext context)
    : GenericRepository<WorkOrderSetupValue, int, AppDbContext>(context), IWorkOrderSetupValueRepository
{
}
