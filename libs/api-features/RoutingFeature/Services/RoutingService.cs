using illyW.Framework.Core.RepositoryPattern;
using illyW.Framework.Core.ResultPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.BomFeature.Repositories;
using MicroMES.ApiFeatures.MaterialFeature.Repositories;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Repositories;
using MicroMES.ApiFeatures.RoutingFeature.Models;
using MicroMES.ApiFeatures.RoutingFeature.Repositories;
using MicroMES.ApiFeatures.ToolingFeature.Repositories;
using System.Text.RegularExpressions;

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
    private IBomItemRepository BomItems => unitOfWork.GetRepository<IBomItemRepository>();
    private IMaterialRepository Materials => unitOfWork.GetRepository<IMaterialRepository>();
    private IRoutingStepToolingRepository StepTooling => unitOfWork.GetRepository<IRoutingStepToolingRepository>();
    private IRoutingStepSetupValueRepository StepValues => unitOfWork.GetRepository<IRoutingStepSetupValueRepository>();
    private IToolingRepository ToolingItems => unitOfWork.GetRepository<IToolingRepository>();
    private ISetupAttributeRepository Attributes => unitOfWork.GetRepository<ISetupAttributeRepository>();
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

        // Toolings and setup attributes: must exist (404); must be active when newly added (422).
        if (await CheckToolingAndAttributesAsync(request, existing) is { } linkError) return linkError;

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
        var unassigned = new List<RoutingUnassignedComponentModel>();
        try
        {
            unitOfWork.BeginTransaction();
            var saveError = await PersistAsync(materialId, request, existing, edges, unassigned);
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

        var model = await LoadModelAsync(materialId);
        model.UnassignedComponents = unassigned;
        return Succeeded(model);
    }

    private async Task<string?> PersistAsync(
        int materialId, UpdateRoutingRequest request, Dictionary<int, RoutingStep> existing, HashSet<(string, string)> edges,
        List<RoutingUnassignedComponentModel> unassigned)
    {
        var keptIds = request.Steps.Where(s => s.Id is not null).Select(s => s.Id!.Value).ToHashSet();
        var removedIds = existing.Keys.Where(id => !keptIds.Contains(id)).ToHashSet();

        // 0. Components linked to a removed step go back to automatic (same transaction); the caller is told which.
        if (removedIds.Count > 0)
        {
            var removedList = removedIds.ToList();
            var linked = new List<BomItem>();
            await foreach (var b in BomItems.FetchAsync(x => x.ConsumedInStepId != null && removedList.Contains(x.ConsumedInStepId.Value))) linked.Add(b);
            var componentIds = linked.Select(b => b.ComponentMaterialId).Distinct().ToList();
            var components = new Dictionary<int, Material>();
            await foreach (var m in Materials.FetchAsync(x => componentIds.Contains(x.Id))) components[m.Id] = m;
            var segIds = removedIds.Select(id => existing[id].ProcessSegmentId).Distinct().ToList();
            var segNames = new Dictionary<int, string>();
            await foreach (var seg in Segments.FetchAsync(x => segIds.Contains(x.Id))) segNames[seg.Id] = seg.Name;

            foreach (var b in linked.OrderBy(b => existing[b.ConsumedInStepId!.Value].OperationNumber).ThenBy(b => components[b.ComponentMaterialId].Code, StringComparer.Ordinal))
            {
                var step = existing[b.ConsumedInStepId!.Value];
                var component = components[b.ComponentMaterialId];
                unassigned.Add(new RoutingUnassignedComponentModel
                {
                    BomItemId = b.Id, MaterialId = component.Id, MaterialCode = component.Code, MaterialName = component.Name,
                    PreviousStepId = step.Id, PreviousOperationNumber = step.OperationNumber,
                    PreviousProcessSegmentName = segNames[step.ProcessSegmentId]
                });
                b.ConsumedInStepId = null;
                b.UpdatedAt = DateTime.UtcNow;
                if (!(await BomItems.UpdateAsync(b)).IsSuccessful) return "Unable to reset a BOM item link.";
            }
        }

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

        // 4b. Toolings and setup values of every step.
        foreach (var s in request.Steps)
            if (await SyncLinksAsync(keyToId[s.Key], s) is { } linkError) return linkError;

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

    /// <summary>Trim and collapse inner whitespace; empty = null.</summary>
    public static string? NormalizeValue(string? value)
    {
        var v = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        return v.Length == 0 ? null : v;
    }

    private async Task<RoutingResult<RoutingModel>?> CheckToolingAndAttributesAsync(
        UpdateRoutingRequest request, Dictionary<int, RoutingStep> existing)
    {
        var toolingIds = request.Steps.SelectMany(s => s.ToolingIds ?? []).Distinct().ToList();
        var attributeIds = request.Steps.SelectMany(s => s.SetupValues ?? []).Select(v => v.AttributeId!.Value).Distinct().ToList();
        if (toolingIds.Count == 0 && attributeIds.Count == 0) return null;

        var toolings = new Dictionary<int, Tooling>();
        await foreach (var t in ToolingItems.FetchAsync(x => toolingIds.Contains(x.Id))) toolings[t.Id] = t;
        var attributes = new Dictionary<int, SetupAttribute>();
        await foreach (var a in Attributes.FetchAsync(x => attributeIds.Contains(x.Id))) attributes[a.Id] = a;
        var missingTooling = toolingIds.FirstOrDefault(i => !toolings.ContainsKey(i));
        if (missingTooling != 0) return Failed(RoutingFailure.NotFound, $"Attrezzatura {missingTooling} non trovata.");
        var missingAttribute = attributeIds.FirstOrDefault(i => !attributes.ContainsKey(i));
        if (missingAttribute != 0) return Failed(RoutingFailure.NotFound, $"Attributo di cambio {missingAttribute} non trovato.");

        var existingIds = existing.Keys.ToList();
        var linkedTooling = new HashSet<(int, int)>();
        await foreach (var l in StepTooling.FetchAsync(x => existingIds.Contains(x.RoutingStepId))) linkedTooling.Add((l.RoutingStepId, l.ToolingId));
        var linkedValues = new HashSet<(int, int)>();
        await foreach (var l in StepValues.FetchAsync(x => existingIds.Contains(x.RoutingStepId))) linkedValues.Add((l.RoutingStepId, l.SetupAttributeId));

        var errors = new List<string>();
        foreach (var s in request.Steps)
        {
            foreach (var id in (s.ToolingIds ?? []).Distinct())
            {
                var t = toolings[id];
                var already = s.Id is { } sid && linkedTooling.Contains((sid, id));
                if (!t.IsActive && !already) errors.Add($"L'attrezzatura {t.Code} è disattivata: non si può aggiungere al passo {s.OperationNumber}.");
            }
            foreach (var v in s.SetupValues ?? [])
            {
                if (NormalizeValue(v.Value) is null) continue;
                var a = attributes[v.AttributeId!.Value];
                var already = s.Id is { } sid && linkedValues.Contains((sid, a.Id));
                if (!a.IsActive && !already) errors.Add($"L'attributo di cambio {a.Code} è disattivato: non si può aggiungere al passo {s.OperationNumber}.");
            }
        }
        return errors.Count == 0 ? null : Failed(RoutingFailure.Invalid, errors.Distinct());
    }

    /// <summary>Aligns tooling and setup values of a step with the request (null list = unchanged).</summary>
    private async Task<string?> SyncLinksAsync(int stepId, UpdateRoutingStepRequest s)
    {
        if (s.ToolingIds is { } wanted)
        {
            var ids = wanted.Distinct().ToList();
            var current = new List<RoutingStepTooling>();
            await foreach (var l in StepTooling.FetchAsync(x => x.RoutingStepId == stepId)) current.Add(l);
            foreach (var l in current.Where(l => !ids.Contains(l.ToolingId)))
                if (!(await StepTooling.DeleteAsync(l)).IsSuccessful) return "Unable to remove a step tooling.";
            foreach (var id in ids.Where(id => current.All(l => l.ToolingId != id)))
                if (!(await StepTooling.AddAsync(new RoutingStepTooling { RoutingStepId = stepId, ToolingId = id })).IsSuccessful)
                    return "Unable to add a step tooling.";
        }
        if (s.SetupValues is { } values)
        {
            var wantedValues = new Dictionary<int, string?>();
            foreach (var v in values) wantedValues[v.AttributeId!.Value] = NormalizeValue(v.Value); // last wins
            var current = new List<RoutingStepSetupValue>();
            await foreach (var l in StepValues.FetchAsync(x => x.RoutingStepId == stepId)) current.Add(l);
            foreach (var l in current)
            {
                if (!wantedValues.TryGetValue(l.SetupAttributeId, out var nv)) continue;
                if (nv is null)
                {
                    if (!(await StepValues.DeleteAsync(l)).IsSuccessful) return "Unable to remove a setup value.";
                }
                else if (l.Value != nv)
                {
                    l.Value = nv;
                    if (!(await StepValues.UpdateAsync(l)).IsSuccessful) return "Unable to update a setup value.";
                }
            }
            foreach (var (attr, nv) in wantedValues.Where(kv => kv.Value is not null && current.All(l => l.SetupAttributeId != kv.Key)))
                if (!(await StepValues.AddAsync(new RoutingStepSetupValue { RoutingStepId = stepId, SetupAttributeId = attr, Value = nv! })).IsSuccessful)
                    return "Unable to add a setup value.";
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

        var bomRows = new List<BomItem>();
        await foreach (var b in BomItems.FetchAsync(x => x.ParentMaterialId == materialId)) bomRows.Add(b);
        var compIds = bomRows.Select(b => b.ComponentMaterialId).ToList();
        var compMap = new Dictionary<int, Material>();
        await foreach (var m in Materials.FetchAsync(x => compIds.Contains(x.Id))) compMap[m.Id] = m;
        var toolLinks = new List<RoutingStepTooling>();
        await foreach (var l in StepTooling.FetchAsync(x => stepIds.Contains(x.RoutingStepId))) toolLinks.Add(l);
        var valueLinks = new List<RoutingStepSetupValue>();
        await foreach (var l in StepValues.FetchAsync(x => stepIds.Contains(x.RoutingStepId))) valueLinks.Add(l);
        var toolIds = toolLinks.Select(l => l.ToolingId).Distinct().ToList();
        var toolMap = new Dictionary<int, Tooling>();
        await foreach (var t in ToolingItems.FetchAsync(x => toolIds.Contains(x.Id))) toolMap[t.Id] = t;
        var attrIds = valueLinks.Select(l => l.SetupAttributeId).Distinct().ToList();
        var attrMap = new Dictionary<int, SetupAttribute>();
        await foreach (var a in Attributes.FetchAsync(x => attrIds.Contains(x.Id))) attrMap[a.Id] = a;
        var firstStartId = RoutingStepRules.FirstStartStep(stepList, depList)?.Id;
        int? EffectiveStepId(BomItem b) => b.ConsumedInStepId ?? firstStartId;

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
                    PositionY = s.PositionY,
                    Tooling = toolLinks.Where(l => l.RoutingStepId == s.Id).Select(l => toolMap[l.ToolingId])
                        .OrderBy(t => t.Code, StringComparer.Ordinal)
                        .Select(t => new RoutingStepToolingModel { Id = t.Id, Code = t.Code, Name = t.Name, IsActive = t.IsActive }).ToList(),
                    SetupValues = valueLinks.Where(l => l.RoutingStepId == s.Id)
                        .Select(l => new RoutingStepSetupValueModel { AttributeId = l.SetupAttributeId, Code = attrMap[l.SetupAttributeId].Code, Name = attrMap[l.SetupAttributeId].Name, Value = l.Value })
                        .OrderBy(v => v.Code, StringComparer.Ordinal).ToList(),
                    ConsumedComponents = bomRows.Where(b => EffectiveStepId(b) == s.Id)
                        .OrderBy(b => compMap[b.ComponentMaterialId].Code, StringComparer.Ordinal)
                        .Select(b => new RoutingStepComponentModel
                        {
                            BomItemId = b.Id, MaterialId = b.ComponentMaterialId,
                            MaterialCode = compMap[b.ComponentMaterialId].Code, MaterialName = compMap[b.ComponentMaterialId].Name,
                            UnitOfMeasure = compMap[b.ComponentMaterialId].UnitOfMeasure, Quantity = b.Quantity,
                            IsAutomatic = b.ConsumedInStepId is null
                        }).ToList()
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
