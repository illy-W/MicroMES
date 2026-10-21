using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;

namespace MicroMES.ApiTests.Features.ScrapFeature;

public class ScrapEntryRulesFixture
{
    private const int Segment = 7;

    private static readonly Dictionary<int, ScrapReason> Reasons = new()
    {
        [1] = new ScrapReason { Id = 1, Code = "GRAFFIO", Name = "Graffio", IsActive = true },
        [2] = new ScrapReason { Id = 2, Code = "SAL", Name = "Saldatura", IsActive = true },
        [3] = new ScrapReason { Id = 3, Code = "OLD", Name = "Vecchio", IsActive = false }
    };

    private static readonly Dictionary<int, HashSet<int>> Links = new() { [2] = [99] };

    private static ScrapEntryRequest E(int? reason, decimal qty) => new() { ScrapReasonId = reason, Quantity = qty };

    private static ScrapResolution Resolve(decimal scrap, params ScrapEntryRequest[]? entries)
        => ScrapEntryRules.Resolve(scrap, entries, Segment, Reasons, Links);

    [Fact]
    public void Resolve_NoEntriesWithScrap_Success_OneRowWithoutReason()
    {
        var r = ScrapEntryRules.Resolve(3, null, Segment, Reasons, Links);

        r.IsValid.Should().BeTrue();
        r.Entries.Should().ContainSingle().Which.Should().Be(new ResolvedScrapEntry(null, null, null, 3));
    }

    [Fact]
    public void Resolve_NoEntriesWithoutScrap_Success_NoRows()
        => ScrapEntryRules.Resolve(0, [], Segment, Reasons, Links).Entries.Should().BeEmpty();

    [Fact]
    public void Resolve_MultipleReasons_Success_SnapshotAndMerge()
    {
        var r = Resolve(4, E(1, 1), E(1, 2), E(null, 1), E(1, 0));

        r.IsValid.Should().BeTrue();
        r.Entries.Should().BeEquivalentTo(new[]
        {
            new ResolvedScrapEntry(1, "GRAFFIO", "Graffio", 3),
            new ResolvedScrapEntry(null, null, null, 1)
        });
    }

    [Fact]
    public void Resolve_SumDifferentFromScrap_Failure()
        => Resolve(5, E(1, 2), E(null, 1)).Error.Should().Contain("somma");

    [Fact]
    public void Resolve_NegativeQuantity_Failure()
        => Resolve(1, E(1, 2), E(null, -1)).Error.Should().Contain("negative");

    [Fact]
    public void Resolve_InactiveReason_Failure()
        => Resolve(1, E(3, 1)).Error.Should().Contain("non è attivo");

    [Fact]
    public void Resolve_ReasonLinkedToOtherSegment_Failure()
        => Resolve(1, E(2, 1)).Error.Should().Contain("non è valido per la fase");

    [Fact]
    public void Resolve_UnknownReason_Failure()
        => Resolve(1, E(404, 1)).Error.Should().Contain("non esiste");

    [Fact]
    public void Resolve_ReasonLinkedToSegment_Success()
        => ScrapEntryRules.Resolve(1, [E(2, 1)], 99, Reasons, Links).IsValid.Should().BeTrue();

    [Fact]
    public void Describe_Rows_Success_ReadableText()
        => ScrapEntryRules.Describe([("Graffio", 2m), (null, 1m)]).Should().Be("Graffio: 2; Senza motivo: 1");
}
