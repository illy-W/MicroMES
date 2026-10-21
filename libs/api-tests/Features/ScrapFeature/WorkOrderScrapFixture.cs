using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiFeatures.ScrapFeature.Models;
using MicroMES.ApiFeatures.ScrapFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.ScrapFeature;

/// <summary>Closing a session with scrap rows and correcting it, on in-memory SQLite.</summary>
public class WorkOrderScrapFixture : BaseFeatureFixture
{
    private int _segment, _otherSegment, _order, _wo, _graffio, _saldatura;

    public WorkOrderScrapFixture() => Build(ProductionOrderStatus.InProgress);

    /// <summary>One order with a running work order (open session) of the segment SEG; planned 10.</summary>
    private void Build(ProductionOrderStatus orderStatus)
    {
        var material = AddMaterial("PF");
        _segment = AddSegment("SEG");
        _otherSegment = AddSegment("ALT");
        Arrange(db =>
        {
            var now = DateTime.UtcNow;
            var graffio = new ScrapReason { Code = "GRAFFIO", Name = "Graffio", IsActive = true, CreatedAt = now, UpdatedAt = now };
            var sal = new ScrapReason { Code = "SAL", Name = "Saldatura", IsActive = true, CreatedAt = now, UpdatedAt = now };
            db.ScrapReasons.AddRange(graffio, sal);
            db.SaveChanges();
            db.ScrapReasonProcessSegments.Add(new ScrapReasonProcessSegment { ScrapReasonId = sal.Id, ProcessSegmentId = _otherSegment });
            _graffio = graffio.Id;
            _saldatura = sal.Id;

            var order = new ProductionOrder { Code = "OP-1", MaterialId = material, Quantity = 10, Status = orderStatus, CreatedAt = now, UpdatedAt = now };
            db.ProductionOrders.Add(order);
            db.SaveChanges();
            var wo = new WorkOrder
            {
                ProductionOrderId = order.Id, OperationNumber = 10, ProcessSegmentId = _segment, ProcessSegmentCode = "SEG",
                ProcessSegmentName = "SEG", PlannedQuantity = 10, Status = WorkOrderStatus.Running, StartedAt = now.AddHours(-2),
                CreatedAt = now, UpdatedAt = now
            };
            // A second work order that stays Waiting keeps the order open after the first one ends.
            db.WorkOrders.Add(new WorkOrder
            {
                ProductionOrderId = order.Id, OperationNumber = 20, ProcessSegmentId = _segment, ProcessSegmentCode = "SEG",
                ProcessSegmentName = "SEG", PlannedQuantity = 10, Status = WorkOrderStatus.Waiting, CreatedAt = now, UpdatedAt = now
            });
            db.WorkOrders.Add(wo);
            db.SaveChanges();
            db.WorkOrderResponses.Add(new WorkOrderResponse { WorkOrderId = wo.Id, StartedAt = now.AddHours(-2) });
            _order = order.Id;
            _wo = wo.Id;
        });
    }

    private static ScrapEntryRequest E(int? reason, decimal qty) => new() { ScrapReasonId = reason, Quantity = qty };

    private Task<ProductionOrderResult<WorkOrderDetailModel>> Stop(decimal good, decimal scrap, params ScrapEntryRequest[]? entries)
        => Service<WorkOrderService>().StopAsync(_wo, new CloseSessionRequest
        {
            GoodQuantity = good, ScrapQuantity = scrap, ScrapEntries = entries?.ToList()
        });

    [Fact]
    public async Task StopAsync_MultipleReasons_Success_RowsWithSnapshot()
    {
        var result = await Stop(7, 3, E(_graffio, 2), E(null, 1));

        result.IsSuccessful.Should().BeTrue();
        var session = result.Data!.Sessions.Single();
        session.ScrapQuantity.Should().Be(3);
        session.ScrapEntries.Should().HaveCount(2);
        session.ScrapEntries.Should().Contain(e => e.ReasonCode == "GRAFFIO" && e.ReasonName == "Graffio" && e.Quantity == 2);
        session.ScrapEntries.Should().Contain(e => e.ScrapReasonId == null && e.ReasonCode == null && e.Quantity == 1);
        result.Data.ShortfallQuantity.Should().Be(3);
    }

    [Fact]
    public async Task StopAsync_RenamedReason_Success_SnapshotKeepsOldName()
    {
        await Stop(7, 1, E(_graffio, 1));
        Arrange(db => db.ScrapReasons.Single(r => r.Id == _graffio).Name = "Rigatura");

        var detail = await Service<WorkOrderService>().GetAsync(_wo);

        detail.Data!.Sessions.Single().ScrapEntries.Single().ReasonName.Should().Be("Graffio");
    }

