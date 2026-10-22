using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ToolingFeature.Repositories;

public class ToolingRepository(AppDbContext context)
    : GenericRepository<Tooling, int, AppDbContext>(context), IToolingRepository
{
}
