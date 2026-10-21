using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ScrapFeature.Repositories;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Services;

/// <summary>A validated scrap row with the snapshot of the reason (null reason = "no reason").</summary>
public sealed record ResolvedScrapEntry(int? ScrapReasonId, string? ReasonCode, string? ReasonName, decimal Quantity);

/// <summary>Outcome of the scrap rule: the rows to write, or the (Italian) reason why the request is invalid.</summary>
public sealed record ScrapResolution(List<ResolvedScrapEntry> Entries, string? Error)
{
    public bool IsValid => Error is null;
}

/// <summary>The single scrap rule shared by closing a session and correcting it.</summary>
public static class ScrapEntryRules
{
    public const string NoReasonLabel = "Senza motivo";

    /// <param name="reasons">The reasons referenced by the entries, by id.</param>
    /// <param name="segmentLinks">Process segments of each reason (missing or empty = every segment).</param>
    public static ScrapResolution Resolve(
        decimal scrapQuantity,
        IReadOnlyList<ScrapEntryRequest>? entries,
        int processSegmentId,
        IReadOnlyDictionary<int, ScrapReason> reasons,
        IReadOnlyDictionary<int, HashSet<int>> segmentLinks)
    {
        if (entries is null || entries.Count == 0)
            return new ScrapResolution(scrapQuantity > 0 ? [new ResolvedScrapEntry(null, null, null, scrapQuantity)] : [], null);

        if (entries.Any(e => e.Quantity < 0))
            return Invalid("Le quantità di scarto per motivo non possono essere negative.");

        // Zero rows are ignored; rows of the same reason are summed.
        var positive = entries.Where(e => e.Quantity > 0)
            .GroupBy(e => e.ScrapReasonId)
            .Select(g => (ReasonId: g.Key, Quantity: g.Sum(e => e.Quantity)))
            .ToList();

        var sum = positive.Sum(p => p.Quantity);
        if (sum != scrapQuantity)
            return Invalid($"La somma degli scarti per motivo ({sum}) deve essere uguale alla quantità scartata ({scrapQuantity}).");

        var result = new List<ResolvedScrapEntry>();
        foreach (var (reasonId, quantity) in positive)
        {
            if (reasonId is not { } id)
            {
                result.Add(new ResolvedScrapEntry(null, null, null, quantity));
                continue;
            }
            if (!reasons.TryGetValue(id, out var reason))
                return Invalid($"Il motivo di scarto {id} non esiste.");
            if (!reason.IsActive)
                return Invalid($"Il motivo di scarto '{reason.Name}' non è attivo.");
            if (segmentLinks.TryGetValue(id, out var segments) && segments.Count > 0 && !segments.Contains(processSegmentId))
                return Invalid($"Il motivo di scarto '{reason.Name}' non è valido per la fase di questo lavoro.");
            result.Add(new ResolvedScrapEntry(id, reason.Code, reason.Name, quantity));
        }
        return new ScrapResolution(result, null);
    }

    /// <summary>Readable text of a set of rows, e.g. "Graffio: 2; Senza motivo: 1".</summary>
    public static string? Describe(IEnumerable<(string? Name, decimal Quantity)> rows)
    {
        var text = string.Join("; ", rows.Select(r => $"{r.Name ?? NoReasonLabel}: {r.Quantity:0.####}"));
        if (text.Length == 0) return null;
        return text.Length <= 1000 ? text : text[..1000];
    }

    private static ScrapResolution Invalid(string message) => new([], message);
}

/// <summary>Loads the reasons referenced by a request and applies <see cref="ScrapEntryRules"/>.</summary>
public class ScrapEntryResolver(IUnitOfWork unitOfWork)
{
    public async Task<ScrapResolution> ResolveAsync(
        decimal scrapQuantity, IReadOnlyList<ScrapEntryRequest>? entries, int processSegmentId)
    {
        var ids = entries?.Where(e => e.ScrapReasonId is not null && e.Quantity > 0)
            .Select(e => e.ScrapReasonId!.Value).Distinct().ToList() ?? [];

        var reasons = new Dictionary<int, ScrapReason>();
        var links = new Dictionary<int, HashSet<int>>();
        if (ids.Count > 0)
        {
            var reasonRepo = unitOfWork.GetRepository<IScrapReasonRepository>();
            var linkRepo = unitOfWork.GetRepository<IScrapReasonProcessSegmentRepository>();
            await foreach (var r in reasonRepo.FetchAsync(x => ids.Contains(x.Id))) reasons[r.Id] = r;
            await foreach (var l in linkRepo.FetchAsync(x => ids.Contains(x.ScrapReasonId)))
            {
                if (!links.TryGetValue(l.ScrapReasonId, out var set)) links[l.ScrapReasonId] = set = [];
                set.Add(l.ProcessSegmentId);
            }
        }
        return ScrapEntryRules.Resolve(scrapQuantity, entries, processSegmentId, reasons, links);
    }
}
