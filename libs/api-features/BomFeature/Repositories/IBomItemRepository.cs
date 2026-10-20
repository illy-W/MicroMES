using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.BomFeature.Repositories;

public interface IBomItemRepository : IGenericRepository<BomItem, int>
{
}
