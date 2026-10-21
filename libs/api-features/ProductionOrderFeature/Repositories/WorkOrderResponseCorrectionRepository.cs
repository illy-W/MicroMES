using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

public class WorkOrderResponseCorrectionRepository(AppDbContext context)
    : GenericRepository<WorkOrderResponseCorrection, int, AppDbContext>(context), IWorkOrderResponseCorrectionRepository
{
}