    [Fact]
    public async Task StopAsync_WithoutEntries_Success_ScrapWithoutReason()
    {
        var result = await Stop(7, 3);

        result.Data!.Sessions.Single().ScrapEntries.Should().ContainSingle()
            .Which.Should().Match<WorkOrderScrapEntryModel>(e => e.ScrapReasonId == null && e.Quantity == 3);
    }

    [Fact]
    public async Task StopAsync_NoScrap_Success_NoRows()
    {
        var result = await Stop(10, 0);

        result.Data!.Sessions.Single().ScrapEntries.Should().BeEmpty();
        result.Data.ShortfallQuantity.Should().Be(0);
    }

    [Fact]
    public async Task StopAsync_WrongSum_Failure_InvalidAndNothingChanges()
    {
        var result = await Stop(7, 3, E(_graffio, 1));

        result.Failure.Should().Be(ProductionOrderFailure.Invalid);
        Query(db => db.WorkOrders.Single(w => w.Id == _wo).Status).Should().Be(WorkOrderStatus.Running);
        Query(db => db.WorkOrderResponseScraps.Count()).Should().Be(0);
    }

    [Fact]
    public async Task StopAsync_ReasonOfAnotherSegment_Failure_Invalid()
    {
        var result = await Stop(7, 1, E(_saldatura, 1));

        result.Failure.Should().Be(ProductionOrderFailure.Invalid);
        result.Errors.Should().Contain(e => e.Contains("non è valido per la fase"));
    }

    [Fact]
    public async Task StopAsync_InactiveReason_Failure_Invalid()
    {
        Arrange(db => db.ScrapReasons.Single(r => r.Id == _graffio).IsActive = false);

        (await Stop(7, 1, E(_graffio, 1))).Failure.Should().Be(ProductionOrderFailure.Invalid);
    }

    [Fact]
    public async Task PauseAsync_WithEntries_Success_RowsOnClosedSession()
    {
        var result = await Service<WorkOrderService>().PauseAsync(_wo, new CloseSessionRequest
        {
            GoodQuantity = 2, ScrapQuantity = 1, ScrapEntries = [E(_graffio, 1)]
        });

        result.Data!.Status.Should().Be(WorkOrderStatus.Interrupted);
        result.Data.Sessions.Single().ScrapEntries.Single().ReasonCode.Should().Be("GRAFFIO");
    }

    // ---- corrections ----

    private Task<ProductionOrderResult<WorkOrderDetailModel>> Correct(int sessionId, decimal good, decimal scrap, string? notes, params ScrapEntryRequest[] entries)
        => Service<WorkOrderService>().CorrectSessionAsync(_wo, sessionId, new CorrectSessionRequest
        {
            GoodQuantity = good, ScrapQuantity = scrap, Notes = notes!, ScrapEntries = entries.ToList()
        });

    private int SessionId() => Query(db => db.WorkOrderResponses.Single(s => s.WorkOrderId == _wo).Id);

    [Fact]
    public async Task CorrectSessionAsync_ClosedSession_Success_TraceReplacedRowsAndTotals()
    {
        await Stop(7, 3, E(_graffio, 2), E(null, 1));

        var result = await Correct(SessionId(), 8, 2, " Riconteggio ", E(_graffio, 2));

        result.IsSuccessful.Should().BeTrue();
        var session = result.Data!.Sessions.Single();
        session.GoodQuantity.Should().Be(8);
        session.ScrapQuantity.Should().Be(2);
        session.IsCorrected.Should().BeTrue();
        session.ScrapEntries.Should().ContainSingle().Which.Quantity.Should().Be(2);
        var c = session.Corrections.Should().ContainSingle().Subject;
        c.Notes.Should().Be("Riconteggio");
        (c.PreviousGoodQuantity, c.PreviousScrapQuantity, c.NewGoodQuantity, c.NewScrapQuantity).Should().Be((7m, 3m, 8m, 2m));
        c.PreviousScrapDetail.Should().Contain("Graffio: 2").And.Contain("Senza motivo: 1");
        c.NewScrapDetail.Should().Be("Graffio: 2");
        result.Data.GoodQuantity.Should().Be(8);
        Query(db => db.WorkOrderResponseScraps.Count()).Should().Be(1);
    }

    [Fact]
    public async Task CorrectSessionAsync_ClosedSession_Success_StatusesAndProgressFollow()
    {
        await Stop(7, 0);
        var orderBefore = Query(db => db.ProductionOrders.Single(o => o.Id == _order).Status);

        await Correct(SessionId(), 10, 0, "Conteggio");

        Query(db => db.WorkOrders.Single(w => w.Id == _wo).Status).Should().Be(WorkOrderStatus.Ended);
        Query(db => db.ProductionOrders.Single(o => o.Id == _order).Status).Should().Be(orderBefore);
        var order = await Service<ProductionOrderService>().GetAsync(_order);
        order.Data!.ProducedQuantity.Should().Be(10);
        order.Data.ProgressPercent.Should().Be(100);
    }

