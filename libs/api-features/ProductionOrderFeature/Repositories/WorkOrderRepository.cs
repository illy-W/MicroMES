using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

public class WorkOrderRepository(AppDbContext context)
    : GenericRepository<WorkOrder, int, AppDbContext>(context), IWorkOrderRepository
{
}
