using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

public class WorkOrderResponseScrapRepository(AppDbContext context)
    : GenericRepository<WorkOrderResponseScrap, int, AppDbContext>(context), IWorkOrderResponseScrapRepository
{
}
