using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.RoutingFeature.Repositories;

public interface IRoutingStepRepository : IGenericRepository<RoutingStep, int>
{
}
