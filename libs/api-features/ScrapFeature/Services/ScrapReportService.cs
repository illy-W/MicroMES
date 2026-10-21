using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.MaterialFeature.Repositories;
using MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiFeatures.ScrapFeature.Models;

namespace MicroMES.ApiFeatures.ScrapFeature.Services;

/// <summary>Scrap aggregations over the closed sessions of a period (current values: corrections already applied).</summary>
public class ScrapReportService(
    IWorkOrderResponseRepository sessions,
    IWorkOrderResponseScrapRepository scraps,
    IWorkOrderRepository workOrders,
    IProductionOrderRepository orders,
    IMaterialRepository materials)
{
    public async Task<ScrapResult<ScrapReportModel>> GetReportAsync(DateTime? from, DateTime? to)
    {
        var start = ToUtc(from);
        var end = ToUtc(to);
        if (start is not null && end is not null && end < start)
            return ScrapResult<ScrapReportModel>.Failed(ScrapFailure.Invalid, "Il periodo non è valido: 'to' precede 'from'.");

        // A "to" with no time of day means the whole day.
        DateTime? endExclusive = end is null ? null : end.Value.TimeOfDay == TimeSpan.Zero ? end.Value.AddDays(1) : end.Value.AddTicks(1);

        var closed = new List<WorkOrderResponse>();
        await foreach (var s in sessions.FetchAsync(x => x.EndedAt != null
                                                         && (start == null || x.EndedAt >= start)
                                                         && (endExclusive == null || x.EndedAt < endExclusive)))
            closed.Add(s);

        var sessionIds = closed.Select(s => s.Id).ToList();
        var rows = new List<WorkOrderResponseScrap>();
        if (sessionIds.Count > 0)
            await foreach (var r in scraps.FetchAsync(x => sessionIds.Contains(x.WorkOrderResponseId))) rows.Add(r);

        var woIds = closed.Select(s => s.WorkOrderId).Distinct().ToList();
        var wos = new Dictionary<int, WorkOrder>();
        if (woIds.Count > 0)
            await foreach (var w in workOrders.FetchAsync(x => woIds.Contains(x.Id))) wos[w.Id] = w;

        var orderIds = wos.Values.Select(w => w.ProductionOrderId).Distinct().ToList();
        var ords = new Dictionary<int, ProductionOrder>();
        if (orderIds.Count > 0)
            await foreach (var o in orders.FetchAsync(x => orderIds.Contains(x.Id))) ords[o.Id] = o;

        var materialIds = ords.Values.Select(o => o.MaterialId).Distinct().ToList();
        var mats = new Dictionary<int, Material>();
        if (materialIds.Count > 0)
            await foreach (var m in materials.FetchAsync(x => materialIds.Contains(x.Id))) mats[m.Id] = m;

        // ---- by reason ----
        var reasonTotals = new Dictionary<(int? Id, string? Code), (string Name, decimal Qty)>();
        void AddReason(int? id, string? code, string? name, decimal qty)
        {
            var key = (id, code);
            reasonTotals.TryGetValue(key, out var cur);
            reasonTotals[key] = (name ?? ScrapEntryRules.NoReasonLabel, cur.Qty + qty);
        }
        foreach (var r in rows) AddReason(r.ScrapReasonId, r.ReasonCode, r.ReasonName, r.Quantity);
        // Safety net: scrap with no rows at all (data older than the rows) counts as "no reason".
        foreach (var s in closed)
        {
            var missing = s.ScrapQuantity - rows.Where(r => r.WorkOrderResponseId == s.Id).Sum(r => r.Quantity);
            if (missing > 0) AddReason(null, null, null, missing);
        }
        if (!reasonTotals.ContainsKey((null, null))) reasonTotals[(null, null)] = (ScrapEntryRules.NoReasonLabel, 0m);

        var totalScrap = closed.Sum(s => s.ScrapQuantity);
        var totalGood = closed.Sum(s => s.GoodQuantity);

        var byReason = reasonTotals
            .Select(kv => new ScrapReportReasonModel
            {
                ScrapReasonId = kv.Key.Id, Code = kv.Key.Code, Name = kv.Value.Name, Quantity = kv.Value.Qty,
                Share = totalScrap == 0 ? 0m : Math.Round(kv.Value.Qty / totalScrap, 4)
            })
            .OrderByDescending(r => r.Quantity).ThenBy(r => r.Name, StringComparer.Ordinal).ToList();

        // ---- by segment and by material ----
        var byProcessSegment = closed
            .Where(s => wos.ContainsKey(s.WorkOrderId))
            .GroupBy(s => wos[s.WorkOrderId].ProcessSegmentId)
            .Select(g =>
            {
                var any = wos[g.First().WorkOrderId];
                var good = g.Sum(s => s.GoodQuantity);
                var scrap = g.Sum(s => s.ScrapQuantity);
                return new ScrapReportSegmentModel
                {
                    ProcessSegmentId = g.Key, Code = any.ProcessSegmentCode, Name = any.ProcessSegmentName,
                    GoodQuantity = good, ScrapQuantity = scrap, ScrapRate = Rate(scrap, good)
                };
            })
            .OrderByDescending(x => x.ScrapQuantity).ThenBy(x => x.Code, StringComparer.Ordinal).ToList();

        var byMaterial = closed
            .Where(s => wos.ContainsKey(s.WorkOrderId))
            .GroupBy(s => ords[wos[s.WorkOrderId].ProductionOrderId].MaterialId)
            .Select(g =>
            {
                var m = mats[g.Key];
                var good = g.Sum(s => s.GoodQuantity);
                var scrap = g.Sum(s => s.ScrapQuantity);
                return new ScrapReportMaterialModel
                {
                    MaterialId = m.Id, Code = m.Code, Name = m.Name,
                    GoodQuantity = good, ScrapQuantity = scrap, ScrapRate = Rate(scrap, good)
                };
            })
            .OrderByDescending(x => x.ScrapQuantity).ThenBy(x => x.Code, StringComparer.Ordinal).ToList();

        return ScrapResult<ScrapReportModel>.Ok(new ScrapReportModel
        {
            From = start, To = end, GoodQuantity = totalGood, ScrapQuantity = totalScrap, ScrapRate = Rate(totalScrap, totalGood),
            ByReason = byReason, ByProcessSegment = byProcessSegment, ByMaterial = byMaterial
        });
    }

    private static decimal Rate(decimal scrap, decimal good)
        => scrap + good <= 0 ? 0m : Math.Round(scrap / (scrap + good), 4);

    private static DateTime? ToUtc(DateTime? value) => value is null ? null : value.Value.Kind switch
    {
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        _ => value.Value
    };
}
