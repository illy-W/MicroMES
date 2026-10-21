using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

public class WorkOrderMaterialRepository(AppDbContext context)
    : GenericRepository<WorkOrderMaterial, int, AppDbContext>(context), IWorkOrderMaterialRepository
{
}
