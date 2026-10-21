using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

public class ProductionOrderRepository(AppDbContext context)
    : GenericRepository<ProductionOrder, int, AppDbContext>(context), IProductionOrderRepository
{
    public void DiscardPendingChanges() => context.ChangeTracker.Clear();
}
