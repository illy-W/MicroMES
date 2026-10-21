using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Repositories;
using MicroMES.ApiFeatures.ScrapFeature.Models;
using MicroMES.ApiFeatures.ScrapFeature.Repositories;

namespace MicroMES.ApiFeatures.ScrapFeature.Services;

public class ScrapReasonService(IUnitOfWork unitOfWork)
{
    private IScrapReasonRepository Reasons => unitOfWork.GetRepository<IScrapReasonRepository>();
    private IScrapReasonProcessSegmentRepository Links => unitOfWork.GetRepository<IScrapReasonProcessSegmentRepository>();
    private IProcessSegmentRepository Segments => unitOfWork.GetRepository<IProcessSegmentRepository>();

    /// <summary>Reasons; with a segment, only those valid for it (linked to it or linked to no segment).</summary>
    public async Task<ScrapResult<List<ScrapReasonModel>>> GetAllAsync(bool includeInactive, int? processSegmentId)
    {
        var items = new List<ScrapReason>();
        await foreach (var r in Reasons.FetchAsync(x => includeInactive || x.IsActive)) items.Add(r);

        var links = await LoadLinksAsync();
        var segments = await LoadSegmentsAsync();
        var models = items
            .Where(r => processSegmentId is null
                        || !links.TryGetValue(r.Id, out var l) || l.Count == 0 || l.Contains(processSegmentId.Value))
            .OrderBy(r => r.Code, StringComparer.Ordinal)
            .Select(r => ToModel(r, links, segments))
            .ToList();
        return ScrapResult<List<ScrapReasonModel>>.Ok(models);
    }

    public async Task<ScrapResult<ScrapReasonModel>> GetAsync(int id)
    {
        var reason = await FindAsync(id);
        return reason is null ? NotFound() : ScrapResult<ScrapReasonModel>.Ok(await ReloadAsync(reason));
    }

    public Task<ScrapResult<ScrapReasonModel>> CreateAsync(CreateScrapReasonRequest request)
        => InTransactionAsync(async () =>
    {
        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, null)) return CodeConflict();
        var segmentIds = Distinct(request.ProcessSegmentIds);
        if (await CheckSegmentsAsync(segmentIds) is { } invalid) return invalid;

