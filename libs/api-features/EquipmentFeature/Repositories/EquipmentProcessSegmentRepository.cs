using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.EquipmentFeature.Repositories;

public class EquipmentProcessSegmentRepository(AppDbContext context)
    : GenericRepository<EquipmentProcessSegment, int, AppDbContext>(context), IEquipmentProcessSegmentRepository
{
}
