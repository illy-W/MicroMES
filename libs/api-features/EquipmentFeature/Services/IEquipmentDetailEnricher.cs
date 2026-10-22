using MicroMES.ApiFeatures.EquipmentFeature.Models;

namespace MicroMES.ApiFeatures.EquipmentFeature.Services;

/// <summary>Adds to the detail of a machine data owned by other features (tooling, mounted state), so the list stays light.</summary>
public interface IEquipmentDetailEnricher
{
    Task EnrichAsync(EquipmentModel model);
}
