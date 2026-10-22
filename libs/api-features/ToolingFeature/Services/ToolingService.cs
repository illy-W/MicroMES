using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;
using MicroMES.ApiFeatures.MaterialFeature.Repositories;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Repositories;
using MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;
using MicroMES.ApiFeatures.RoutingFeature.Repositories;
using MicroMES.ApiFeatures.ToolingFeature.Models;
using MicroMES.ApiFeatures.ToolingFeature.Repositories;

namespace MicroMES.ApiFeatures.ToolingFeature.Services;

public class ToolingService(IUnitOfWork unitOfWork)
{
    private IToolingRepository Repo => unitOfWork.GetRepository<IToolingRepository>();
    private IToolingEquipmentRepository Links => unitOfWork.GetRepository<IToolingEquipmentRepository>();
    private IRoutingStepToolingRepository StepLinks => unitOfWork.GetRepository<IRoutingStepToolingRepository>();
    private IRoutingStepRepository RoutingSteps => unitOfWork.GetRepository<IRoutingStepRepository>();
    private IMaterialRepository Materials => unitOfWork.GetRepository<IMaterialRepository>();
    private IProcessSegmentRepository Segments => unitOfWork.GetRepository<IProcessSegmentRepository>();
    private IWorkOrderToolingRepository WorkOrderLinks => unitOfWork.GetRepository<IWorkOrderToolingRepository>();
    private IWorkOrderRepository WorkOrders => unitOfWork.GetRepository<IWorkOrderRepository>();
    private IProductionOrderRepository Orders => unitOfWork.GetRepository<IProductionOrderRepository>();
    private IEquipmentRepository EquipmentItems => unitOfWork.GetRepository<IEquipmentRepository>();

    /// <summary>Toolings; q matches code or name (case-insensitive); isActive null = all.</summary>
    public async Task<ToolingResult<List<ToolingModel>>> GetAllAsync(string? q, bool? isActive)
    {
        var items = new List<Tooling>();
        await foreach (var t in Repo.FetchAsync(x => isActive == null || x.IsActive == isActive)) items.Add(t);
        var counts = new Dictionary<int, int>();
        await foreach (var l in Links.FetchAsync(_ => true)) counts[l.ToolingId] = counts.GetValueOrDefault(l.ToolingId) + 1;
        var term = q?.Trim();
        var models = items
            .Where(t => string.IsNullOrEmpty(term)
                        || t.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || t.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Code, StringComparer.Ordinal)
            .Select(t => ToModel(t, counts.GetValueOrDefault(t.Id))).ToList();
        return ToolingResult<List<ToolingModel>>.Ok(models);
    }

    public async Task<ToolingResult<ToolingDetailModel>> GetAsync(int id)
    {
        var tooling = await FindAsync(id);
        return tooling is null ? NotFound() : ToolingResult<ToolingDetailModel>.Ok(await DetailAsync(tooling));
    }

    public Task<ToolingResult<ToolingDetailModel>> CreateAsync(CreateToolingRequest request)
        => InTransactionAsync(async () =>
    {
        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, null)) return CodeConflict(code);
        if (Validate(request.Quantity, request.SetupMinutes) is { } invalid) return invalid;
        var equipmentIds = Distinct(request.EquipmentIds);
        if (await CheckEquipmentAsync(null, equipmentIds) is { } badEquipment) return badEquipment;

