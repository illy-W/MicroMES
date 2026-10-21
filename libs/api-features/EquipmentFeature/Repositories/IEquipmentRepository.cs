using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.EquipmentFeature.Repositories;

public interface IEquipmentRepository : IGenericRepository<Equipment, int>
{
}
