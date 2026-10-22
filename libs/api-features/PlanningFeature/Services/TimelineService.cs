using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Services;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;

namespace MicroMES.ApiFeatures.PlanningFeature.Services;

/// <summary>
/// Read-only timeline of a window (max 62 days). Which work orders appear and on which interval:
/// open ones with a plan (planned interval), Running ones (real start to the planned end, or to now when the plan is missing or past),
/// Ended/Aborted ones that started (real interval). Row: the machine of the open session (Running), the last session
/// (Ended) or the assigned one; none = the virtual row. Lanes are computed on every bar of the window before the filters apply, so they do not
/// change with the filters. Non-working spans and load use the effective calendar of the machine (the default calendar for the virtual row).
/// </summary>
public class TimelineService(
    ProductionDataLoader loader,
    PlanningCatalogLoader catalogLoader,
    EffectiveCalendarService calendars,
    TimeProvider timeProvider)
{
    private static DateTime Utc(DateTime t) => t.Kind switch
    {
        DateTimeKind.Utc => t,
        DateTimeKind.Local => t.ToUniversalTime(),
        _ => DateTime.SpecifyKind(t, DateTimeKind.Utc)
    };

    private sealed record Bar
    {
        public Bar(WorkOrder wo, int? equipmentId, DateTime start, DateTime end)
        {
            Wo = wo;
            EquipmentId = equipmentId;
            Start = Utc(start);
            End = Utc(end);
        }

        public WorkOrder Wo { get; }
        public int? EquipmentId { get; }
        public DateTime Start { get; }
        public DateTime End { get; }
    }

    private static DateTime? Utc(DateTime? t) => t is { } v ? Utc(v) : null;

    public async Task<ProductionOrderResult<TimelineModel>> GetAsync(
        DateTime from, DateTime to, int? equipmentId = null, WorkOrderStatus? status = null, int? productionOrderId = null, bool? locked = null)
    {
        from = Utc(from);
        to = Utc(to);
        if (to <= from)
            return ProductionOrderResult<TimelineModel>.Invalid("La fine della finestra deve essere dopo l'inizio.");
        if ((to - from).TotalDays > TimelineCalculator.MaxWindowDays)
            return ProductionOrderResult<TimelineModel>.Invalid(
                $"La finestra è di {Math.Ceiling((to - from).TotalDays):0} giorni: il massimo è {TimelineCalculator.MaxWindowDays}.");

        var data = await loader.LoadAllAsync();
        var catalog = await catalogLoader.LoadAsync();
        var directory = await calendars.LoadAsync();
        if (equipmentId is { } eid && !catalog.Machines.ContainsKey(eid))
            return ProductionOrderResult<TimelineModel>.NotFound("Macchina non trovata.");
        if (productionOrderId is { } pid && !data.Orders.ContainsKey(pid))
            return ProductionOrderResult<TimelineModel>.NotFound("Ordine di produzione non trovato.");
        foreach (var m in catalog.Machines.Values) data.Equipment.TryAdd(m.Id, m);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var bars = new List<Bar>();
        foreach (var wo in data.WorkOrders.Values)
        {
            var bar = BarOf(data, wo, now);
            if (bar is null) continue;
            var inWindow = bar.End > bar.Start ? bar.Start < to && bar.End > from : bar.Start >= from && bar.Start < to;
            if (inWindow) bars.Add(bar);
        }

        var lanes = new Dictionary<int, int>();
        foreach (var g in bars.GroupBy(b => b.EquipmentId))
            foreach (var kv in TimelineCalculator.AssignLanes(g.Select(b => (b.Wo.Id, b.Start, b.End)))) lanes[kv.Key] = kv.Value;

        // Rows: active machines (also without work) plus the inactive ones that have bars.
        var rows = catalog.Machines.Values
            .Where(m => (equipmentId is null || m.Id == equipmentId) && (m.IsActive || bars.Any(b => b.EquipmentId == m.Id)))
            .OrderBy(m => m.Code, StringComparer.Ordinal).ToList();

        var shown = bars.Where(b => (equipmentId is null || b.EquipmentId == equipmentId)
                                    && (status is null || b.Wo.Status == status)
                                    && (productionOrderId is null || b.Wo.ProductionOrderId == productionOrderId)
                                    && (locked is not true || b.Wo.IsPlanLocked)).ToList();

        var period = TimelineCalculator.PeriodFor(from, to);
        var nonWorking = new List<TimelineNonWorkingModel>();
        var load = new List<TimelineLoadModel>();
        foreach (var m in rows)
        {
            var source = directory.ForEquipment(m.Id);
            nonWorking.AddRange(TimelineCalculator.NonWorking(m.Id, source, directory.Calculator, from, to));
            load.AddRange(TimelineCalculator.Load(m.Id, m.Capacity, source, directory.Calculator,
                bars.Where(b => b.EquipmentId == m.Id).Select(b => (b.Start, b.End)).ToList(), from, to, period));
        }
        if (equipmentId is null && shown.Any(b => b.EquipmentId is null))
            nonWorking.AddRange(TimelineCalculator.NonWorking(null, directory.ForEquipment(null), directory.Calculator, from, to));

        var workOrders = shown.OrderBy(b => b.Start).ThenBy(b => b.Wo.Id).Select(b => ToModel(data, b, lanes[b.Wo.Id])).ToList();
        return ProductionOrderResult<TimelineModel>.Ok(new TimelineModel
        {
            From = from, To = to, TimeZoneId = directory.Zone.Id, LoadPeriod = period,
            Equipment = rows.Select(m => new TimelineEquipmentModel { Id = m.Id, Code = m.Code, Name = m.Name, Capacity = Math.Max(1, m.Capacity), IsActive = m.IsActive }).ToList(),
            WorkOrders = workOrders, NonWorking = nonWorking, Load = load, GeneratedAt = now
        });
    }

    private static Bar? BarOf(ProductionData data, WorkOrder wo, DateTime now)
    {
        var sessions = data.SessionsOf(wo.Id).ToList();
        switch (wo.Status)
        {
            case WorkOrderStatus.Ended or WorkOrderStatus.Aborted:
                var start = sessions.FirstOrDefault()?.StartedAt ?? wo.StartedAt;
                var end = wo.EndedAt ?? sessions.LastOrDefault()?.EndedAt;
                if (start is null || end is null) return null;
                return new Bar(wo, sessions.LastOrDefault(s => s.EquipmentId is not null)?.EquipmentId ?? wo.AssignedEquipmentId, start.Value, end.Value);
            case WorkOrderStatus.Running:
                var began = sessions.FirstOrDefault()?.StartedAt ?? wo.StartedAt ?? wo.PlannedStartAt ?? now;
                var until = wo.PlannedEndAt is { } p && p > now ? p : now;
                return new Bar(wo, sessions.LastOrDefault(s => s.EndedAt is null)?.EquipmentId ?? wo.AssignedEquipmentId, began, until < began ? began : until);
            default:
                return wo.PlannedStartAt is { } ps && wo.PlannedEndAt is { } pe ? new Bar(wo, wo.AssignedEquipmentId, ps, pe) : null;
        }
    }

    private static TimelineWorkOrderModel ToModel(ProductionData data, Bar b, int lane)
    {
        var wo = b.Wo;
        var order = data.Orders[wo.ProductionOrderId];
        var open = wo.Status is not (WorkOrderStatus.Ended or WorkOrderStatus.Aborted);
        var late = open && wo.PlannedEndAt is { } end && order.RequestedEnd is { } due && end > due;
        var sessions = data.SessionsOf(wo.Id).ToList();
        var started = sessions.FirstOrDefault()?.StartedAt ?? wo.StartedAt;
        return new TimelineWorkOrderModel
        {
            Id = wo.Id, Code = ProductionData.CodeOf(order, wo), ProductionOrderId = order.Id, ProductionOrderCode = order.Code,
            MaterialName = data.Materials[order.MaterialId].Name, ProcessSegmentCode = wo.ProcessSegmentCode, ProcessSegmentName = wo.ProcessSegmentName,
            EquipmentId = b.EquipmentId, PlannedStartAt = Utc(wo.PlannedStartAt), PlannedEndAt = Utc(wo.PlannedEndAt),
            ActualStartAt = wo.Status is WorkOrderStatus.Running or WorkOrderStatus.Ended or WorkOrderStatus.Aborted ? Utc(started) : null,
            ActualEndAt = wo.Status is WorkOrderStatus.Ended or WorkOrderStatus.Aborted ? Utc(wo.EndedAt ?? sessions.LastOrDefault()?.EndedAt) : null,
            Status = wo.Status, PlanStatus = data.PlanStatusOf(wo), PlanStatusReason = data.PlanStatusReasonOf(wo), IsPlanLocked = wo.IsPlanLocked, PlanLockedAt = Utc(wo.PlanLockedAt), IsLate = late,
            LateDays = late ? (int)Math.Ceiling((wo.PlannedEndAt!.Value - order.RequestedEnd!.Value).TotalDays) : 0,
            PredecessorIds = data.PredecessorIds(wo.Id).OrderBy(i => i).ToList(), Lane = lane, Priority = order.Priority,
            Tooling = data.ToolingOf(wo.Id), SetupValues = data.SetupValuesOf(wo.Id)
        };
    }
}
