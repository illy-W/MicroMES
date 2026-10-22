using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ToolingFeature.Repositories;

public interface IToolingRepository : IGenericRepository<Tooling, int>
{
}
