using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.MaterialFeature.Repositories;

public class MaterialRepository(AppDbContext context)
    : GenericRepository<Material, int, AppDbContext>(context), IMaterialRepository
{
}
