using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;

public interface IWorkOrderMaterialRepository : IGenericRepository<WorkOrderMaterial, int>
{
}