        var now = DateTime.UtcNow;
        var reason = new ScrapReason
        {
            Code = code, Name = request.Name.Trim(), Description = request.Description,
            IsActive = true, CreatedAt = now, UpdatedAt = now
        };
        return await SaveAsync(reason, segmentIds, isNew: true);
    });

    public Task<ScrapResult<ScrapReasonModel>> UpdateAsync(int id, UpdateScrapReasonRequest request)
        => InTransactionAsync(async () =>
    {
        var reason = await FindAsync(id);
        if (reason is null) return NotFound();

        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, id)) return CodeConflict();
        var segmentIds = Distinct(request.ProcessSegmentIds);
        if (await CheckSegmentsAsync(segmentIds) is { } invalid) return invalid;

        reason.Code = code;
        reason.Name = request.Name.Trim();
        reason.Description = request.Description;
        reason.IsActive = request.IsActive!.Value;
        reason.UpdatedAt = DateTime.UtcNow;
        return await SaveAsync(reason, segmentIds, isNew: false);
    });

    /// <summary>Soft-delete: sets IsActive = false. Idempotent.</summary>
    public Task<ScrapResult<ScrapReasonModel>> DeactivateAsync(int id)
        => InTransactionAsync(async () =>
    {
        var reason = await FindAsync(id);
        if (reason is null) return NotFound();
        if (reason.IsActive)
        {
            reason.IsActive = false;
            reason.UpdatedAt = DateTime.UtcNow;
            var updated = await Reasons.UpdateAsync(reason);
            if (!updated.IsSuccessful)
                return ScrapResult<ScrapReasonModel>.Failed(ScrapFailure.None, "Impossibile salvare il motivo di scarto.", updated.Errors);
        }
        return ScrapResult<ScrapReasonModel>.Ok(await ReloadAsync(reason));
    });

    private async Task<ScrapReason?> FindAsync(int id)
    {
        await foreach (var r in Reasons.FetchAsync(x => x.Id == id)) return r;
        return null;
    }

    // ---- helpers ------------------------------------------------------------------------------------

    private async Task<ScrapResult<ScrapReasonModel>> SaveAsync(ScrapReason reason, List<int> segmentIds, bool isNew)
    {
        var saved = isNew ? await Reasons.AddAsync(reason) : await Reasons.UpdateAsync(reason);
        if (!saved.IsSuccessful || saved.Data is null) return Fail(saved.Errors);

        var current = new List<ScrapReasonProcessSegment>();
        await foreach (var l in Links.FetchAsync(x => x.ScrapReasonId == reason.Id)) current.Add(l);
        foreach (var l in current.Where(l => !segmentIds.Contains(l.ProcessSegmentId)))
            if (!(await Links.DeleteAsync(l)).IsSuccessful) return Fail([]);
        foreach (var segmentId in segmentIds.Where(sid => current.All(l => l.ProcessSegmentId != sid)))
        {
            var added = await Links.AddAsync(new ScrapReasonProcessSegment { ScrapReasonId = reason.Id, ProcessSegmentId = segmentId });
            if (!added.IsSuccessful) return Fail(added.Errors);
        }
        return ScrapResult<ScrapReasonModel>.Ok(await ReloadAsync(reason));
    }

    // None: the controller answers 500 and a recorded constraint violation turns it into 409.
    private static ScrapResult<ScrapReasonModel> Fail(IReadOnlyCollection<string> errors)
        => ScrapResult<ScrapReasonModel>.Failed(ScrapFailure.None, "Impossibile salvare il motivo di scarto.", errors);

    /// <summary>
    /// The transaction opens before any read: tracked entities read earlier would not be saved by the unit of work.
    /// Commit on success, rollback otherwise.
    /// </summary>
    private async Task<ScrapResult<ScrapReasonModel>> InTransactionAsync(Func<Task<ScrapResult<ScrapReasonModel>>> action)
    {
        unitOfWork.BeginTransaction();
        try
        {
            var result = await action();
            if (result.IsSuccessful) unitOfWork.Commit();
            else unitOfWork.Rollback();
            return result;
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return Fail([ex.Message]);
        }
    }

    private async Task<ScrapReasonModel> ReloadAsync(ScrapReason reason)
        => ToModel(reason, await LoadLinksAsync(), await LoadSegmentsAsync());

    private async Task<Dictionary<int, HashSet<int>>> LoadLinksAsync()
    {
        var map = new Dictionary<int, HashSet<int>>();
        await foreach (var l in Links.FetchAsync(_ => true))
        {
            if (!map.TryGetValue(l.ScrapReasonId, out var set)) map[l.ScrapReasonId] = set = [];
            set.Add(l.ProcessSegmentId);
        }
        return map;
    }

    private async Task<Dictionary<int, ProcessSegment>> LoadSegmentsAsync()
    {
        var map = new Dictionary<int, ProcessSegment>();
        await foreach (var s in Segments.FetchAsync(_ => true)) map[s.Id] = s;
        return map;
    }

    private static ScrapReasonModel ToModel(ScrapReason r, Dictionary<int, HashSet<int>> links, Dictionary<int, ProcessSegment> segments) => new()
    {
        Id = r.Id, Code = r.Code, Name = r.Name, Description = r.Description, IsActive = r.IsActive,
        ProcessSegments = (links.TryGetValue(r.Id, out var set) ? set : [])
            .Where(segments.ContainsKey)
            .Select(id => new ScrapReasonSegmentModel { Id = id, Code = segments[id].Code, Name = segments[id].Name })
            .OrderBy(s => s.Code, StringComparer.Ordinal).ToList(),
        CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt
    };

    private static List<int> Distinct(List<int>? ids) => ids?.Distinct().ToList() ?? [];

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private async Task<bool> CodeExistsAsync(string code, int? excludeId)
    {
        // Includes inactive reasons: the unique index covers them too.
        await foreach (var _ in Reasons.FetchAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId)))
            return true;
        return false;
    }

    private async Task<ScrapResult<ScrapReasonModel>?> CheckSegmentsAsync(List<int> ids)
    {
        if (ids.Count == 0) return null;
        var found = new HashSet<int>();
        await foreach (var s in Segments.FetchAsync(x => ids.Contains(x.Id))) found.Add(s.Id);
        var missing = ids.Where(i => !found.Contains(i)).ToList();
        return missing.Count == 0
            ? null
            : ScrapResult<ScrapReasonModel>.Failed(ScrapFailure.Invalid,
                $"Fasi di processo inesistenti: {string.Join(", ", missing)}.");
    }

    private static ScrapResult<ScrapReasonModel> NotFound()
        => ScrapResult<ScrapReasonModel>.Failed(ScrapFailure.NotFound, "Motivo di scarto non trovato.");

    private static ScrapResult<ScrapReasonModel> CodeConflict()
        => ScrapResult<ScrapReasonModel>.Failed(ScrapFailure.Conflict, "Esiste già un motivo di scarto con questo codice.");
}
