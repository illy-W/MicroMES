using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProcessSegmentFeature.Repositories;

public interface IProcessSegmentRepository : IGenericRepository<ProcessSegment, int>
{
}