    [Fact]
    public async Task CorrectSessionAsync_OpenSession_Failure_Conflict()
        => (await Correct(SessionId(), 1, 0, "x")).Failure.Should().Be(ProductionOrderFailure.Conflict);

    [Theory]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Cancelled)]
    public async Task CorrectSessionAsync_ClosedOrder_Failure_Conflict(ProductionOrderStatus status)
    {
        await Stop(7, 0);
        Arrange(db => db.ProductionOrders.Single(o => o.Id == _order).Status = status);

        var result = await Correct(SessionId(), 8, 0, "x");

        result.Failure.Should().Be(ProductionOrderFailure.Conflict);
        Query(db => db.WorkOrderResponses.Single().GoodQuantity).Should().Be(7);
    }

    [Fact]
    public async Task CorrectSessionAsync_MissingNote_Failure_Invalid()
    {
        await Stop(7, 0);

        (await Correct(SessionId(), 8, 0, "  ")).Failure.Should().Be(ProductionOrderFailure.Invalid);
        Query(db => db.WorkOrderResponseCorrections.Count()).Should().Be(0);
    }

    [Fact]
    public async Task CorrectSessionAsync_WrongSum_Failure_InvalidAndUnchanged()
    {
        await Stop(7, 1, E(_graffio, 1));

        var result = await Correct(SessionId(), 8, 3, "x", E(_graffio, 1));

        result.Failure.Should().Be(ProductionOrderFailure.Invalid);
        Query(db => db.WorkOrderResponses.Single().ScrapQuantity).Should().Be(1);
        Query(db => db.WorkOrderResponseCorrections.Count()).Should().Be(0);
    }

    [Fact]
    public async Task CorrectSessionAsync_SessionOfAnotherWorkOrder_Failure_NotFound()
    {
        await Stop(7, 0);

        (await Correct(9999, 1, 0, "x")).Failure.Should().Be(ProductionOrderFailure.NotFound);
    }

    // ---- report ----

    [Fact]
    public async Task GetReportAsync_ClosedSessions_Success_TotalsByReasonSegmentMaterial()
    {
        await Stop(7, 3, E(_graffio, 2), E(null, 1));

        var report = (await Service<ScrapReportService>().GetReportAsync(null, null)).Data!;

        report.GoodQuantity.Should().Be(7);
        report.ScrapQuantity.Should().Be(3);
        report.ScrapRate.Should().Be(0.3m);
        report.ByReason.Select(r => (r.Name, r.Quantity)).Should().Equal(("Graffio", 2m), ("Senza motivo", 1m));
        report.ByProcessSegment.Should().ContainSingle().Which.ScrapQuantity.Should().Be(3);
        report.ByMaterial.Should().ContainSingle().Which.Code.Should().Be("PF");
    }

    [Fact]
    public async Task GetReportAsync_AfterCorrection_Success_UsesCorrectedValues()
    {
        await Stop(7, 3, E(_graffio, 3));
        await Correct(SessionId(), 9, 1, "x", E(_graffio, 1));

        var report = (await Service<ScrapReportService>().GetReportAsync(null, null)).Data!;

        (report.GoodQuantity, report.ScrapQuantity).Should().Be((9m, 1m));
        report.ByReason.Single(r => r.ScrapReasonId == _graffio).Quantity.Should().Be(1);
    }

    [Fact]
    public async Task GetReportAsync_PeriodWithoutSessions_Success_EmptyWithNoReasonRow()
    {
        await Stop(7, 3);

        var report = (await Service<ScrapReportService>().GetReportAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-20))).Data!;

        report.ScrapQuantity.Should().Be(0);
        report.ScrapRate.Should().Be(0);
        report.ByReason.Should().ContainSingle().Which.Name.Should().Be("Senza motivo");
        report.ByProcessSegment.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_ToWithoutTime_Success_IncludesWholeDay()
    {
        await Stop(7, 3);

        var report = (await Service<ScrapReportService>().GetReportAsync(DateTime.UtcNow.Date, DateTime.UtcNow.Date)).Data!;

        report.ScrapQuantity.Should().Be(3);
    }

    [Fact]
    public async Task GetReportAsync_ToBeforeFrom_Failure_Invalid()
        => (await Service<ScrapReportService>().GetReportAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)))
            .Failure.Should().Be(ScrapFailure.Invalid);
}
