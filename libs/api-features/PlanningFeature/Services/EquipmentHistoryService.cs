using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Services;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;

namespace MicroMES.ApiFeatures.PlanningFeature.Services;

/// <summary>Read-only history of a machine from the real sessions (machine of the session, not the planned one).</summary>
public class EquipmentHistoryService(
    IEquipmentRepository equipment,
    IWorkOrderResponseRepository sessions,
    IWorkOrderResponseScrapRepository scraps,
    IWorkOrderRepository workOrders,
    IProductionOrderRepository orders,
    EffectiveCalendarService calendars,
    TimeProvider timeProvider)
{
    public const int MaxPageSize = 100;

    public async Task<ProductionOrderResult<EquipmentHistoryModel>> GetAsync(int equipmentId, int days, int page, int pageSize)
    {
        if (!EquipmentHistoryCalculator.AllowedDays.Contains(days))
            return ProductionOrderResult<EquipmentHistoryModel>.Invalid(EquipmentHistoryCalculator.InvalidDaysMessage);
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        if (await equipment.GetSingleAsync(equipmentId) is null)
            return ProductionOrderResult<EquipmentHistoryModel>.NotFound("La macchina non esiste.");

        var directory = await calendars.LoadAsync();
        var calc = directory.Calculator;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = calc.LocalDate(now);
        var periodStart = calc.ToUtc(today.AddDays(1 - days), TimeOnly.MinValue);

        // Filtered in the database: sessions of this machine overlapping the period.
        var rows = new List<WorkOrderResponse>();
        await foreach (var s in sessions.FetchAsync(x => x.EquipmentId == equipmentId && x.StartedAt < now && (x.EndedAt == null || x.EndedAt > periodStart)))
            rows.Add(s);

        var woIds = rows.Select(r => r.WorkOrderId).Distinct().ToList();
        var wos = new Dictionary<int, WorkOrder>();
        if (woIds.Count > 0) await foreach (var w in workOrders.FetchAsync(x => woIds.Contains(x.Id))) wos[w.Id] = w;
        var orderIds = wos.Values.Select(w => w.ProductionOrderId).Distinct().ToList();
        var ords = new Dictionary<int, ProductionOrder>();
        if (orderIds.Count > 0) await foreach (var o in orders.FetchAsync(x => orderIds.Contains(x.Id))) ords[o.Id] = o;
        var sessionIds = rows.Select(r => r.Id).ToList();
        var scrapRows = new List<WorkOrderResponseScrap>();
        if (sessionIds.Count > 0) await foreach (var s in scraps.FetchAsync(x => sessionIds.Contains(x.WorkOrderResponseId))) scrapRows.Add(s);

        decimal worked = 0, good = rows.Sum(r => r.GoodQuantity), scrap = rows.Sum(r => r.ScrapQuantity);
        foreach (var r in rows) worked += EquipmentHistoryCalculator.ClippedMinutes(Utc(r.StartedAt), r.EndedAt is null ? null : Utc(r.EndedAt.Value), periodStart, now);

        var source = directory.ForEquipment(equipmentId);
        decimal available = 0;
        for (var d = today.AddDays(1 - days); d <= today; d = d.AddDays(1))
            foreach (var (s, e) in calc.DayIntervals(source, d))
            {
                var from = s > periodStart ? s : periodStart;
                var to = e < now ? e : now;
                if (to > from) available += (decimal)(to - from).TotalMinutes;
            }

        var detailed = scrapRows.Sum(s => s.Quantity);
        var summary = new EquipmentHistorySummaryModel
        {
            WorkOrders = woIds.Count,
            CompletedWorkOrders = woIds.Count(id => wos.TryGetValue(id, out var w) && w.Status == WorkOrderStatus.Ended),
            WorkedMinutes = Math.Round(worked, 1), AvailableMinutes = Math.Round(available, 1),
            UtilizationPercent = EquipmentHistoryCalculator.Utilization(worked, available),
            GoodQuantity = good, ScrapQuantity = scrap, ScrapPercent = EquipmentHistoryCalculator.ScrapPercent(good, scrap)
        };
        var byReason = EquipmentHistoryCalculator.ByReason(
            scrapRows.Select(s => (s.ScrapReasonId, s.ReasonCode, s.ReasonName, s.Quantity)), scrap - detailed);

        var items = rows.OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r =>
            {
                var w = wos[r.WorkOrderId];
                var o = ords[w.ProductionOrderId];
                var end = r.EndedAt is null ? now : Utc(r.EndedAt.Value);
                return new EquipmentHistorySessionModel
                {
                    SessionId = r.Id, WorkOrderId = w.Id, WorkOrderCode = ProductionData.CodeOf(o, w), ProductionOrderId = o.Id,
                    ProductionOrderCode = o.Code, ProcessSegmentName = w.ProcessSegmentName, StartedAt = Utc(r.StartedAt),
                    EndedAt = r.EndedAt is null ? null : Utc(r.EndedAt.Value),
                    DurationMinutes = Math.Round(Math.Max(0m, (decimal)(end - Utc(r.StartedAt)).TotalMinutes), 1),
                    GoodQuantity = r.GoodQuantity, ScrapQuantity = r.ScrapQuantity, IsRunning = r.EndedAt is null
                };
            }).ToList();

        return ProductionOrderResult<EquipmentHistoryModel>.Ok(new EquipmentHistoryModel
        {
            EquipmentId = equipmentId, Days = days, PeriodStart = periodStart, PeriodEnd = now, Summary = summary, ScrapByReason = byReason,
            Sessions = new EquipmentHistorySessionPageModel { Items = items, Page = page, PageSize = pageSize, TotalCount = rows.Count }
        });
    }

    private static DateTime Utc(DateTime t) => t.Kind == DateTimeKind.Utc ? t : DateTime.SpecifyKind(t, DateTimeKind.Utc);
}
