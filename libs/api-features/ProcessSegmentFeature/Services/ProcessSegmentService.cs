using illyW.Framework.Core.ResultPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Models;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Repositories;

namespace MicroMES.ApiFeatures.ProcessSegmentFeature.Services;

public enum ProcessSegmentFailure
{
    None,
    NotFound,
    Conflict
}

/// <summary>Result carrying the failure kind so the controller can map it to 404/409.</summary>
public class ProcessSegmentResult<T> : Result<T>
{
    public ProcessSegmentFailure Failure { get; set; }
}

public class ProcessSegmentService(IProcessSegmentRepository repository)
{
    public async Task<Result<List<ProcessSegmentModel>>> GetAllAsync(bool includeInactive)
    {
        var result = new Result<List<ProcessSegmentModel>>();
        var items = new List<ProcessSegment>();
        await foreach (var m in repository.FetchAsync(x => includeInactive || x.IsActive))
            items.Add(m);

        result.Succeed(items.OrderBy(x => x.Code, StringComparer.Ordinal).Select(ProcessSegmentModel.FromEntity).ToList());
        return result;
    }

    public async Task<ProcessSegmentResult<ProcessSegmentModel>> GetAsync(int id)
    {
        var segment = await repository.GetSingleAsync(id);
        if (segment is null) return NotFound();

        var result = new ProcessSegmentResult<ProcessSegmentModel>();
        result.Succeed(ProcessSegmentModel.FromEntity(segment));
        return result;
    }

    public async Task<ProcessSegmentResult<ProcessSegmentModel>> CreateAsync(CreateProcessSegmentRequest request)
    {
        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, null)) return Conflict(code);

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

        var added = await repository.AddAsync(segment);
        return FromSave(added.IsSuccessful, added.Errors, added.Data);
    }

    public async Task<ProcessSegmentResult<ProcessSegmentModel>> UpdateAsync(int id, UpdateProcessSegmentRequest request)
    {
        var segment = await repository.GetSingleAsync(id);
        if (segment is null) return NotFound();

        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, id)) return Conflict(code);

        segment.Code = code;
        segment.Name = request.Name.Trim();
        segment.Description = request.Description;
        segment.SetupMinutes = request.SetupMinutes;
        segment.RunMinutesPerUnit = request.RunMinutesPerUnit;
        segment.IsActive = request.IsActive!.Value;
        segment.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(segment);
        return FromSave(updated.IsSuccessful, updated.Errors, updated.Data);
    }

    /// <summary>Soft-delete: sets IsActive = false. Idempotent.</summary>
    public async Task<ProcessSegmentResult<ProcessSegmentModel>> DeactivateAsync(int id)
    {
        var segment = await repository.GetSingleAsync(id);
        if (segment is null) return NotFound();

        if (!segment.IsActive)
        {
            var already = new ProcessSegmentResult<ProcessSegmentModel>();
            already.Succeed(ProcessSegmentModel.FromEntity(segment));
            return already;
        }

        segment.IsActive = false;
        segment.UpdatedAt = DateTime.UtcNow;
        var updated = await repository.UpdateAsync(segment);
        return FromSave(updated.IsSuccessful, updated.Errors, updated.Data);
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private async Task<bool> CodeExistsAsync(string code, int? excludeId)
    {
        // Includes inactive segments: the unique index covers them too.
        await foreach (var _ in repository.FetchAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId)))
            return true;
        return false;
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

    private static ProcessSegmentResult<ProcessSegmentModel> FromSave(bool ok, IReadOnlyCollection<string> errors, ProcessSegment? data)
    {
        var result = new ProcessSegmentResult<ProcessSegmentModel>();
        if (ok && data is not null)
        {
            result.Succeed(ProcessSegmentModel.FromEntity(data));
            return result;
        }

        result.Fail("Unable to save the process segment.");
        result.AddErrors(errors);
        return result;
    }
}
