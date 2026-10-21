using illyW.Framework.Core.RepositoryPattern;
using illyW.Framework.Core.ResultPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.EquipmentFeature.Services;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Models;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Repositories;

namespace MicroMES.ApiFeatures.ProcessSegmentFeature.Services;

public enum ProcessSegmentFailure
{
    None,
    NotFound,
    Conflict,
    Invalid
}

/// <summary>Result carrying the failure kind so the controller can map it to 404/409/422.</summary>
public class ProcessSegmentResult<T> : Result<T>
{
    public ProcessSegmentFailure Failure { get; set; }
}

public class ProcessSegmentService(IUnitOfWork unitOfWork, EquipmentSegmentLinker linker)
{
    private IProcessSegmentRepository Repo => unitOfWork.GetRepository<IProcessSegmentRepository>();

    public async Task<Result<List<ProcessSegmentModel>>> GetAllAsync(bool includeInactive)
    {
        var result = new Result<List<ProcessSegmentModel>>();
        var items = new List<ProcessSegment>();
        await foreach (var m in Repo.FetchAsync(x => includeInactive || x.IsActive))
            items.Add(m);

        var links = await linker.LoadLinksAsync();
        var equipment = await linker.LoadEquipmentAsync();
        result.Succeed(items.OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => ToModel(x, links, equipment)).ToList());
        return result;
    }

    public async Task<ProcessSegmentResult<ProcessSegmentModel>> GetAsync(int id)
    {
        var segment = await Repo.GetSingleAsync(id);
        if (segment is null) return NotFound();
        return Ok(await ReloadAsync(segment));
    }

    public Task<ProcessSegmentResult<ProcessSegmentModel>> CreateAsync(CreateProcessSegmentRequest request)
        => InTransactionAsync(async () =>
    {
        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, null)) return Conflict(code);
        var equipmentIds = request.EquipmentIds?.Distinct().ToList();
        if (equipmentIds is not null && await linker.ValidateEquipmentAsync(null, equipmentIds) is { IsSuccess: false } invalid)
            return FromOutcome(invalid);

        var now = DateTime.UtcNow;
        var segment = new ProcessSegment
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description,
            SetupMinutes = request.SetupMinutes,
            RunMinutesPerUnit = request.RunMinutesPerUnit,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var added = await Repo.AddAsync(segment);
        return await AfterSaveAsync(added.IsSuccessful, added.Errors, added.Data, equipmentIds);
    });

    public Task<ProcessSegmentResult<ProcessSegmentModel>> UpdateAsync(int id, UpdateProcessSegmentRequest request)
        => InTransactionAsync(async () =>
    {
        var segment = await Repo.GetSingleAsync(id);
        if (segment is null) return NotFound();

        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, id)) return Conflict(code);
        // Omitted (null) = keep the current links; present = replace the set.
        var equipmentIds = request.EquipmentIds?.Distinct().ToList();
        if (equipmentIds is not null && await linker.ValidateEquipmentAsync(id, equipmentIds) is { IsSuccess: false } invalid)
            return FromOutcome(invalid);

        segment.Code = code;
        segment.Name = request.Name.Trim();
        segment.Description = request.Description;
        segment.SetupMinutes = request.SetupMinutes;
        segment.RunMinutesPerUnit = request.RunMinutesPerUnit;
        segment.IsActive = request.IsActive!.Value;
        segment.UpdatedAt = DateTime.UtcNow;

        var updated = await Repo.UpdateAsync(segment);
        return await AfterSaveAsync(updated.IsSuccessful, updated.Errors, updated.Data, equipmentIds);
    });

    /// <summary>Soft-delete: sets IsActive = false and keeps the links. Idempotent.</summary>
    public Task<ProcessSegmentResult<ProcessSegmentModel>> DeactivateAsync(int id)
        => InTransactionAsync(async () =>
    {
        var segment = await Repo.GetSingleAsync(id);
        if (segment is null) return NotFound();

        if (!segment.IsActive) return Ok(await ReloadAsync(segment));

        segment.IsActive = false;
        segment.UpdatedAt = DateTime.UtcNow;
        var updated = await Repo.UpdateAsync(segment);
        return await AfterSaveAsync(updated.IsSuccessful, updated.Errors, updated.Data, null);
    });

    private async Task<ProcessSegmentResult<ProcessSegmentModel>> AfterSaveAsync(
        bool ok, IReadOnlyCollection<string> errors, ProcessSegment? data, List<int>? equipmentIds)
    {
        if (!ok || data is null) return SaveFailed(errors);
        if (equipmentIds is not null)
        {
            var linked = await linker.ReplaceEquipmentOfSegmentAsync(data.Id, equipmentIds);
            if (!linked.IsSuccess) return FromOutcome(linked);
        }
        return Ok(await ReloadAsync(data));
    }

    /// <summary>The transaction opens before any read (tracked entities read earlier would not be saved). Commit on success, rollback otherwise.</summary>
    private async Task<ProcessSegmentResult<ProcessSegmentModel>> InTransactionAsync(
        Func<Task<ProcessSegmentResult<ProcessSegmentModel>>> action)
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
            return SaveFailed([ex.Message]);
        }
    }

    private async Task<ProcessSegmentModel> ReloadAsync(ProcessSegment segment)
        => ToModel(segment, await linker.LoadLinksAsync(), await linker.LoadEquipmentAsync());

    private static ProcessSegmentModel ToModel(ProcessSegment s, List<EquipmentProcessSegment> links, Dictionary<int, Equipment> equipment)
        => ProcessSegmentModel.FromEntity(s, links.Where(l => l.ProcessSegmentId == s.Id && equipment.ContainsKey(l.EquipmentId))
            .Select(l => equipment[l.EquipmentId])
            .Select(e => new ProcessSegmentEquipmentModel { Id = e.Id, Code = e.Code, Name = e.Name, IsActive = e.IsActive })
            .OrderBy(e => e.Code, StringComparer.Ordinal).ToList());

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private async Task<bool> CodeExistsAsync(string code, int? excludeId)
    {
        // Includes inactive segments: the unique index covers them too.
        await foreach (var _ in Repo.FetchAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId)))
            return true;
        return false;
    }

    private static ProcessSegmentResult<ProcessSegmentModel> Ok(ProcessSegmentModel model)
    {
        var result = new ProcessSegmentResult<ProcessSegmentModel>();
        result.Succeed(model);
        return result;
    }

    private static ProcessSegmentResult<ProcessSegmentModel> NotFound()
    {
        var result = new ProcessSegmentResult<ProcessSegmentModel> { Failure = ProcessSegmentFailure.NotFound };
        result.Fail("Process segment not found.");
        result.AddError("Process segment not found.");
        return result;
    }

    private static ProcessSegmentResult<ProcessSegmentModel> Conflict(string code)
    {
        var result = new ProcessSegmentResult<ProcessSegmentModel> { Failure = ProcessSegmentFailure.Conflict };
        result.Fail($"A process segment with code '{code}' already exists.");
        result.AddError($"A process segment with code '{code}' already exists.");
        return result;
    }

    private static ProcessSegmentResult<ProcessSegmentModel> SaveFailed(IReadOnlyCollection<string> errors)
    {
        var result = new ProcessSegmentResult<ProcessSegmentModel>();
        result.Fail("Unable to save the process segment.");
        result.AddErrors(errors);
        return result;
    }

    private static ProcessSegmentResult<ProcessSegmentModel> FromOutcome(LinkOutcome o)
    {
        if (!o.Invalid) return SaveFailed(o.Errors);
        var result = new ProcessSegmentResult<ProcessSegmentModel> { Failure = ProcessSegmentFailure.Invalid };
        result.Fail(o.Message!);
        result.AddError(o.Message!);
        return result;
    }
}
