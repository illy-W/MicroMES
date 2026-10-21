using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;

namespace MicroMES.ApiTests.Shared.Fixtures;

/// <summary>Builds a <see cref="PlannerCatalog"/> in memory. Step ids are <c>materialId * 100 + operation</c>.</summary>
public sealed class PlannerCatalogBuilder
{
    private readonly PlannerCatalog _catalog = new();

    public static int StepId(int materialId, int operation) => materialId * 100 + operation;

    public PlannerCatalogBuilder Material(int id, MaterialType type, bool active = true)
    {
        _catalog.Materials[id] = new PlannerMaterial(id, $"M{id}", type, active);
        return this;
    }

    /// <summary>Routing of a material: steps as (operation, setup, run per unit), dependencies as operation pairs.</summary>
    public PlannerCatalogBuilder Routing(
        int materialId,
        (int Operation, decimal Setup, decimal Run)[] steps,
        params (int From, int To)[] dependencies)
    {
        _catalog.Routings[materialId] = new PlannerRouting(
            steps.Select(s => new PlannerStep(
                StepId(materialId, s.Operation), s.Operation, ProcessSegmentId: s.Operation,
                ProcessSegmentCode: $"SEG{s.Operation}", ProcessSegmentName: $"Segment {s.Operation}",
                s.Setup, s.Run, Notes: null)).ToList(),
            dependencies.Select(d => (StepId(materialId, d.From), StepId(materialId, d.To))).ToList());
        return this;
    }

    /// <summary>Linear routing 10 -> 20 -> ... with no setup and one minute per unit.</summary>
    public PlannerCatalogBuilder LinearRouting(int materialId, params int[] operations)
        => Routing(materialId,
            operations.Select(o => (o, 0m, 1m)).ToArray(),
            operations.Zip(operations.Skip(1), (a, b) => (a, b)).ToArray());

    /// <summary>BOM row; <paramref name="consumedInOperation"/> is the operation of the parent routing that consumes it (null = automatic).</summary>
    public PlannerCatalogBuilder Bom(int parentId, int componentId, decimal quantity, int? consumedInOperation = null)
    {
        if (!_catalog.Bom.TryGetValue(parentId, out var lines)) _catalog.Bom[parentId] = lines = [];
        lines.Add(new PlannerBomLine(componentId, quantity, consumedInOperation is { } op ? StepId(parentId, op) : null));
        return this;
    }

    public PlannerCatalog Build() => _catalog;
}
