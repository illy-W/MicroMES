using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ScrapFeature.Repositories;

public interface IScrapReasonRepository : IGenericRepository<ScrapReason, int>
{
}
