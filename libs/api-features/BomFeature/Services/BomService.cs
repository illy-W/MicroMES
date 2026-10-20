using illyW.Framework.Core.ResultPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.BomFeature.Models;
using MicroMES.ApiFeatures.BomFeature.Repositories;
using MicroMES.ApiFeatures.MaterialFeature.Repositories;

namespace MicroMES.ApiFeatures.BomFeature.Services;

public enum BomFailure
{
    None,
    NotFound,
    Conflict,
    Invalid
}

/// <summary>Result carrying the failure kind so the controller can map it to 404/409/422.</summary>
public class BomResult<T> : Result<T>
{
    public BomFailure Failure { get; set; }
}

public class BomService(IBomItemRepository repository, IMaterialRepository materials)
{
    private const int MaxDepth = 10;

    public async Task<BomResult<List<BomNodeModel>>> GetTreeAsync(int materialId)
    {
        if (await materials.GetSingleAsync(materialId) is null) return Failed<List<BomNodeModel>>(BomFailure.NotFound, "Material not found.");

        var roots = new List<BomNodeModel>();
        // Each frontier entry: parent material id -> children list to fill.
        var frontier = new Dictionary<int, List<List<BomNodeModel>>> { [materialId] = [roots] };

        for (var level = 0; level < MaxDepth && frontier.Count > 0; level++)
        {
            var parentIds = frontier.Keys.ToList();
            var items = new List<BomItem>();
            await foreach (var i in repository.FetchAsync(x => parentIds.Contains(x.ParentMaterialId))) items.Add(i);
            if (items.Count == 0) break;

            var componentIds = items.Select(i => i.ComponentMaterialId).Distinct().ToList();
            var comps = new Dictionary<int, Material>();
            await foreach (var m in materials.FetchAsync(x => componentIds.Contains(x.Id))) comps[m.Id] = m;

            var next = new Dictionary<int, List<List<BomNodeModel>>>();
            foreach (var item in items.OrderBy(i => comps[i.ComponentMaterialId].Code, StringComparer.Ordinal))
            {
                var c = comps[item.ComponentMaterialId];
                foreach (var target in frontier[item.ParentMaterialId])
                {
                    var node = new BomNodeModel
                    {
                        Id = item.Id,
                        ComponentMaterialId = c.Id,
                        Code = c.Code,
                        Name = c.Name,
                        Type = c.Type,
                        UnitOfMeasure = c.UnitOfMeasure,
                        Quantity = item.Quantity,
                        IsActive = c.IsActive,
                        Children = []
                    };
                    target.Add(node);
                    if (!next.TryGetValue(c.Id, out var list)) next[c.Id] = list = [];
                    list.Add(node.Children);
                }
            }
            frontier = next;
        }

        var result = new BomResult<List<BomNodeModel>>();
        result.Succeed(roots);
        return result;
    }

    public async Task<BomResult<BomItemModel>> AddAsync(int materialId, CreateBomItemRequest request)
    {
        var parent = await materials.GetSingleAsync(materialId);
        if (parent is null) return Failed<BomItemModel>(BomFailure.NotFound, "Material not found.");
        if (parent.Type is not (MaterialType.Intermediate or MaterialType.FinishedGood))
            return Failed<BomItemModel>(BomFailure.Invalid, "Only Intermediate or FinishedGood materials can have a BOM.");

        var componentId = request.ComponentMaterialId!.Value;
        if (componentId == materialId)
            return Failed<BomItemModel>(BomFailure.Invalid, "A material cannot be a component of itself.");

        var component = await materials.GetSingleAsync(componentId);
        if (component is null) return Failed<BomItemModel>(BomFailure.NotFound, "Component material not found.");
        if (!component.IsActive) return Failed<BomItemModel>(BomFailure.Invalid, "Component material is not active.");

        await foreach (var _ in repository.FetchAsync(x => x.ParentMaterialId == materialId && x.ComponentMaterialId == componentId))
            return Failed<BomItemModel>(BomFailure.Conflict, "The component is already in the BOM.");

        var now = DateTime.UtcNow;
        var added = await repository.AddAsync(new BomItem
        {
            ParentMaterialId = materialId,
            ComponentMaterialId = componentId,
            Quantity = request.Quantity!.Value,
            CreatedAt = now,
            UpdatedAt = now
        });
        return FromSave(added.IsSuccessful, added.Errors, added.Data);
    }

    public async Task<BomResult<BomItemModel>> UpdateAsync(int id, UpdateBomItemRequest request)
    {
        var item = await repository.GetSingleAsync(id);
        if (item is null) return Failed<BomItemModel>(BomFailure.NotFound, "BOM item not found.");

        item.Quantity = request.Quantity!.Value;
        item.UpdatedAt = DateTime.UtcNow;
        var updated = await repository.UpdateAsync(item);
        return FromSave(updated.IsSuccessful, updated.Errors, updated.Data);
    }

    /// <summary>Physical delete: a BOM row is a relationship, not a master record.</summary>
    public async Task<BomResult<BomItemModel>> DeleteAsync(int id)
    {
        var item = await repository.GetSingleAsync(id);
        if (item is null) return Failed<BomItemModel>(BomFailure.NotFound, "BOM item not found.");

        var deleted = await repository.DeleteAsync(item);
        return FromSave(deleted.IsSuccessful, deleted.Errors, item);
    }

    private static BomResult<T> Failed<T>(BomFailure failure, string message)
    {
        var result = new BomResult<T> { Failure = failure };
        result.Fail(message);
        result.AddError(message);
        return result;
    }

    private static BomResult<BomItemModel> FromSave(bool ok, IReadOnlyCollection<string> errors, BomItem? data)
    {
        var result = new BomResult<BomItemModel>();
        if (ok && data is not null)
        {
            result.Succeed(BomItemModel.FromEntity(data));
            return result;
        }

        result.Fail("Unable to save the BOM item.");
        result.AddErrors(errors);
        return result;
    }
}
