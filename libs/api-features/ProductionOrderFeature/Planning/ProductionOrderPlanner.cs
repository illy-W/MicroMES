using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Planning;

public record PlannerMaterial(int Id, string Code, MaterialType Type, bool IsActive);

public record PlannerBomLine(int ComponentMaterialId, decimal Quantity);

/// <summary>Routing step with the effective times already resolved (step value, else process segment value).</summary>
public record PlannerStep(
    int StepId,
    int OperationNumber,
    int ProcessSegmentId,
    string ProcessSegmentCode,
    string ProcessSegmentName,
    decimal SetupMinutes,
    decimal RunMinutesPerUnit,
    string? Notes);

public record PlannerRouting(IReadOnlyList<PlannerStep> Steps, IReadOnlyList<(int PredecessorStepId, int SuccessorStepId)> Dependencies);

/// <summary>Master data the planner reads (already loaded by the service).</summary>
public class PlannerCatalog
{
    public Dictionary<int, PlannerMaterial> Materials { get; init; } = [];
    public Dictionary<int, List<PlannerBomLine>> Bom { get; init; } = [];
    public Dictionary<int, PlannerRouting> Routings { get; init; } = [];
}

public class PlannedWorkOrder
{
    public required PlannerStep Step { get; init; }
    public required decimal PlannedQuantity { get; init; }
    public int OperationNumber => Step.OperationNumber;
    public decimal PlannedMinutes => ProductionOrderPlanner.PlannedMinutes(Step.SetupMinutes, Step.RunMinutesPerUnit, PlannedQuantity);
}

public class PlannedMaterial
{
    public required int MaterialId { get; init; }
    public required decimal RequiredQuantity { get; init; }
    public PlannedOrder? Child { get; init; }
}

public class PlannedOrder
{
    public required int MaterialId { get; init; }
    public required decimal Quantity { get; init; }
    public List<PlannedWorkOrder> WorkOrders { get; } = [];
    /// <summary>Dependencies inside this order plus those from the last step of each child to the start steps of this order.</summary>
    public List<(PlannedWorkOrder Predecessor, PlannedWorkOrder Successor)> Dependencies { get; } = [];
    public List<PlannedMaterial> Materials { get; } = [];
    public List<PlannedOrder> Children { get; } = [];
    /// <summary>Steps with no predecessor in the routing (before the child dependencies).</summary>
    public List<PlannedWorkOrder> StartSteps { get; } = [];
    /// <summary>Steps with no successor in the routing.</summary>
    public List<PlannedWorkOrder> FinalSteps { get; } = [];
}

public enum PlanError
{
    None,
    EmptyRouting,
    CyclicBom
}

public class PlanResult
{
    public PlannedOrder? Order { get; init; }
    public PlanError Error { get; init; }
    public string? Message { get; init; }
}

/// <summary>Pure planning logic: no I/O, no entities persisted.</summary>
public static class ProductionOrderPlanner
{
    public const int MaxDepth = 10;

    public static decimal PlannedMinutes(decimal setup, decimal runPerUnit, decimal quantity) => setup + runPerUnit * quantity;

    public static PlanResult Plan(int materialId, decimal quantity, PlannerCatalog catalog)
    {
        if (!catalog.Routings.TryGetValue(materialId, out var routing) || routing.Steps.Count == 0)
            return new PlanResult { Error = PlanError.EmptyRouting, Message = "Il materiale non ha un ciclo di lavoro" };

        try
        {
            var order = Build(materialId, quantity, catalog, [materialId], 1);
            return new PlanResult { Order = order };
        }
        catch (CyclicBomException)
        {
            return new PlanResult { Error = PlanError.CyclicBom, Message = "Distinta base ciclica" };
        }
    }

