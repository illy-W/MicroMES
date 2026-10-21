using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.BomFeature.Models;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Repositories;
using MicroMES.ApiFeatures.RoutingFeature.Repositories;
using MicroMES.ApiFeatures.RoutingFeature.Services;

namespace MicroMES.ApiFeatures.BomFeature.Services;

/// <summary>Routing of some parent materials, loaded once to resolve the consumption step of BOM rows.</summary>
public class BomStepResolver
{
    public sealed record Resolution(BomStepModel? Consumed, BomEffectiveStepModel? Effective);

    private readonly Dictionary<int, RoutingStep> _steps = [];
    private readonly Dictionary<int, string> _segmentNames = [];
    private readonly Dictionary<int, RoutingStep?> _firstStart = [];

    public static async Task<BomStepResolver> LoadAsync(
        IEnumerable<int> parentMaterialIds,
        IRoutingStepRepository steps,
        IRoutingStepDependencyRepository dependencies,
        IProcessSegmentRepository segments)
    {
        var resolver = new BomStepResolver();
        var parents = parentMaterialIds.Distinct().ToList();
        var byParent = new Dictionary<int, List<RoutingStep>>();
        await foreach (var s in steps.FetchAsync(x => parents.Contains(x.MaterialId)))
        {
            resolver._steps[s.Id] = s;
            if (!byParent.TryGetValue(s.MaterialId, out var list)) byParent[s.MaterialId] = list = [];
            list.Add(s);
        }

        var stepIds = resolver._steps.Keys.ToList();
        var segmentIds = resolver._steps.Values.Select(s => s.ProcessSegmentId).Distinct().ToList();
        await foreach (var seg in segments.FetchAsync(x => segmentIds.Contains(x.Id))) resolver._segmentNames[seg.Id] = seg.Name;
        var deps = new List<RoutingStepDependency>();
        await foreach (var d in dependencies.FetchAsync(x => stepIds.Contains(x.SuccessorStepId))) deps.Add(d);

        foreach (var parent in parents)
            resolver._firstStart[parent] = byParent.TryGetValue(parent, out var list)
                ? RoutingStepRules.FirstStartStep(list, deps.Where(d => list.Any(s => s.Id == d.SuccessorStepId)))
                : null;
        return resolver;
    }

    public RoutingStep? Find(int stepId) => _steps.GetValueOrDefault(stepId);

    public Resolution Resolve(BomItem item)
    {
        var explicitStep = item.ConsumedInStepId is { } id ? _steps.GetValueOrDefault(id) : null;
        var effective = explicitStep ?? _firstStart.GetValueOrDefault(item.ParentMaterialId);
        return new Resolution(
            explicitStep is null ? null : new BomStepModel { Id = explicitStep.Id, OperationNumber = explicitStep.OperationNumber, ProcessSegmentName = _segmentNames[explicitStep.ProcessSegmentId] },
            effective is null ? null : new BomEffectiveStepModel
            {
                Id = effective.Id, OperationNumber = effective.OperationNumber,
                ProcessSegmentName = _segmentNames[effective.ProcessSegmentId], IsAutomatic = explicitStep is null
            });
    }
}
