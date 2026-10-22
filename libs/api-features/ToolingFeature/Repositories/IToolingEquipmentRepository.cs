using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ToolingFeature.Repositories;

public interface IToolingEquipmentRepository : IGenericRepository<ToolingEquipment, int>
{
}
