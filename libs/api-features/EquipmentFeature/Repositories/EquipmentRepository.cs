using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.EquipmentFeature.Repositories;

public class EquipmentRepository(AppDbContext context)
    : GenericRepository<Equipment, int, AppDbContext>(context), IEquipmentRepository
{
}
