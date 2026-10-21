using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

public class WorkOrderDependencyRepository(AppDbContext context)
    : GenericRepository<WorkOrderDependency, int, AppDbContext>(context), IWorkOrderDependencyRepository
{
}
