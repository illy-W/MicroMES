using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

public class WorkOrderResponseRepository(AppDbContext context)
    : GenericRepository<WorkOrderResponse, int, AppDbContext>(context), IWorkOrderResponseRepository
{
}
