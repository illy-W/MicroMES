using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProcessSegmentFeature.Repositories;

public class ProcessSegmentRepository(AppDbContext context)
    : GenericRepository<ProcessSegment, int, AppDbContext>(context), IProcessSegmentRepository
{
}
