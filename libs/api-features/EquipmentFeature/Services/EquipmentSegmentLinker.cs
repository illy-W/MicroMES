using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Repositories;

namespace MicroMES.ApiFeatures.EquipmentFeature.Services;

/// <summary>Outcome of a link operation: null Message = success; Invalid = validation failure (422), otherwise a persistence failure.</summary>
public sealed record LinkOutcome(bool Invalid, string? Message, IReadOnlyCollection<string> Errors)
{
    public static readonly LinkOutcome Success = new(false, null, []);
    public bool IsSuccess => Message is null;
}

/// <summary>
/// Machine ↔ process segment links, shared by the equipment and the process segment services.
/// Must run inside the caller's unit-of-work transaction. A set replaces the previous one; ids already linked are kept
/// even if the item has been deactivated since (no silent loss of data), new links accept only existing active items.
/// </summary>
public class EquipmentSegmentLinker(IUnitOfWork unitOfWork)
{
    private IEquipmentRepository EquipmentItems => unitOfWork.GetRepository<IEquipmentRepository>();
    private IEquipmentProcessSegmentRepository Links => unitOfWork.GetRepository<IEquipmentProcessSegmentRepository>();
    private IProcessSegmentRepository Segments => unitOfWork.GetRepository<IProcessSegmentRepository>();

    public async Task<List<EquipmentProcessSegment>> LoadLinksAsync()
    {
        var all = new List<EquipmentProcessSegment>();
        await foreach (var l in Links.FetchAsync(_ => true)) all.Add(l);
        return all;
    }

    public async Task<Dictionary<int, Equipment>> LoadEquipmentAsync()
    {
        var map = new Dictionary<int, Equipment>();
        await foreach (var e in EquipmentItems.FetchAsync(_ => true)) map[e.Id] = e;
        return map;
    }

    public async Task<Dictionary<int, ProcessSegment>> LoadSegmentsAsync()
    {
        var map = new Dictionary<int, ProcessSegment>();
        await foreach (var s in Segments.FetchAsync(_ => true)) map[s.Id] = s;
        return map;
    }

    /// <summary>Validates the new set of segments of a machine (equipmentId null = new machine).</summary>
    public async Task<LinkOutcome> ValidateSegmentsAsync(int? equipmentId, List<int> segmentIds)
    {
        if (segmentIds.Count == 0) return LinkOutcome.Success;
        var linked = new HashSet<int>();
        if (equipmentId is not null)
            await foreach (var l in Links.FetchAsync(x => x.EquipmentId == equipmentId)) linked.Add(l.ProcessSegmentId);
        var found = new Dictionary<int, bool>();
        await foreach (var s in Segments.FetchAsync(x => segmentIds.Contains(x.Id))) found[s.Id] = s.IsActive;
        return Check(segmentIds, found, linked, "Fasi di processo");
    }

    /// <summary>Validates the new set of machines of a segment (segmentId null = new segment).</summary>
    public async Task<LinkOutcome> ValidateEquipmentAsync(int? segmentId, List<int> equipmentIds)
    {
        if (equipmentIds.Count == 0) return LinkOutcome.Success;
        var linked = new HashSet<int>();
        if (segmentId is not null)
            await foreach (var l in Links.FetchAsync(x => x.ProcessSegmentId == segmentId)) linked.Add(l.EquipmentId);
        var found = new Dictionary<int, bool>();
        await foreach (var e in EquipmentItems.FetchAsync(x => equipmentIds.Contains(x.Id))) found[e.Id] = e.IsActive;
        return Check(equipmentIds, found, linked, "Macchine e postazioni");
    }

    private static LinkOutcome Check(List<int> ids, Dictionary<int, bool> found, HashSet<int> alreadyLinked, string label)
    {
        var missing = ids.Where(i => !found.ContainsKey(i)).ToList();
        if (missing.Count > 0)
            return new LinkOutcome(true, $"{label} inesistenti: {string.Join(", ", missing)}.", []);
        var inactive = ids.Where(i => !found[i] && !alreadyLinked.Contains(i)).ToList();
        return inactive.Count == 0
            ? LinkOutcome.Success
            : new LinkOutcome(true, $"{label} disattivate, non collegabili: {string.Join(", ", inactive)}.", []);
    }

