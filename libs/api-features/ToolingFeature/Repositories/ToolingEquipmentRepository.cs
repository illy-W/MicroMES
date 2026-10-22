using illyW.Framework.EFCore;
using MicroMES.ApiCore.Persistence;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ToolingFeature.Repositories;

public class ToolingEquipmentRepository(AppDbContext context)
    : GenericRepository<ToolingEquipment, int, AppDbContext>(context), IToolingEquipmentRepository
{
}
