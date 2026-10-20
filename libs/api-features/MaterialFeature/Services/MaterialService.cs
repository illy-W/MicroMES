using illyW.Framework.Core.ResultPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.MaterialFeature.Models;
using MicroMES.ApiFeatures.MaterialFeature.Repositories;

namespace MicroMES.ApiFeatures.MaterialFeature.Services;

public enum MaterialFailure
{
    None,
    NotFound,
    Conflict
}

/// <summary>Result carrying the failure kind so the controller can map it to 404/409.</summary>
public class MaterialResult<T> : Result<T>
{
    public MaterialFailure Failure { get; set; }
}

public class MaterialService(IMaterialRepository repository)
{
    public async Task<Result<List<MaterialModel>>> GetAllAsync(bool includeInactive)
    {
        var result = new Result<List<MaterialModel>>();
        var items = new List<Material>();
        await foreach (var m in repository.FetchAsync(x => includeInactive || x.IsActive))
            items.Add(m);

        result.Succeed(items.OrderBy(x => x.Code, StringComparer.Ordinal).Select(MaterialModel.FromEntity).ToList());
        return result;
    }

    public async Task<MaterialResult<MaterialModel>> GetAsync(int id)
    {
        var material = await repository.GetSingleAsync(id);
        if (material is null) return NotFound();

        var result = new MaterialResult<MaterialModel>();
        result.Succeed(MaterialModel.FromEntity(material));
        return result;
    }

    public async Task<MaterialResult<MaterialModel>> CreateAsync(CreateMaterialRequest request)
    {
        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, null)) return Conflict(code);

        var now = DateTime.UtcNow;
        var material = new Material
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description,
            Type = request.Type!.Value,
            UnitOfMeasure = request.UnitOfMeasure!.Value,
            Vendor = request.Vendor,
            VendorMaterialId = request.VendorMaterialId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var added = await repository.AddAsync(material);
        return FromSave(added.IsSuccessful, added.Errors, added.Data);
    }

    public async Task<MaterialResult<MaterialModel>> UpdateAsync(int id, UpdateMaterialRequest request)
    {
        var material = await repository.GetSingleAsync(id);
        if (material is null) return NotFound();

        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, id)) return Conflict(code);

        material.Code = code;
        material.Name = request.Name.Trim();
        material.Description = request.Description;
        material.Type = request.Type!.Value;
        material.UnitOfMeasure = request.UnitOfMeasure!.Value;
        material.Vendor = request.Vendor;
        material.VendorMaterialId = request.VendorMaterialId;
        material.IsActive = request.IsActive!.Value;
        material.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(material);
        return FromSave(updated.IsSuccessful, updated.Errors, updated.Data);
    }

    /// <summary>Soft-delete: sets IsActive = false. Idempotent.</summary>
    public async Task<MaterialResult<MaterialModel>> DeactivateAsync(int id)
    {
        var material = await repository.GetSingleAsync(id);
        if (material is null) return NotFound();

        if (!material.IsActive)
        {
            var already = new MaterialResult<MaterialModel>();
            already.Succeed(MaterialModel.FromEntity(material));
            return already;
        }

        material.IsActive = false;
        material.UpdatedAt = DateTime.UtcNow;
        var updated = await repository.UpdateAsync(material);
        return FromSave(updated.IsSuccessful, updated.Errors, updated.Data);
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private async Task<bool> CodeExistsAsync(string code, int? excludeId)
    {
        // Includes inactive materials: the unique index covers them too.
        await foreach (var _ in repository.FetchAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId)))
            return true;
        return false;
    }

    private static MaterialResult<MaterialModel> NotFound()
    {
        var result = new MaterialResult<MaterialModel> { Failure = MaterialFailure.NotFound };
        result.Fail("Material not found.");
        result.AddError("Material not found.");
        return result;
    }

    private static MaterialResult<MaterialModel> Conflict(string code)
    {
        var result = new MaterialResult<MaterialModel> { Failure = MaterialFailure.Conflict };
        result.Fail($"A material with code '{code}' already exists.");
        result.AddError($"A material with code '{code}' already exists.");
        return result;
    }

    private static MaterialResult<MaterialModel> FromSave(bool ok, IReadOnlyCollection<string> errors, Material? data)
    {
        var result = new MaterialResult<MaterialModel>();
        if (ok && data is not null)
        {
            result.Succeed(MaterialModel.FromEntity(data));
            return result;
        }

        result.Fail("Unable to save the material.");
        result.AddErrors(errors);
        return result;
    }
}
