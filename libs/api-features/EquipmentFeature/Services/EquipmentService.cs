using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;

namespace MicroMES.ApiFeatures.EquipmentFeature.Services;

public class EquipmentService(IUnitOfWork unitOfWork, EquipmentSegmentLinker linker)
{
    private IEquipmentRepository Repo => unitOfWork.GetRepository<IEquipmentRepository>();

    /// <summary>Machines; with a segment only those enabled for it; search matches code or name (case-insensitive).</summary>
    public async Task<EquipmentResult<List<EquipmentModel>>> GetAllAsync(bool includeInactive, string? search, int? processSegmentId)
    {
        var items = new List<Equipment>();
        await foreach (var e in Repo.FetchAsync(x => includeInactive || x.IsActive)) items.Add(e);

        var links = await linker.LoadLinksAsync();
        var segments = await linker.LoadSegmentsAsync();
        var term = search?.Trim();
        var models = items
            .Where(e => string.IsNullOrEmpty(term)
                        || e.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || e.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Where(e => processSegmentId is null
                        || links.Any(l => l.EquipmentId == e.Id && l.ProcessSegmentId == processSegmentId))
            .OrderBy(e => e.Code, StringComparer.Ordinal)
            .Select(e => ToModel(e, links, segments))
            .ToList();
        return EquipmentResult<List<EquipmentModel>>.Ok(models);
    }

    public async Task<EquipmentResult<EquipmentModel>> GetAsync(int id)
    {
        var equipment = await FindAsync(id);
        return equipment is null ? NotFound() : EquipmentResult<EquipmentModel>.Ok(await ReloadAsync(equipment));
    }

    public Task<EquipmentResult<EquipmentModel>> CreateAsync(CreateEquipmentRequest request)
        => InTransactionAsync(async () =>
    {
        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, null)) return CodeConflict();
        var segmentIds = Distinct(request.ProcessSegmentIds);
        if (await linker.ValidateSegmentsAsync(null, segmentIds) is { IsSuccess: false } invalid) return FromOutcome(invalid);

        var now = DateTime.UtcNow;
        var equipment = new Equipment
        {
            Code = code, Name = request.Name.Trim(), Description = request.Description,
            IsActive = true, CreatedAt = now, UpdatedAt = now
        };
        return await SaveAsync(equipment, segmentIds, isNew: true);
    });

    public Task<EquipmentResult<EquipmentModel>> UpdateAsync(int id, UpdateEquipmentRequest request)
        => InTransactionAsync(async () =>
    {
        var equipment = await FindAsync(id);
        if (equipment is null) return NotFound();

        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, id)) return CodeConflict();
        var segmentIds = Distinct(request.ProcessSegmentIds);
        if (await linker.ValidateSegmentsAsync(id, segmentIds) is { IsSuccess: false } invalid) return FromOutcome(invalid);

        equipment.Code = code;
        equipment.Name = request.Name.Trim();
        equipment.Description = request.Description;
        equipment.IsActive = request.IsActive!.Value;
        equipment.UpdatedAt = DateTime.UtcNow;
        return await SaveAsync(equipment, segmentIds, isNew: false);
    });

    /// <summary>Soft-delete: sets IsActive = false and keeps the links. Idempotent.</summary>
    public Task<EquipmentResult<EquipmentModel>> DeactivateAsync(int id)
        => InTransactionAsync(async () =>
    {
        var equipment = await FindAsync(id);
        if (equipment is null) return NotFound();
        if (equipment.IsActive)
        {
            equipment.IsActive = false;
            equipment.UpdatedAt = DateTime.UtcNow;
            var updated = await Repo.UpdateAsync(equipment);
            if (!updated.IsSuccessful) return Fail(updated.Errors);
        }
        return EquipmentResult<EquipmentModel>.Ok(await ReloadAsync(equipment));
    });

    private async Task<Equipment?> FindAsync(int id)
    {
        await foreach (var e in Repo.FetchAsync(x => x.Id == id)) return e;
        return null;
    }

    private async Task<EquipmentResult<EquipmentModel>> SaveAsync(Equipment equipment, List<int> segmentIds, bool isNew)
    {
        var saved = isNew ? await Repo.AddAsync(equipment) : await Repo.UpdateAsync(equipment);
        if (!saved.IsSuccessful || saved.Data is null) return Fail(saved.Errors);

        var linked = await linker.ReplaceSegmentsOfEquipmentAsync(equipment.Id, segmentIds);
        if (!linked.IsSuccess) return FromOutcome(linked);
        return EquipmentResult<EquipmentModel>.Ok(await ReloadAsync(equipment));
    }

    // None: the controller answers 500 and a recorded constraint violation turns it into 409.
    private static EquipmentResult<EquipmentModel> Fail(IReadOnlyCollection<string> errors)
        => EquipmentResult<EquipmentModel>.Failed(EquipmentFailure.None, "Impossibile salvare la macchina o postazione.", errors);

    private static EquipmentResult<EquipmentModel> FromOutcome(LinkOutcome o)
        => o.Invalid
            ? EquipmentResult<EquipmentModel>.Failed(EquipmentFailure.Invalid, o.Message!)
            : EquipmentResult<EquipmentModel>.Failed(EquipmentFailure.None, o.Message!, o.Errors);

    /// <summary>The transaction opens before any read (tracked entities read earlier would not be saved). Commit on success, rollback otherwise.</summary>
    private async Task<EquipmentResult<EquipmentModel>> InTransactionAsync(Func<Task<EquipmentResult<EquipmentModel>>> action)
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

    private async Task<EquipmentModel> ReloadAsync(Equipment equipment)
        => ToModel(equipment, await linker.LoadLinksAsync(), await linker.LoadSegmentsAsync());

    private static EquipmentModel ToModel(Equipment e, List<EquipmentProcessSegment> links, Dictionary<int, ProcessSegment> segments) => new()
    {
        Id = e.Id, Code = e.Code, Name = e.Name, Description = e.Description, IsActive = e.IsActive,
        ProcessSegments = links.Where(l => l.EquipmentId == e.Id && segments.ContainsKey(l.ProcessSegmentId))
            .Select(l => segments[l.ProcessSegmentId])
            .Select(s => new EquipmentSegmentModel { Id = s.Id, Code = s.Code, Name = s.Name, IsActive = s.IsActive })
            .OrderBy(s => s.Code, StringComparer.Ordinal).ToList(),
        CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt
    };

    private static List<int> Distinct(List<int>? ids) => ids?.Distinct().ToList() ?? [];

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private async Task<bool> CodeExistsAsync(string code, int? excludeId)
    {
        // Includes inactive machines: the unique index covers them too.
        await foreach (var _ in Repo.FetchAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId)))
            return true;
        return false;
    }

    private static EquipmentResult<EquipmentModel> NotFound()
        => EquipmentResult<EquipmentModel>.Failed(EquipmentFailure.NotFound, "Macchina o postazione non trovata.");

    private static EquipmentResult<EquipmentModel> CodeConflict()
        => EquipmentResult<EquipmentModel>.Failed(EquipmentFailure.Conflict, "Esiste già una macchina o postazione con questo codice.");
}
