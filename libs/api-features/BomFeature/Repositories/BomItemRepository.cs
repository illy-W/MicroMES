using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.BomFeature.Repositories;

public class BomItemRepository(AppDbContext context)
    : GenericRepository<BomItem, int, AppDbContext>(context), IBomItemRepository
{
}
