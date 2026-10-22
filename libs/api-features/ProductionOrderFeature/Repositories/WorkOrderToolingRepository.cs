using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

public class WorkOrderToolingRepository(AppDbContext context)
    : GenericRepository<WorkOrderTooling, int, AppDbContext>(context), IWorkOrderToolingRepository
{
}
