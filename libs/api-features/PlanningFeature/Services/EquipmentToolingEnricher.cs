using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiFeatures.EquipmentFeature.Services;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiFeatures.ToolingFeature.Repositories;

namespace MicroMES.ApiFeatures.PlanningFeature.Services;

/// <summary>
/// Compatible and universal tooling of a machine for its detail. "Mounted now" comes from the same
/// <see cref="EquipmentOverviewCalculator.MountedStateOf"/> used by the overview. Lives in the planning feature because it needs the production data.
/// </summary>
public class EquipmentToolingEnricher(IToolingRepository tooling, IToolingEquipmentRepository links, ProductionDataLoader loader) : IEquipmentDetailEnricher
{
    public async Task EnrichAsync(EquipmentModel model)
    {
        var tools = new List<Tooling>();
        await foreach (var t in tooling.FetchAsync(_ => true)) tools.Add(t);
        var all = new List<ToolingEquipment>();
        await foreach (var l in links.FetchAsync(_ => true)) all.Add(l);
        var data = await loader.LoadAllAsync();
        var mounted = EquipmentOverviewCalculator.MountedStateOf(data, model.Id).Tooling.Select(t => t.Id).ToHashSet();
        Apply(model, tools, all, mounted);
    }

    /// <summary>Explicit links to the machine -> compatible; tooling with no links at all -> universal.</summary>
    public static void Apply(EquipmentModel model, IReadOnlyCollection<Tooling> tools, IReadOnlyCollection<ToolingEquipment> links, IReadOnlySet<int> mountedIds)
    {
        var linked = links.GroupBy(l => l.ToolingId).ToDictionary(g => g.Key, g => g.Select(l => l.EquipmentId).ToHashSet());
        EquipmentToolingModel ToModel(Tooling t) => new()
        {
            Id = t.Id, Code = t.Code, Name = t.Name, Quantity = t.Quantity, SetupMinutes = t.SetupMinutes,
            IsActive = t.IsActive, IsMountedNow = mountedIds.Contains(t.Id)
        };
        model.CompatibleTooling = tools.Where(t => linked.TryGetValue(t.Id, out var e) && e.Contains(model.Id))
            .OrderBy(t => t.Code, StringComparer.Ordinal).Select(ToModel).ToList();
        model.UniversalTooling = tools.Where(t => !linked.ContainsKey(t.Id))
            .OrderBy(t => t.Code, StringComparer.Ordinal).Select(ToModel).ToList();
    }
}
