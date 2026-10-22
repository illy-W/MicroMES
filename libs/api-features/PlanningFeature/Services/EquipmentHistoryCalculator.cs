using MicroMES.ApiFeatures.PlanningFeature.Models;

namespace MicroMES.ApiFeatures.PlanningFeature.Services;

/// <summary>Pure rules of the machine history.</summary>
public static class EquipmentHistoryCalculator
{
    public static readonly int[] AllowedDays = [7, 30, 90];
    public const string InvalidDaysMessage = "Il periodo deve essere 7, 30 o 90 giorni.";
    public const string NoReasonCode = "-";
    public const string NoReasonName = "Senza motivo";

    /// <summary>Minutes of the session inside [periodStart, now]; a running session counts up to now.</summary>
    public static decimal ClippedMinutes(DateTime startedAt, DateTime? endedAt, DateTime periodStart, DateTime now)
    {
        var from = startedAt > periodStart ? startedAt : periodStart;
        var to = endedAt is { } e && e < now ? e : now;
        return to > from ? (decimal)(to - from).TotalMinutes : 0m;
    }

    public static decimal? Utilization(decimal worked, decimal available)
        => available > 0 ? Math.Round(worked / available * 100m, 1) : null;

    public static decimal? ScrapPercent(decimal good, decimal scrap)
        => good + scrap > 0 ? Math.Round(scrap / (good + scrap) * 100m, 1) : null;

    /// <summary>Groups scrap rows by reason; the part of the session scrap without detail rows goes to the "no reason" bucket.</summary>
    public static List<EquipmentScrapByReasonModel> ByReason(
        IEnumerable<(int? ReasonId, string? Code, string? Name, decimal Quantity)> rows, decimal undetailed)
    {
        var list = new List<EquipmentScrapByReasonModel>();
        decimal none = undetailed > 0 ? undetailed : 0;
        foreach (var g in rows.Where(r => r.ReasonId is not null).GroupBy(r => r.ReasonId))
        {
            var first = g.First();
            list.Add(new EquipmentScrapByReasonModel
            {
                ReasonId = g.Key, Code = first.Code ?? NoReasonCode, Name = first.Name ?? NoReasonName, Quantity = g.Sum(r => r.Quantity)
            });
        }
        none += rows.Where(r => r.ReasonId is null).Sum(r => r.Quantity);
        list = list.OrderByDescending(x => x.Quantity).ThenBy(x => x.Code).ToList();
        if (none > 0) list.Add(new EquipmentScrapByReasonModel { Code = NoReasonCode, Name = NoReasonName, Quantity = none });
        return list.Where(x => x.Quantity > 0).ToList();
    }
}
