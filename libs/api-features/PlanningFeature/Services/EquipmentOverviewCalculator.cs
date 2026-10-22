using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;

namespace MicroMES.ApiFeatures.PlanningFeature.Services;

/// <summary>Pure rules of the machine overview: state now, aggregate load, mounted state.</summary>
public static class EquipmentOverviewCalculator
{
    public const string OutOfShiftReason = "fuori turno";
    public const string BreakReason = "pausa";

    /// <summary>Inactive, else Working when something runs on it, else ClosedNow (with the reason) when the calendar is closed at the instant, else Free.</summary>
    public static (EquipmentNowState State, string? ClosedReason) StateOf(
        bool isActive, bool hasRunning, IAvailabilitySource source, WorkingTimeCalculator calc, DateTime nowUtc)
    {
        if (!isActive) return (EquipmentNowState.Inactive, null);
        if (hasRunning) return (EquipmentNowState.Working, null);

        var date = calc.LocalDate(nowUtc);
        var intervals = calc.DayIntervals(source, date);
        if (intervals.Any(i => i.Start <= nowUtc && nowUtc < i.End)) return (EquipmentNowState.Free, null);

        var winner = AvailabilityResolver.Winning(source, date);
        if (winner is not null && !string.IsNullOrWhiteSpace(winner.Reason)) return (EquipmentNowState.ClosedNow, winner.Reason);
        var inBreak = intervals.Count > 0 && intervals.Min(i => i.Start) <= nowUtc && nowUtc < intervals.Max(i => i.End);
        return (EquipmentNowState.ClosedNow, inBreak ? BreakReason : OutOfShiftReason);
    }

    /// <summary>Busy / capacity minutes of the periods, as a percentage (1 decimal); null when there is no capacity.</summary>
    public static decimal? Aggregate(IEnumerable<TimelineLoadModel> periods)
    {
        var list = periods.ToList();
        var capacity = list.Sum(p => p.CapacityMinutes);
        return capacity > 0 ? Math.Round(list.Sum(p => p.BusyMinutes) / capacity * 100m, 1) : null;
    }

    /// <summary>Mounted tooling and values from the last executed work order of the machine (the same one the setup calculation uses).</summary>
    public static EquipmentMountedStateModel MountedStateOf(ProductionData data, int equipmentId)
    {
        var wo = MachineStartingState.LastExecuted(data, equipmentId);
        if (wo is null) return new EquipmentMountedStateModel { Known = false, Tooling = [], SetupValues = [] };
        return new EquipmentMountedStateModel
        {
            Known = true,
            FromWorkOrderCode = ProductionData.CodeOf(data.Orders[wo.ProductionOrderId], wo),
            Tooling = data.ToolingOf(wo.Id).Select(t => new EquipmentMountedToolingModel { Id = t.ToolingId, Code = t.Code, Name = t.Name }).ToList(),
            SetupValues = data.SetupValuesOf(wo.Id).Select(v => new EquipmentMountedSetupValueModel { AttributeCode = v.Code, AttributeName = v.Name, Value = v.Value }).ToList()
        };
    }
}
