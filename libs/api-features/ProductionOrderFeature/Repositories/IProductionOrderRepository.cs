using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

public interface IProductionOrderRepository : IGenericRepository<ProductionOrder, int>
{
    /// <summary>Detaches every tracked entity (after a failed save, so the next attempt starts clean).</summary>
    void DiscardPendingChanges();
}
