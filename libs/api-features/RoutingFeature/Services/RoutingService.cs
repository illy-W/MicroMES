using illyW.Framework.Core.RepositoryPattern;
using illyW.Framework.Core.ResultPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.MaterialFeature.Repositories;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Repositories;
using MicroMES.ApiFeatures.RoutingFeature.Models;
using MicroMES.ApiFeatures.RoutingFeature.Repositories;

namespace MicroMES.ApiFeatures.RoutingFeature.Services;

public enum RoutingFailure
{
    None,
    NotFound,
    Invalid
}

/// <summary>Result carrying the failure kind so the controller can map it to 404/422.</summary>
public class RoutingResult<T> : Result<T>
{
    public RoutingFailure Failure { get; set; }
}

public class RoutingService(
    IUnitOfWork unitOfWork)
{
    // Repositories always come from the unit of work (same scope, same DbContext, same transaction).
    private IRoutingStepRepository Steps => unitOfWork.GetRepository<IRoutingStepRepository>();
    private IRoutingStepDependencyRepository Dependencies => unitOfWork.GetRepository<IRoutingStepDependencyRepository>();
    private IMaterialRepository Materials => unitOfWork.GetRepository<IMaterialRepository>();
    private IProcessSegmentRepository Segments => unitOfWork.GetRepository<IProcessSegmentRepository>();

    public async Task<RoutingResult<RoutingModel>> GetAsync(int materialId)
    {
        if (await Materials.GetSingleAsync(materialId) is null) return Failed(RoutingFailure.NotFound, "Material not found.");
        return Succeeded(await LoadModelAsync(materialId));
    }

    public async Task<RoutingResult<RoutingModel>> ReplaceAsync(int materialId, UpdateRoutingRequest request)
    {
        var material = await Materials.GetSingleAsync(materialId);
        if (material is null) return Failed(RoutingFailure.NotFound, "Material not found.");
        if (material.Type is not (MaterialType.Intermediate or MaterialType.FinishedGood))
            return Failed(RoutingFailure.Invalid, "Only Intermediate or FinishedGood materials can have a routing.");

        var existing = new Dictionary<int, RoutingStep>();
        await foreach (var s in Steps.FetchAsync(x => x.MaterialId == materialId)) existing[s.Id] = s;

        // Shape validation (keys, ids, operation numbers).
        var errors = new List<string>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<int>();
        var ops = new HashSet<int>();
        foreach (var s in request.Steps)
        {
            if (!keys.Add(s.Key)) errors.Add($"Duplicate step key '{s.Key}'.");
            if (s.Id is { } id)
            {
                if (!existing.ContainsKey(id)) return Failed(RoutingFailure.NotFound, $"Routing step {id} not found for this material.");
                if (!ids.Add(id)) errors.Add($"Step id {id} appears more than once.");
            }
            if (s.OperationNumber!.Value <= 0) errors.Add($"Operation number must be greater than 0 (step '{s.Key}').");
            else if (!ops.Add(s.OperationNumber.Value)) errors.Add($"Duplicate operation number {s.OperationNumber}.");
        }
        if (errors.Count > 0) return Failed(RoutingFailure.Invalid, errors);

        // Process segments: must exist; must be active when new or changed.
        var segmentIds = request.Steps.Select(s => s.ProcessSegmentId!.Value).Distinct().ToList();
        var segmentMap = new Dictionary<int, ProcessSegment>();
        await foreach (var seg in Segments.FetchAsync(x => segmentIds.Contains(x.Id))) segmentMap[seg.Id] = seg;
        foreach (var s in request.Steps)
        {
            if (!segmentMap.TryGetValue(s.ProcessSegmentId!.Value, out var seg))
                return Failed(RoutingFailure.NotFound, $"Process segment {s.ProcessSegmentId} not found.");
            var isNewOrChanged = s.Id is null || existing[s.Id.Value].ProcessSegmentId != seg.Id;
            if (isNewOrChanged && !seg.IsActive)
                errors.Add($"Process segment '{seg.Code}' is not active.");
        }
        if (errors.Count > 0) return Failed(RoutingFailure.Invalid, errors);

        // Dependencies and graph rules.
        var edges = new HashSet<(string, string)>();
        foreach (var d in request.Dependencies)
        {
            if (!keys.Contains(d.PredecessorKey)) errors.Add($"Unknown step key '{d.PredecessorKey}' in dependency.");
            if (!keys.Contains(d.SuccessorKey)) errors.Add($"Unknown step key '{d.SuccessorKey}' in dependency.");
            if (d.PredecessorKey == d.SuccessorKey) errors.Add($"A step cannot depend on itself ('{d.PredecessorKey}').");
            else if (!edges.Add((d.PredecessorKey, d.SuccessorKey)))
                errors.Add($"Duplicate dependency '{d.PredecessorKey}' -> '{d.SuccessorKey}'.");
        }
        if (errors.Count == 0) errors.AddRange(ValidateGraph(keys, edges));
        if (errors.Count > 0) return Failed(RoutingFailure.Invalid, errors);

        // Persist everything in one transaction.
        try
        {
            unitOfWork.BeginTransaction();
            var saveError = await PersistAsync(materialId, request, existing, edges);
            if (saveError is not null)
            {
                unitOfWork.Rollback();
                return Failed(RoutingFailure.None, saveError);
            }
            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return Failed(RoutingFailure.None, $"Unable to save the routing: {ex.Message}");
        }

        return Succeeded(await LoadModelAsync(materialId));
    }

    private async Task<string?> PersistAsync(
        int materialId, UpdateRoutingRequest request, Dictionary<int, RoutingStep> existing, HashSet<(string, string)> edges)
    {
        var keptIds = request.Steps.Where(s => s.Id is not null).Select(s => s.Id!.Value).ToHashSet();
        var removedIds = existing.Keys.Where(id => !keptIds.Contains(id)).ToHashSet();

        // 1. Remove all existing dependencies of this routing; they are recreated from the request.
        var existingIds = existing.Keys.ToList();
        var oldDeps = new List<RoutingStepDependency>();
        await foreach (var d in Dependencies.FetchAsync(x => existingIds.Contains(x.PredecessorStepId) || existingIds.Contains(x.SuccessorStepId)))
            oldDeps.Add(d);
        foreach (var d in oldDeps)
            if (!(await Dependencies.DeleteAsync(d)).IsSuccessful) return "Unable to remove a dependency.";

        // 2. Remove missing steps.
        foreach (var id in removedIds)
            if (!(await Steps.DeleteAsync(existing[id])).IsSuccessful) return "Unable to remove a step.";

        // 3. Park the operation numbers that change on unique negative values (allows swaps).
        var now = DateTime.UtcNow;
        var changing = request.Steps.Where(s => s.Id is not null && existing[s.Id.Value].OperationNumber != s.OperationNumber).ToList();
        foreach (var s in changing)
        {
            var e = existing[s.Id!.Value];
            e.OperationNumber = -e.Id;
            if (!(await Steps.UpdateAsync(e)).IsSuccessful) return "Unable to update a step.";
        }

        // 4. Update existing steps, create new ones; remember the final id of each key.
        var keyToId = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var s in request.Steps.Where(s => s.Id is not null))
        {
            var e = existing[s.Id!.Value];
            Apply(e, s);
            e.UpdatedAt = now;
            if (!(await Steps.UpdateAsync(e)).IsSuccessful) return "Unable to update a step.";
            keyToId[s.Key] = e.Id;
        }
        foreach (var s in request.Steps.Where(s => s.Id is null))
        {
            var e = new RoutingStep { MaterialId = materialId, CreatedAt = now, UpdatedAt = now };
            Apply(e, s);
            var added = await Steps.AddAsync(e);
            if (!added.IsSuccessful || added.Data is null) return "Unable to create a step.";
            keyToId[s.Key] = added.Data.Id;
        }

        // 5. Create the dependencies.
        foreach (var (pred, succ) in edges)
        {
            var added = await Dependencies.AddAsync(new RoutingStepDependency
            {
                PredecessorStepId = keyToId[pred],
                SuccessorStepId = keyToId[succ]
            });
            if (!added.IsSuccessful) return "Unable to create a dependency.";
        }

        return null;
    }

    private static void Apply(RoutingStep e, UpdateRoutingStepRequest s)
    {
        e.ProcessSegmentId = s.ProcessSegmentId!.Value;
        e.OperationNumber = s.OperationNumber!.Value;
        e.SetupMinutes = s.SetupMinutes;
        e.RunMinutesPerUnit = s.RunMinutesPerUnit;
        e.Notes = s.Notes;
        e.PositionX = s.PositionX;
        e.PositionY = s.PositionY;
    }

    /// <summary>Acyclic, weakly connected, exactly one final step (no successors). Empty graph is valid.</summary>
    private static List<string> ValidateGraph(HashSet<string> nodes, HashSet<(string, string)> edges)
    {
        var errors = new List<string>();
        if (nodes.Count == 0) return errors;

        // Acyclic: Kahn's algorithm.
        var indegree = nodes.ToDictionary(n => n, _ => 0, StringComparer.Ordinal);
        var next = nodes.ToDictionary(n => n, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var (p, s) in edges) { indegree[s]++; next[p].Add(s); }
        var queue = new Queue<string>(indegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var visited = 0;
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            visited++;
            foreach (var m in next[n]) if (--indegree[m] == 0) queue.Enqueue(m);
        }
        if (visited != nodes.Count) errors.Add("The routing contains a cycle.");

        // Weakly connected: union-find.
        var parent = nodes.ToDictionary(n => n, n => n, StringComparer.Ordinal);
        string Find(string x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        foreach (var (p, s) in edges) parent[Find(p)] = Find(s);
        if (nodes.Select(Find).Distinct().Count() > 1) errors.Add("The routing must be a single connected graph.");

        // Exactly one final step.
        var finals = nodes.Count(n => next[n].Count == 0);
        if (finals == 0) { if (errors.Count == 0) errors.Add("The routing must have exactly one final step."); }
        else if (finals > 1) errors.Add("The routing must have exactly one final step (found " + finals + ").");

        return errors;
    }

    private async Task<RoutingModel> LoadModelAsync(int materialId)
    {
        var stepList = new List<RoutingStep>();
        await foreach (var s in Steps.FetchAsync(x => x.MaterialId == materialId)) stepList.Add(s);

        var stepIds = stepList.Select(s => s.Id).ToList();
        var segmentIds = stepList.Select(s => s.ProcessSegmentId).Distinct().ToList();
        var segmentMap = new Dictionary<int, ProcessSegment>();
        await foreach (var seg in Segments.FetchAsync(x => segmentIds.Contains(x.Id))) segmentMap[seg.Id] = seg;

        var depList = new List<RoutingStepDependency>();
        await foreach (var d in Dependencies.FetchAsync(x => stepIds.Contains(x.PredecessorStepId))) depList.Add(d);

        return new RoutingModel
        {
            Steps = stepList.OrderBy(s => s.OperationNumber).Select(s =>
            {
                var seg = segmentMap[s.ProcessSegmentId];
                return new RoutingStepModel
                {
                    Id = s.Id,
                    ProcessSegmentId = seg.Id,
                    ProcessSegmentCode = seg.Code,
                    ProcessSegmentName = seg.Name,
                    StandardSetupMinutes = seg.SetupMinutes,
                    StandardRunMinutesPerUnit = seg.RunMinutesPerUnit,
                    OperationNumber = s.OperationNumber,
                    SetupMinutes = s.SetupMinutes,
                    RunMinutesPerUnit = s.RunMinutesPerUnit,
                    EffectiveSetupMinutes = s.SetupMinutes ?? seg.SetupMinutes,
                    EffectiveRunMinutesPerUnit = s.RunMinutesPerUnit ?? seg.RunMinutesPerUnit,
                    Notes = s.Notes,
                    PositionX = s.PositionX,
                    PositionY = s.PositionY
                };
            }).ToList(),
            Dependencies = depList.OrderBy(d => d.Id).Select(d => new RoutingDependencyModel
            {
                Id = d.Id,
                PredecessorStepId = d.PredecessorStepId,
                SuccessorStepId = d.SuccessorStepId
            }).ToList()
        };
    }

    private static RoutingResult<RoutingModel> Succeeded(RoutingModel model)
    {
        var result = new RoutingResult<RoutingModel>();
        result.Succeed(model);
        return result;
    }

    private static RoutingResult<RoutingModel> Failed(RoutingFailure failure, string message)
        => Failed(failure, [message]);

    private static RoutingResult<RoutingModel> Failed(RoutingFailure failure, IEnumerable<string> messages)
    {
        var list = messages.ToList();
        var result = new RoutingResult<RoutingModel> { Failure = failure };
        result.Fail(list.First());
        foreach (var m in list) result.AddError(m);
        return result;
    }
}
