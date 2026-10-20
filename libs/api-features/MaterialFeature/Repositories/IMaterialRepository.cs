using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.MaterialFeature.Repositories;

public interface IMaterialRepository : IGenericRepository<Material, int>
{
}
