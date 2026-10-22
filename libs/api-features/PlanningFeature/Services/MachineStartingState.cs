using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;

namespace MicroMES.ApiFeatures.PlanningFeature.Services;

/// <summary>
/// Starting state of a machine (REQ-4) from the work orders actually executed on it: the Running one (latest session start),
/// else the Ended one with the latest real end. "On it" = a session of the work order used the machine (session equipment
/// snapshot). Tooling and values come from the snapshots of the work order. Null = no history (unknown: full setup).
/// </summary>
public static class MachineStartingState
{
    /// <summary>What the work order needs for its setup (snapshots of the release + the full setup of the step).</summary>
    public static SetupRequirements RequirementsOf(ProductionData data, WorkOrder wo)
        => new(
            data.ToolingLines.Where(l => l.WorkOrderId == wo.Id).OrderBy(l => l.ToolingCode, StringComparer.Ordinal)
                .Select(l => new JobTooling(l.ToolingId, l.ToolingCode, l.ToolingSetupMinutes)).ToList(),
            data.SetupValueLines.Where(l => l.WorkOrderId == wo.Id).OrderBy(l => l.AttributeCode, StringComparer.Ordinal)
                .Select(l => new JobSetupValue(l.SetupAttributeId, l.AttributeCode, l.AttributeName, l.AttributeChangeMinutes, l.Value)).ToList(),
            wo.SetupMinutes);

    /// <summary>The work order that left its state on the machine, or null when there is none.</summary>
    public static WorkOrder? LastExecuted(ProductionData data, int equipmentId)
    {
        WorkOrder? running = null;
        var runningStart = DateTime.MinValue;
        WorkOrder? ended = null;
        var endedAt = DateTime.MinValue;
        foreach (var wo in data.WorkOrders.Values.OrderBy(w => w.Id))
        {
            if (wo.Status is not (WorkOrderStatus.Running or WorkOrderStatus.Ended)) continue;
            var sessions = data.SessionsOf(wo.Id).Where(s => s.EquipmentId == equipmentId).ToList();
            if (sessions.Count == 0) continue;
            if (wo.Status == WorkOrderStatus.Running)
            {
                var start = sessions.Max(s => s.StartedAt);
                if (running is null || start >= runningStart) { running = wo; runningStart = start; }
            }
            else
            {
                var end = wo.EndedAt ?? sessions.Max(s => s.EndedAt ?? s.StartedAt);
                if (ended is null || end >= endedAt) { ended = wo; endedAt = end; }
            }
        }
        return running ?? ended;
    }

    public static MachineSetupState? Of(ProductionData data, int equipmentId)
        => LastExecuted(data, equipmentId) is { } wo ? MachineSetupState.After(RequirementsOf(data, wo)) : null;
}