    private static PlannedOrder Build(int materialId, decimal quantity, PlannerCatalog catalog, List<int> path, int depth)
    {
        if (depth > MaxDepth) throw new CyclicBomException();

        var routing = catalog.Routings[materialId];
        var order = new PlannedOrder { MaterialId = materialId, Quantity = quantity };

        var byStep = new Dictionary<int, PlannedWorkOrder>();
        foreach (var step in routing.Steps.OrderBy(s => s.OperationNumber))
        {
            var wo = new PlannedWorkOrder { Step = step, PlannedQuantity = quantity };
            order.WorkOrders.Add(wo);
            byStep[step.StepId] = wo;
        }

        var hasPredecessor = new HashSet<int>();
        var hasSuccessor = new HashSet<int>();
        foreach (var (p, s) in routing.Dependencies)
        {
            order.Dependencies.Add((byStep[p], byStep[s]));
            hasSuccessor.Add(p);
            hasPredecessor.Add(s);
        }

        order.StartSteps.AddRange(order.WorkOrders.Where(w => !hasPredecessor.Contains(w.Step.StepId)));
        order.FinalSteps.AddRange(order.WorkOrders.Where(w => !hasSuccessor.Contains(w.Step.StepId)));

        foreach (var line in catalog.Bom.GetValueOrDefault(materialId) ?? [])
        {
            var required = line.Quantity * quantity;
            PlannedOrder? child = null;

            var component = catalog.Materials[line.ComponentMaterialId];
            if (component.Type is MaterialType.Intermediate or MaterialType.FinishedGood)
            {
                if (path.Contains(component.Id)) throw new CyclicBomException();

                if (catalog.Routings.TryGetValue(component.Id, out var childRouting) && childRouting.Steps.Count > 0)
                {
                    path.Add(component.Id);
                    child = Build(component.Id, required, catalog, path, depth + 1);
                    path.RemoveAt(path.Count - 1);

                    order.Children.Add(child);
                    // The last step of the child gates every start step of this order.
                    foreach (var last in child.FinalSteps)
                        foreach (var start in order.StartSteps)
                            order.Dependencies.Add((last, start));
                }
            }

            order.Materials.Add(new PlannedMaterial { MaterialId = component.Id, RequiredQuantity = required, Child = child });
        }

        return order;
    }

    /// <summary>The start step that receives the required materials: lowest operation number among the steps with no predecessor.</summary>
    public static PlannedWorkOrder FirstStartStep(PlannedOrder order) => order.StartSteps.OrderBy(w => w.OperationNumber).First();

    /// <summary>
    /// Earliest finish of every node of a dependency graph (longest path using the node durations).
    /// Nodes may belong to different orders; the graph must be acyclic.
    /// </summary>
    public static Dictionary<int, decimal> EarliestFinish(
        IReadOnlyDictionary<int, decimal> durations,
        IEnumerable<(int Predecessor, int Successor)> dependencies)
    {
        var preds = durations.Keys.ToDictionary(k => k, _ => new List<int>());
        foreach (var (p, s) in dependencies)
            if (preds.ContainsKey(p) && preds.ContainsKey(s)) preds[s].Add(p);

        var finish = new Dictionary<int, decimal>();
        var visiting = new HashSet<int>();

        decimal Visit(int id)
        {
            if (finish.TryGetValue(id, out var known)) return known;
            if (!visiting.Add(id)) return 0m; // defensive: a cycle would never reach here for validated routings
            var start = preds[id].Count == 0 ? 0m : preds[id].Max(Visit);
            visiting.Remove(id);
            return finish[id] = start + durations[id];
        }

        foreach (var id in durations.Keys) Visit(id);
        return finish;
    }

    /// <summary>
    /// Nodes of one longest path (the one that ends at the largest earliest finish), from first to last.
    /// Ties are broken by the lowest node id so the result is deterministic.
    /// </summary>
    public static List<int> CriticalPath(
        IReadOnlyDictionary<int, decimal> durations,
        IEnumerable<(int Predecessor, int Successor)> dependencies)
    {
        var deps = dependencies.Where(d => durations.ContainsKey(d.Predecessor) && durations.ContainsKey(d.Successor)).ToList();
        var finish = EarliestFinish(durations, deps);
        if (finish.Count == 0) return [];

        var path = new List<int>();
        var current = finish.OrderByDescending(f => f.Value).ThenBy(f => f.Key).First().Key;
        var guard = durations.Count;
        while (guard-- > 0)
        {
            path.Add(current);
            var start = finish[current] - durations[current];
            var previous = deps.Where(d => d.Successor == current && finish[d.Predecessor] == start)
                .Select(d => d.Predecessor).OrderBy(id => id).Cast<int?>().FirstOrDefault();
            if (previous is null) break;
            current = previous.Value;
        }
        path.Reverse();
        return path;
    }

    private sealed class CyclicBomException : Exception;
}