        var now = DateTime.UtcNow;
        var tooling = new Tooling
        {
            Code = code, Name = request.Name.Trim(), Description = request.Description,
            Quantity = request.Quantity ?? 1, SetupMinutes = request.SetupMinutes ?? 0,
            IsActive = true, CreatedAt = now, UpdatedAt = now
        };
        return await SaveAsync(tooling, equipmentIds, isNew: true);
    });

    public Task<ToolingResult<ToolingDetailModel>> UpdateAsync(int id, UpdateToolingRequest request)
        => InTransactionAsync(async () =>
    {
        var tooling = await FindAsync(id);
        if (tooling is null) return NotFound();
        var code = NormalizeCode(request.Code);
        if (await CodeExistsAsync(code, id)) return CodeConflict(code);
        if (Validate(request.Quantity, request.SetupMinutes) is { } invalid) return invalid;
        var equipmentIds = Distinct(request.EquipmentIds);
        if (await CheckEquipmentAsync(id, equipmentIds) is { } badEquipment) return badEquipment;

        tooling.Code = code;
        tooling.Name = request.Name.Trim();
        tooling.Description = request.Description;
        if (request.Quantity is { } quantity) tooling.Quantity = quantity;
        if (request.SetupMinutes is { } minutes) tooling.SetupMinutes = minutes;
        tooling.IsActive = request.IsActive!.Value;
        tooling.UpdatedAt = DateTime.UtcNow;
        return await SaveAsync(tooling, equipmentIds, isNew: false);
    });

    /// <summary>Soft-delete: sets IsActive = false and keeps the links. Idempotent.</summary>
    public Task<ToolingResult<ToolingDetailModel>> DeactivateAsync(int id) => SetActiveAsync(id, false);

    /// <summary>Sets IsActive = true. Idempotent.</summary>
    public Task<ToolingResult<ToolingDetailModel>> ReactivateAsync(int id) => SetActiveAsync(id, true);

    private Task<ToolingResult<ToolingDetailModel>> SetActiveAsync(int id, bool active)
        => InTransactionAsync(async () =>
    {
        var tooling = await FindAsync(id);
        if (tooling is null) return NotFound();
        if (tooling.IsActive != active)
        {
            tooling.IsActive = active;
            tooling.UpdatedAt = DateTime.UtcNow;
            var updated = await Repo.UpdateAsync(tooling);
            if (!updated.IsSuccessful) return Fail(updated.Errors);
        }
        return ToolingResult<ToolingDetailModel>.Ok(await DetailAsync(tooling));
    });

    private async Task<Tooling?> FindAsync(int id)
    {
        await foreach (var t in Repo.FetchAsync(x => x.Id == id)) return t;
        return null;
    }

    private async Task<ToolingResult<ToolingDetailModel>> SaveAsync(Tooling tooling, List<int> equipmentIds, bool isNew)
    {
        var saved = isNew ? await Repo.AddAsync(tooling) : await Repo.UpdateAsync(tooling);
        if (!saved.IsSuccessful || saved.Data is null) return Fail(saved.Errors);

        var current = new List<ToolingEquipment>();
        await foreach (var l in Links.FetchAsync(x => x.ToolingId == tooling.Id)) current.Add(l);
        foreach (var l in current.Where(l => !equipmentIds.Contains(l.EquipmentId)))
        {
            var deleted = await Links.DeleteAsync(l);
            if (!deleted.IsSuccessful) return Fail(deleted.Errors);
        }
        foreach (var id in equipmentIds.Where(id => current.All(l => l.EquipmentId != id)))
        {
            var added = await Links.AddAsync(new ToolingEquipment { ToolingId = tooling.Id, EquipmentId = id });
            if (!added.IsSuccessful) return Fail(added.Errors);
        }
        return ToolingResult<ToolingDetailModel>.Ok(await DetailAsync(tooling));
    }

    /// <summary>422 when a machine does not exist or is inactive (an already linked one is kept even if deactivated since).</summary>
    private async Task<ToolingResult<ToolingDetailModel>?> CheckEquipmentAsync(int? toolingId, List<int> equipmentIds)
    {
        if (equipmentIds.Count == 0) return null;
        var linked = new HashSet<int>();
        if (toolingId is not null)
            await foreach (var l in Links.FetchAsync(x => x.ToolingId == toolingId)) linked.Add(l.EquipmentId);
        var found = new Dictionary<int, Equipment>();
        await foreach (var e in EquipmentItems.FetchAsync(x => equipmentIds.Contains(x.Id))) found[e.Id] = e;

        var missing = equipmentIds.Where(i => !found.ContainsKey(i)).ToList();
        if (missing.Count > 0)
            return Invalid($"Macchine e postazioni inesistenti: {string.Join(", ", missing)}.");
        var inactive = equipmentIds.Where(i => !found[i].IsActive && !linked.Contains(i)).Select(i => found[i].Code).ToList();
        return inactive.Count == 0 ? null
            : Invalid($"Macchine e postazioni disattivate, non collegabili: {string.Join(", ", inactive)}.");
    }

    private static ToolingResult<ToolingDetailModel>? Validate(int? quantity, int? setupMinutes)
    {
        if (quantity is < 1) return Invalid("La quantità deve essere almeno 1.");
        if (setupMinutes is < 0) return Invalid("I minuti di setup non possono essere negativi.");
        return null;
    }

    private async Task<ToolingDetailModel> DetailAsync(Tooling t)
    {
        var equipmentIds = new List<int>();
        await foreach (var l in Links.FetchAsync(x => x.ToolingId == t.Id)) equipmentIds.Add(l.EquipmentId);
        var equipment = new List<ToolingEquipmentModel>();
        await foreach (var e in EquipmentItems.FetchAsync(x => equipmentIds.Contains(x.Id)))
            equipment.Add(new ToolingEquipmentModel { Id = e.Id, Code = e.Code, Name = e.Name, IsActive = e.IsActive });
        equipment = equipment.OrderBy(e => e.Code, StringComparer.Ordinal).ToList();
        var b = ToModel(t, equipment.Count);
        return new ToolingDetailModel
        {
            Id = b.Id, Code = b.Code, Name = b.Name, Description = b.Description, Quantity = b.Quantity,
            SetupMinutes = b.SetupMinutes, IsActive = b.IsActive, EquipmentCount = b.EquipmentCount,
            CreatedAt = b.CreatedAt, UpdatedAt = b.UpdatedAt,
            Equipment = equipment, RoutingSteps = await RoutingStepsOfAsync(t.Id), WorkOrders = await OpenWorkOrdersOfAsync(t.Id)
        };
    }

    /// <summary>Routing steps requiring the tooling: code '{material code} / op {n}', name = process segment name.</summary>
    private async Task<List<ToolingUsageModel>> RoutingStepsOfAsync(int toolingId)
    {
        var stepIds = new List<int>();
        await foreach (var l in StepLinks.FetchAsync(x => x.ToolingId == toolingId)) stepIds.Add(l.RoutingStepId);
        var steps = new List<RoutingStep>();
        await foreach (var st in RoutingSteps.FetchAsync(x => stepIds.Contains(x.Id))) steps.Add(st);
        var materialIds = steps.Select(x => x.MaterialId).Distinct().ToList();
        var materials = new Dictionary<int, Material>();
        await foreach (var m in Materials.FetchAsync(x => materialIds.Contains(x.Id))) materials[m.Id] = m;
        var segmentIds = steps.Select(x => x.ProcessSegmentId).Distinct().ToList();
        var segments = new Dictionary<int, ProcessSegment>();
        await foreach (var sg in Segments.FetchAsync(x => segmentIds.Contains(x.Id))) segments[sg.Id] = sg;
        return steps.OrderBy(x => materials[x.MaterialId].Code, StringComparer.Ordinal).ThenBy(x => x.OperationNumber)
            .Select(x => new ToolingUsageModel
            {
                Id = x.Id, Code = $"{materials[x.MaterialId].Code} / op {x.OperationNumber}", Name = segments[x.ProcessSegmentId].Name
            }).ToList();
    }

    /// <summary>Open (not Ended/Aborted) work orders requiring the tooling: code '{order}/{op}', name = process segment name.</summary>
    private async Task<List<ToolingUsageModel>> OpenWorkOrdersOfAsync(int toolingId)
    {
        var woIds = new List<int>();
        await foreach (var l in WorkOrderLinks.FetchAsync(x => x.ToolingId == toolingId)) woIds.Add(l.WorkOrderId);
        var items = new List<WorkOrder>();
        await foreach (var w in WorkOrders.FetchAsync(x => woIds.Contains(x.Id)
                         && x.Status != WorkOrderStatus.Ended && x.Status != WorkOrderStatus.Aborted)) items.Add(w);
        var orderIds = items.Select(w => w.ProductionOrderId).Distinct().ToList();
        var orders = new Dictionary<int, ProductionOrder>();
        await foreach (var o in Orders.FetchAsync(x => orderIds.Contains(x.Id))) orders[o.Id] = o;
        return items.OrderBy(w => orders[w.ProductionOrderId].Code, StringComparer.Ordinal).ThenBy(w => w.OperationNumber)
            .Select(w => new ToolingUsageModel { Id = w.Id, Code = $"{orders[w.ProductionOrderId].Code}/{w.OperationNumber}", Name = w.ProcessSegmentName }).ToList();
    }

    private static ToolingModel ToModel(Tooling t, int equipmentCount) => new()
    {
        Id = t.Id, Code = t.Code, Name = t.Name, Description = t.Description, Quantity = t.Quantity,
        SetupMinutes = t.SetupMinutes, IsActive = t.IsActive, EquipmentCount = equipmentCount,
        CreatedAt = t.CreatedAt, UpdatedAt = t.UpdatedAt
    };

    private async Task<ToolingResult<ToolingDetailModel>> InTransactionAsync(Func<Task<ToolingResult<ToolingDetailModel>>> action)
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

    private static List<int> Distinct(List<int>? ids) => ids?.Distinct().ToList() ?? [];

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private async Task<bool> CodeExistsAsync(string code, int? excludeId)
    {
        // Includes inactive toolings: the unique index covers them too.
        await foreach (var _ in Repo.FetchAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId)))
            return true;
        return false;
    }

    // None: the controller answers 500 and a recorded constraint violation turns it into 409.
    private static ToolingResult<ToolingDetailModel> Fail(IReadOnlyCollection<string> errors)
        => ToolingResult<ToolingDetailModel>.Failed(ToolingFailure.None, "Impossibile salvare l'attrezzatura.", errors);

    private static ToolingResult<ToolingDetailModel> Invalid(string message)
        => ToolingResult<ToolingDetailModel>.Failed(ToolingFailure.Invalid, message);

    private static ToolingResult<ToolingDetailModel> NotFound()
        => ToolingResult<ToolingDetailModel>.Failed(ToolingFailure.NotFound, "Attrezzatura non trovata.");

    private static ToolingResult<ToolingDetailModel> CodeConflict(string code)
        => ToolingResult<ToolingDetailModel>.Failed(ToolingFailure.Conflict, $"Esiste già un'attrezzatura con codice {code}.");
}
