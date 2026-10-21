using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

public class ScrapApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static ScrapEntryRequest E(int? reason, decimal qty) => new() { ScrapReasonId = reason, Quantity = qty };

    /// <summary>One product with a single-step routing on segment CUT, one order released; returns (workOrderId, orderId, cutId, otherSegmentId).</summary>
    private async Task<(int Wo, int Order, int Cut, int Other)> ScenarioAsync(int steps = 1)
    {
        var cut = await Api.CreateSegmentAsync("CUT");
        var other = await Api.CreateSegmentAsync("OTH");
        var product = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        await Api.PutLinearRoutingAsync(product.Id, steps == 1 ? [cut.Id] : [cut.Id, other.Id]);
        var order = await Api.CreateOrderAsync(product.Id, 10);
        await Api.ReleaseAsync(order.Id);
        var wo = (await Api.GetWorkOrdersAsync(order.Id)).Ok.Single(w => w.OperationNumber == 10).Id;
        await Api.StartAsync(wo);
        return (wo, order.Id, cut.Id, other.Id);
    }

    [Fact]
    public async Task ScrapReasons_DuplicateCode_Failure_Conflict409WithSpecificMessage()
    {
        await Api.CreateScrapReasonAsync("GRAFFIO");

        var duplicate = await Api.CreateScrapReasonRawAsync("graffio");

        duplicate.Status.Should().Be(HttpStatusCode.Conflict);
        duplicate.Raw.Should().Contain("motivo di scarto").And.NotContainEquivalentOf("riprova");
    }

    [Fact]
    public async Task ScrapReasons_UpdateToExistingCode_Failure_Conflict409()
    {
        await Api.CreateScrapReasonAsync("DUP");
        var segment = await Api.CreateSegmentAsync("S1");
        var reason = await Api.CreateScrapReasonAsync("LNK", segment.Id);

        var update = await Api.PutAsync<object>($"/api/scrap-reasons/{reason.Id}",
            new { code = "DUP", name = "x", isActive = true, processSegmentIds = Array.Empty<int>() });

        update.Status.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Stop_MultipleReasons_Success_RowsStoredAndReportGroups()
    {
        var (wo, _, cut, _) = await ScenarioAsync();
        var scratch = await Api.CreateScrapReasonAsync("GRAFFIO");
        var weld = await Api.CreateScrapReasonAsync("SAL", cut);

        var stopped = (await Api.StopWithScrapAsync(wo, 6, 4, E(scratch.Id, 2), E(weld.Id, 1), E(null, 1))).Ok;

        stopped.Sessions.Single().ScrapEntries.Should().HaveCount(3);
        var report = (await Api.GetScrapReportAsync()).Ok;
        report.ScrapQuantity.Should().Be(4);
        report.ScrapRate.Should().Be(0.4m);
        report.ByReason.Select(r => (r.Name, r.Quantity)).Should().BeEquivalentTo(new[]
        {
            ("Motivo GRAFFIO", 2m), ("Motivo SAL", 1m), ("Senza motivo", 1m)
        });
        report.ByProcessSegment.Single().Code.Should().Be("CUT");
    }

    [Fact]
    public async Task Stop_ReasonNotValidForSegment_Failure_Unprocessable422()
    {
        var (wo, _, _, other) = await ScenarioAsync();
        var onlyOther = await Api.CreateScrapReasonAsync("ONLY-OTH", other);

        var result = await Api.StopWithScrapAsync(wo, 1, 1, E(onlyOther.Id, 1));

        result.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.GetWorkOrderAsync(wo)).Ok.Status.Should().Be(WorkOrderStatus.Running);
    }

    [Fact]
    public async Task Stop_WrongSum_Failure_Unprocessable422()
    {
        var (wo, _, _, _) = await ScenarioAsync();
        var reason = await Api.CreateScrapReasonAsync("GRAFFIO");

        (await Api.StopWithScrapAsync(wo, 1, 3, E(reason.Id, 1))).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Stop_WithoutEntries_Success_BackwardCompatibleScrapWithoutReason()
    {
        var (wo, _, _, _) = await ScenarioAsync();

        var stopped = (await Api.StopAsync(wo, good: 8, scrap: 2)).Ok;

        stopped.Sessions.Single().ScrapEntries.Should().ContainSingle().Which.ScrapReasonId.Should().BeNull();
        (await Api.GetScrapReportAsync()).Ok.ByReason.Single(r => r.ScrapReasonId == null).Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Correction_OpenOrder_Success_TraceAndTotalsAndStatusesUnchanged()
    {
        var (wo, orderId, _, _) = await ScenarioAsync(steps: 2);
        var reason = await Api.CreateScrapReasonAsync("GRAFFIO");
        var stopped = (await Api.StopWithScrapAsync(wo, 6, 4, E(null, 4))).Ok;
        var session = stopped.Sessions.Single().Id;

        var corrected = (await Api.CorrectAsync(wo, session, 7, 3, "Riconteggio", E(reason.Id, 3))).Ok;

        corrected.Status.Should().Be(WorkOrderStatus.Ended);
        corrected.GoodQuantity.Should().Be(7);
        corrected.Sessions.Single().Corrections.Should().ContainSingle().Which.PreviousScrapDetail.Should().Be("Senza motivo: 4");
        (await Api.GetOrderAsync(orderId)).Ok.Status.Should().Be(ProductionOrderStatus.InProgress);
        (await Api.GetScrapReportAsync()).Ok.ScrapQuantity.Should().Be(3);
        (await Api.CorrectAsync(wo, session, 7, 3, " ")).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Correction_CompletedOrder_Failure_Conflict409()
    {
        var (wo, orderId, _, _) = await ScenarioAsync();
        var stopped = (await Api.StopAsync(wo, good: 10)).Ok;
        (await Api.GetOrderAsync(orderId)).Ok.Status.Should().Be(ProductionOrderStatus.Completed);

        var result = await Api.CorrectAsync(wo, stopped.Sessions.Single().Id, 9, 1, "Errore");

        result.Status.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Report_PeriodFilter_Success_ByEndedAt()
    {
        var (wo, _, _, _) = await ScenarioAsync();
        await Api.StopAsync(wo, good: 8, scrap: 2);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        (await Api.GetScrapReportAsync($"?from={today}&to={today}")).Ok.ScrapQuantity.Should().Be(2);
        (await Api.GetScrapReportAsync("?from=2000-01-01&to=2000-01-31")).Ok.ScrapQuantity.Should().Be(0);
    }
}
