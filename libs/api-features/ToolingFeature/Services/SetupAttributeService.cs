using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ToolingFeature.Models;
using MicroMES.ApiFeatures.ToolingFeature.Repositories;

namespace MicroMES.ApiFeatures.ToolingFeature.Services;

public class SetupAttributeService(IUnitOfWork unitOfWork)
{
    private ISetupAttributeRepository Repo => unitOfWork.GetRepository<ISetupAttributeRepository>();

    public async Task<ToolingResult<List<SetupAttributeModel>>> GetAllAsync(string? q, bool? isActive)
    {
        var items = new List<SetupAttribute>();
        await foreach (var a in Repo.FetchAsync(x => isActive == null || x.IsActive == isActive)) items.Add(a);
        var term = q?.Trim();
        var models = items
            .Where(a => string.IsNullOrEmpty(term)
                        || a.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || a.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Code, StringComparer.Ordinal).Select(ToModel).ToList();
        return ToolingResult<List<SetupAttributeModel>>.Ok(models);
    }

    public async Task<ToolingResult<SetupAttributeModel>> GetAsync(int id)
    {
        var item = await FindAsync(id);
        return item is null ? NotFound() : ToolingResult<SetupAttributeModel>.Ok(ToModel(item));
    }

    public Task<ToolingResult<SetupAttributeModel>> CreateAsync(CreateSetupAttributeRequest request)
        => InTransactionAsync(async () =>
    {
        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, null)) return CodeConflict(code);
        var minutes = request.ChangeMinutes ?? 0;
        if (minutes < 0) return NegativeMinutes();

        var now = DateTime.UtcNow;
        var item = new SetupAttribute
            { Code = code, Name = request.Name.Trim(), ChangeMinutes = minutes, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var saved = await Repo.AddAsync(item);
        return saved.IsSuccessful ? ToolingResult<SetupAttributeModel>.Ok(ToModel(item)) : Fail(saved.Errors);
    });

    public Task<ToolingResult<SetupAttributeModel>> UpdateAsync(int id, UpdateSetupAttributeRequest request)
        => InTransactionAsync(async () =>
    {
        var item = await FindAsync(id);
        if (item is null) return NotFound();
        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, id)) return CodeConflict(code);
        if (request.ChangeMinutes is < 0) return NegativeMinutes();

        item.Code = code;
        item.Name = request.Name.Trim();
        if (request.ChangeMinutes is { } minutes) item.ChangeMinutes = minutes;
        item.IsActive = request.IsActive!.Value;
        item.UpdatedAt = DateTime.UtcNow;
        var saved = await Repo.UpdateAsync(item);
        return saved.IsSuccessful ? ToolingResult<SetupAttributeModel>.Ok(ToModel(item)) : Fail(saved.Errors);
    });

    /// <summary>Soft-delete: sets IsActive = false. Idempotent.</summary>
    public Task<ToolingResult<SetupAttributeModel>> DeactivateAsync(int id) => SetActiveAsync(id, false);

    /// <summary>Sets IsActive = true. Idempotent.</summary>
    public Task<ToolingResult<SetupAttributeModel>> ReactivateAsync(int id) => SetActiveAsync(id, true);

    private Task<ToolingResult<SetupAttributeModel>> SetActiveAsync(int id, bool active)
        => InTransactionAsync(async () =>
    {
        var item = await FindAsync(id);
        if (item is null) return NotFound();
        if (item.IsActive != active)
        {
            item.IsActive = active;
            item.UpdatedAt = DateTime.UtcNow;
            var updated = await Repo.UpdateAsync(item);
            if (!updated.IsSuccessful) return Fail(updated.Errors);
        }
        return ToolingResult<SetupAttributeModel>.Ok(ToModel(item));
    });

    private async Task<SetupAttribute?> FindAsync(int id)
    {
        await foreach (var a in Repo.FetchAsync(x => x.Id == id)) return a;
        return null;
    }

    private async Task<bool> CodeExistsAsync(string code, int? excludeId)
    {
        // Includes inactive items: the unique index covers them too.
        await foreach (var _ in Repo.FetchAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId)))
            return true;
        return false;
    }

    private async Task<ToolingResult<SetupAttributeModel>> InTransactionAsync(Func<Task<ToolingResult<SetupAttributeModel>>> action)
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

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    // None: the controller answers 500 and a recorded constraint violation turns it into 409.
    private static ToolingResult<SetupAttributeModel> Fail(IReadOnlyCollection<string> errors)
        => ToolingResult<SetupAttributeModel>.Failed(ToolingFailure.None, "Impossibile salvare l'attributo di setup.", errors);

    private static ToolingResult<SetupAttributeModel> NotFound()
        => ToolingResult<SetupAttributeModel>.Failed(ToolingFailure.NotFound, "Attributo di setup non trovato.");

    private static ToolingResult<SetupAttributeModel> CodeConflict(string code)
        => ToolingResult<SetupAttributeModel>.Failed(ToolingFailure.Conflict, $"Esiste già un attributo di setup con codice {code}.");

    private static ToolingResult<SetupAttributeModel> NegativeMinutes()
        => ToolingResult<SetupAttributeModel>.Failed(ToolingFailure.Invalid, "I minuti di cambio non possono essere negativi.");

    private static SetupAttributeModel ToModel(SetupAttribute a) => new()
    {
        Id = a.Id, Code = a.Code, Name = a.Name, ChangeMinutes = a.ChangeMinutes, IsActive = a.IsActive,
        CreatedAt = a.CreatedAt, UpdatedAt = a.UpdatedAt
    };
}
