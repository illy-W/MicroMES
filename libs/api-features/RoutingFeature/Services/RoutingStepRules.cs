using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.RoutingFeature.Services;

/// <summary>Pure routing rules shared by BOM, routing and release planning.</summary>
public static class RoutingStepRules
{
    /// <summary>
    /// Default consumption step: the lowest operation number among the steps with no predecessor.
    /// Null when the routing has no steps.
    /// </summary>
    public static RoutingStep? FirstStartStep(IReadOnlyCollection<RoutingStep> steps, IEnumerable<RoutingStepDependency> dependencies)
    {
        var successors = dependencies.Select(d => d.SuccessorStepId).ToHashSet();
        return steps.Where(s => !successors.Contains(s.Id)).OrderBy(s => s.OperationNumber).FirstOrDefault();
    }
}
