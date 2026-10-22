using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Services;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;

namespace MicroMES.ApiFeatures.PlanningFeature.Services;

/// <summary>
/// Read-only operational overview of a machine. It reuses the existing calculators: effective calendar (state now), the timeline
/// (load), <see cref="MachineStartingState"/> (mounted state) and the plan alerts (filtered by machine). Nothing is stored.
/// </summary>
public class EquipmentOverviewService(
    ProductionDataLoader loader,
    PlanningCatalogLoader catalogLoader,
    EffectiveCalendarService calendars,
    TimelineService timeline,
    PlanningService planning,
    TimeProvider timeProvider)
{
    public const int QueueSize = 20;

    private static DateTime Utc(DateTime t) => t.Kind == DateTimeKind.Utc ? t : DateTime.SpecifyKind(t, DateTimeKind.Utc);

    public async Task<ProductionOrderResult<EquipmentOverviewModel>> GetAsync(int equipmentId)
    {
        var catalog = await catalogLoader.LoadAsync();
        if (!catalog.Machines.TryGetValue(equipmentId, out var machine))
            return ProductionOrderResult<EquipmentOverviewModel>.NotFound("La macchina non esiste.");

        var data = await loader.LoadAllAsync();
        var directory = await calendars.LoadAsync();
        var calc = directory.Calculator;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        string Code(WorkOrder w) => ProductionData.CodeOf(data.Orders[w.ProductionOrderId], w);

        var running = new List<EquipmentRunningModel>();
        foreach (var wo in data.WorkOrders.Values.Where(w => w.Status == WorkOrderStatus.Running).OrderBy(w => w.Id))
        {
            var open = data.SessionsOf(wo.Id).LastOrDefault(s => s.EndedAt is null);
            if (open is null || open.EquipmentId != equipmentId) continue;
            var order = data.Orders[wo.ProductionOrderId];
            running.Add(new EquipmentRunningModel
            {
                WorkOrderId = wo.Id, WorkOrderCode = Code(wo), ProductionOrderId = order.Id, ProductionOrderCode = order.Code,
                ProcessSegmentName = wo.ProcessSegmentName, ActualStartAt = Utc(open.StartedAt),
                ElapsedMinutes = Math.Max(0, (int)Math.Floor((now - Utc(open.StartedAt)).TotalMinutes))
            });
        }
        running = running.OrderBy(r => r.ActualStartAt).ThenBy(r => r.WorkOrderId).ToList();

        var queue = data.WorkOrders.Values
            .Where(w => w.AssignedEquipmentId == equipmentId
                        && w.Status is WorkOrderStatus.Waiting or WorkOrderStatus.Ready or WorkOrderStatus.Interrupted
                        && w.PlannedStartAt is not null && w.PlannedEndAt is not null && Utc(w.PlannedEndAt.Value) > now)
            .OrderBy(w => w.PlannedStartAt).ThenBy(w => w.Id).Take(QueueSize)
            .Select(w =>
            {
                var order = data.Orders[w.ProductionOrderId];
                return new EquipmentQueueItemModel
                {
                    WorkOrderId = w.Id, WorkOrderCode = Code(w), ProductionOrderId = order.Id, ProductionOrderCode = order.Code,
                    ProcessSegmentCode = w.ProcessSegmentCode, ProcessSegmentName = w.ProcessSegmentName,
                    PlannedStartAt = Utc(w.PlannedStartAt!.Value), PlannedEndAt = Utc(w.PlannedEndAt!.Value),
                    PlannedSetupMinutes = w.PlannedSetupMinutes, PlanStatus = data.PlanStatusOf(w),
                    PlanStatusReason = data.PlanStatusReasonOf(w), IsPlanLocked = w.IsPlanLocked
                };
            }).ToList();

        var (state, reason) = EquipmentOverviewCalculator.StateOf(machine.IsActive, running.Count > 0, directory.ForEquipment(equipmentId), calc, now);

        // Load: same computation as the timeline (day periods up to 14 days, weeks beyond), aggregated over the window.
        var today = calc.LocalDate(now);
        var start = calc.ToUtc(today, TimeOnly.MinValue);
        var week = await timeline.GetAsync(start, calc.ToUtc(today.AddDays(7), TimeOnly.MinValue), equipmentId);
        var month = await timeline.GetAsync(start, calc.ToUtc(today.AddDays(30), TimeOnly.MinValue), equipmentId);
        if (!week.IsSuccessful || !month.IsSuccessful || week.Data is null || month.Data is null)
            return ProductionOrderResult<EquipmentOverviewModel>.Invalid("Impossibile calcolare il carico della macchina.");
        var dayEnd = calc.ToUtc(today.AddDays(1), TimeOnly.MinValue);
        var load = new EquipmentLoadSummaryModel
        {
            LoadToday = EquipmentOverviewCalculator.Aggregate(week.Data.Load.Where(l => l.PeriodStart >= start && l.PeriodEnd <= dayEnd)),
            Load7 = EquipmentOverviewCalculator.Aggregate(week.Data.Load),
            Load30 = EquipmentOverviewCalculator.Aggregate(month.Data.Load)
        };

        var alerts = await planning.GetAlertsAsync();
        if (!alerts.IsSuccessful || alerts.Data is null)
            return ProductionOrderResult<EquipmentOverviewModel>.Invalid("Impossibile calcolare le segnalazioni.");

        return ProductionOrderResult<EquipmentOverviewModel>.Ok(new EquipmentOverviewModel
        {
            EquipmentId = equipmentId, State = state, ClosedReason = state == EquipmentNowState.ClosedNow ? reason : null,
            Running = running, Queue = queue, MountedState = EquipmentOverviewCalculator.MountedStateOf(data, equipmentId),
            Load = load, Alerts = alerts.Data.Alerts.Where(a => a.EquipmentId == equipmentId).ToList(), GeneratedAt = now
        });
    }
}
