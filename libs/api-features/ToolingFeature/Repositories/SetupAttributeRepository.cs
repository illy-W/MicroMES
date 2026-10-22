using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ToolingFeature.Repositories;

public class SetupAttributeRepository(AppDbContext context)
    : GenericRepository<SetupAttribute, int, AppDbContext>(context), ISetupAttributeRepository
{
}
