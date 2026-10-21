using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;

namespace MicroMES.ApiFeatures.EquipmentFeature.Services;

/// <summary>A machine linked to a process segment, with the default flag of the link.</summary>
public sealed record SegmentMachine(Equipment Equipment, bool IsDefault);

/// <summary>
/// The single rule for choosing and validating the machine of a work order or session: it must exist, be active and be
/// linked to the work order's process segment; a segment without linked machines accepts none.
/// </summary>
public class EquipmentSelector(IUnitOfWork unitOfWork)
{
    private IEquipmentRepository EquipmentItems => unitOfWork.GetRepository<IEquipmentRepository>();
    private IEquipmentProcessSegmentRepository Links => unitOfWork.GetRepository<IEquipmentProcessSegmentRepository>();

    public const string NoMachinesMessage = "La fase non ha macchine collegate.";
    public const string ChooseMachineMessage = "Scegli la macchina.";

    /// <summary>Machines linked to a segment (active or not).</summary>
    public async Task<List<SegmentMachine>> MachinesOfSegmentAsync(int segmentId)
    {
        var links = new List<EquipmentProcessSegment>();
        await foreach (var l in Links.FetchAsync(x => x.ProcessSegmentId == segmentId)) links.Add(l);
        var ids = links.Select(l => l.EquipmentId).ToList();
        var machines = new Dictionary<int, Equipment>();
        await foreach (var e in EquipmentItems.FetchAsync(x => ids.Contains(x.Id))) machines[e.Id] = e;
        return links.Where(l => machines.ContainsKey(l.EquipmentId))
            .Select(l => new SegmentMachine(machines[l.EquipmentId], l.IsDefault))
            .OrderBy(m => m.Equipment.Code, StringComparer.Ordinal).ToList();
    }

    /// <summary>Machines of every segment (one read, for the release of an order).</summary>
    public async Task<Dictionary<int, List<SegmentMachine>>> MachinesBySegmentAsync()
    {
        var links = new List<EquipmentProcessSegment>();
        await foreach (var l in Links.FetchAsync(_ => true)) links.Add(l);
        var machines = new Dictionary<int, Equipment>();
        await foreach (var e in EquipmentItems.FetchAsync(_ => true)) machines[e.Id] = e;
        return links.Where(l => machines.ContainsKey(l.EquipmentId))
            .GroupBy(l => l.ProcessSegmentId)
            .ToDictionary(g => g.Key, g => g.Select(l => new SegmentMachine(machines[l.EquipmentId], l.IsDefault))
                .OrderBy(m => m.Equipment.Code, StringComparer.Ordinal).ToList());
    }

    /// <summary>Automatic choice at release: the active default machine, else the only active machine, else none.</summary>
    public static Equipment? Pick(IEnumerable<SegmentMachine>? machines)
    {
        var active = (machines ?? []).Where(m => m.Equipment.IsActive).ToList();
        if (active.FirstOrDefault(m => m.IsDefault) is { } preferred) return preferred.Equipment;
        return active.Count == 1 ? active[0].Equipment : null;
    }

    /// <summary>True when the segment has at least one active linked machine (so a session must name one).</summary>
    public static bool RequiresMachine(IEnumerable<SegmentMachine> machines) => machines.Any(m => m.Equipment.IsActive);

    /// <summary>True when the machine can be used now: linked to the segment and active.</summary>
    public static bool IsEnabled(IEnumerable<SegmentMachine> machines, int equipmentId)
        => machines.Any(m => m.Equipment.Id == equipmentId && m.Equipment.IsActive);

    /// <summary>Error message (Italian) when the machine cannot be used for the segment, null when it can.</summary>
    public static string? Check(IReadOnlyCollection<SegmentMachine> machines, int equipmentId)
    {
        if (machines.Count == 0) return NoMachinesMessage;
        var found = machines.FirstOrDefault(m => m.Equipment.Id == equipmentId);
        if (found is null) return "La macchina indicata non è collegata alla fase del lavoro.";
        return found.Equipment.IsActive ? null : $"La macchina {found.Equipment.Code} è disattivata.";
    }

    /// <summary>Validates a machine for a segment: the machine (when valid) or the 422 message.</summary>
    public async Task<(Equipment? Equipment, string? Error)> ValidateAsync(int segmentId, int equipmentId)
    {
        var machine = await EquipmentItems.GetSingleAsync(equipmentId);
        if (machine is null) return (null, "Macchina non trovata.");
        var error = Check(await MachinesOfSegmentAsync(segmentId), equipmentId);
        return error is null ? (machine, null) : (null, error);
    }
}