    /// <summary>Replaces the segments of a machine; the links that survive keep their default flag, new links are not default.</summary>
    public Task<LinkOutcome> ReplaceSegmentsOfEquipmentAsync(int equipmentId, List<int> segmentIds)
        => ReplaceAsync(x => x.EquipmentId == equipmentId, l => l.ProcessSegmentId, segmentIds,
            id => new EquipmentProcessSegment { EquipmentId = equipmentId, ProcessSegmentId = id }, applyDefault: false, null);

    /// <summary>Replaces the machines of a segment; the default becomes <paramref name="defaultEquipmentId"/> (null = none).</summary>
    public Task<LinkOutcome> ReplaceEquipmentOfSegmentAsync(int segmentId, List<int> equipmentIds, int? defaultEquipmentId = null)
        => ReplaceAsync(x => x.ProcessSegmentId == segmentId, l => l.EquipmentId, equipmentIds,
            id => new EquipmentProcessSegment { EquipmentId = id, ProcessSegmentId = segmentId, IsDefault = id == defaultEquipmentId },
            applyDefault: true, defaultEquipmentId);

    /// <summary>
    /// The default of a segment must be one of the new machines and, unless it already is the default, active (422 otherwise).
    /// </summary>
    public async Task<LinkOutcome> ValidateDefaultAsync(int? segmentId, List<int> equipmentIds, int? defaultEquipmentId)
    {
        if (defaultEquipmentId is not { } id) return LinkOutcome.Success;
        if (!equipmentIds.Contains(id))
            return new LinkOutcome(true, "La macchina predefinita deve essere una delle macchine collegate alla fase.", []);
        if (segmentId is not null)
            await foreach (var _ in Links.FetchAsync(x => x.ProcessSegmentId == segmentId && x.EquipmentId == id && x.IsDefault))
                return LinkOutcome.Success; // already the default: kept even if deactivated since
        var machine = await EquipmentItems.GetSingleAsync(id);
        return machine is { IsActive: true }
            ? LinkOutcome.Success
            : new LinkOutcome(true, "La macchina predefinita deve esistere ed essere attiva.", []);
    }

    private async Task<LinkOutcome> ReplaceAsync(System.Linq.Expressions.Expression<Func<EquipmentProcessSegment, bool>> owner,
        Func<EquipmentProcessSegment, int> otherSide, List<int> wanted, Func<int, EquipmentProcessSegment> create,
        bool applyDefault, int? defaultId)
    {
        var current = new List<EquipmentProcessSegment>();
        await foreach (var l in Links.FetchAsync(owner)) current.Add(l);
        foreach (var l in current.Where(l => !wanted.Contains(otherSide(l))))
        {
            var deleted = await Links.DeleteAsync(l);
            if (!deleted.IsSuccessful) return new LinkOutcome(false, "Impossibile salvare i collegamenti.", deleted.Errors);
        }
        if (applyDefault)
        {
            // The old default goes first: the filtered unique index allows one default per segment at any time.
            foreach (var l in current.Where(l => wanted.Contains(otherSide(l)) && l.IsDefault && otherSide(l) != defaultId))
            {
                l.IsDefault = false;
                var cleared = await Links.UpdateAsync(l);
                if (!cleared.IsSuccessful) return new LinkOutcome(false, "Impossibile salvare i collegamenti.", cleared.Errors);
            }
            foreach (var l in current.Where(l => !l.IsDefault && otherSide(l) == defaultId))
            {
                l.IsDefault = true;
                var set = await Links.UpdateAsync(l);
                if (!set.IsSuccessful) return new LinkOutcome(false, "Impossibile salvare i collegamenti.", set.Errors);
            }
        }
        foreach (var id in wanted.Where(id => current.All(l => otherSide(l) != id)))
        {
            var added = await Links.AddAsync(create(id));
            if (!added.IsSuccessful) return new LinkOutcome(false, "Impossibile salvare i collegamenti.", added.Errors);
        }
        return LinkOutcome.Success;
    }
}
