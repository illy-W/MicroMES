using System.Net;
using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

/// <summary>Manual move of a work order over HTTP: preview, apply, 422 and 409.</summary>
public class PlanMoveApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    /// <summary>Local Rome time of the first given weekday at least 3 weeks ahead (avoids "now" and most holidays).</summary>
    private static DateTime Future(DayOfWeek day, int hour)
    {
        var d = DateTime.UtcNow.Date.AddDays(21);
        while (d.DayOfWeek != day) d = d.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(new DateTime(d.Year, d.Month, d.Day, hour, 0, 0, DateTimeKind.Unspecified), Rome);
    }

    private int _otherSegment;

    private async Task<List<WorkOrderModel>> OrderAsync()
    {
        var seg = await Api.CreateSegmentAsync("SEG", setup: 60);
        await Api.CreateEquipmentAsync("M1", seg.Id);
        _otherSegment = (await Api.CreateSegmentAsync("OTH", setup: 30)).Id;
        await Api.CreateEquipmentAsync("M9", _otherSegment);
        var mat = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        await Api.PutLinearRoutingAsync(mat.Id, seg.Id, seg.Id);
        var order = await Api.CreateOrderAsync(mat.Id, 1);
        await Api.ReleaseAsync(order.Id);
        return (await Api.GetWorkOrdersAsync(order.Id)).Ok;
    }

    private Task<ApiResult<MovePreviewModel>> PreviewAsync(int id, DateTime start, int? machine = null)
        => Api.PostAsync<MovePreviewModel>($"/api/work-orders/{id}/move/preview", new MoveWorkOrderRequest { StartAt = start, EquipmentId = machine });

    private Task<ApiResult<MoveApplyModel>> MoveAsync(int id, DateTime start, int? machine = null, DateTime? expected = null)
        => Api.PostAsync<MoveApplyModel>($"/api/work-orders/{id}/move", new MoveWorkOrderRequest { StartAt = start, EquipmentId = machine, ExpectedUpdatedAt = expected });

    [Fact]
    public async Task Preview_WorkingStart_Success_ComputesEndAndSavesNothing()
    {
        var wo = (await OrderAsync())[0];

        var r = await PreviewAsync(wo.Id, Future(DayOfWeek.Wednesday, 9));

        r.Status.Should().Be(HttpStatusCode.OK);
        r.Ok.NewEnd.Should().Be(r.Ok.NewStart.AddHours(1));
        r.Ok.DurationMinutes.Should().Be(60);
        r.Ok.Equipment!.Code.Should().Be("M1");
        (await Api.GetWorkOrderAsync(wo.Id)).Ok.PlannedStartAt.Should().BeNull();
    }

    [Fact]
    public async Task Move_WorkingStart_Success_LocksAndWritesPlan()
    {
        var wo = (await OrderAsync())[0];
        var start = Future(DayOfWeek.Wednesday, 9);
        var preview = (await PreviewAsync(wo.Id, start)).Ok;

        var r = await MoveAsync(wo.Id, start, null, preview.ExpectedUpdatedAt);

        r.Status.Should().Be(HttpStatusCode.OK);
        r.Ok.WorkOrder.IsPlanLocked.Should().BeTrue();
        r.Ok.WorkOrder.PlannedStartAt.Should().Be(start);
        r.Ok.WorkOrder.PlannedEndAt.Should().Be(preview.NewEnd);
        r.Ok.WorkOrder.AssignedEquipment!.Code.Should().Be("M1");
        r.Ok.WorkOrder.AllowedActions.Should().Contain("Unlock");
    }

    [Fact]
    public async Task Preview_StartInThePast_Failure_422()
    {
        var wo = (await OrderAsync())[0];

        var r = await PreviewAsync(wo.Id, DateTime.UtcNow.AddDays(-1));

        r.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        r.Raw.Should().Contain("L'inizio deve essere dopo adesso.");
    }

    [Fact]
    public async Task Preview_Sunday_Failure_422WithSuggestion()
    {
        var wo = (await OrderAsync())[0];

        var r = await PreviewAsync(wo.Id, Future(DayOfWeek.Sunday, 10));

        r.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        r.Raw.Should().Contain("M1").And.Contain("Prossimo orario disponibile");
    }

    [Fact]
    public async Task Move_MachineOfAnotherPhase_Failure_422()
    {
        var wo = (await OrderAsync())[0];
        var other = (await Api.GetAsync<List<MicroMES.ApiFeatures.EquipmentFeature.Models.EquipmentModel>>("/api/equipment")).Ok.Single(e => e.Code == "M9");

        var r = await MoveAsync(wo.Id, Future(DayOfWeek.Wednesday, 9), other.Id);

        r.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        r.Raw.Should().Contain("non è collegata alla fase");
        (await Api.GetWorkOrderAsync(wo.Id)).Ok.IsPlanLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Move_Running_Failure_409NamesStatus()
    {
        var wo = (await OrderAsync())[0];
        await Api.StartAsync(wo.Id);

        var r = await MoveAsync(wo.Id, Future(DayOfWeek.Wednesday, 9));

        r.Status.Should().Be(HttpStatusCode.Conflict);
        r.Raw.Should().Contain("In esecuzione");
    }

    [Fact]
    public async Task Move_ChangedSincePreview_Failure_409()
    {
        var wo = (await OrderAsync())[0];
        var start = Future(DayOfWeek.Wednesday, 9);
        var preview = (await PreviewAsync(wo.Id, start)).Ok;
        (await MoveAsync(wo.Id, start, null, preview.ExpectedUpdatedAt)).Status.Should().Be(HttpStatusCode.OK);

        var again = await MoveAsync(wo.Id, Future(DayOfWeek.Thursday, 9), null, preview.ExpectedUpdatedAt);

        again.Status.Should().Be(HttpStatusCode.Conflict);
        again.Raw.Should().Contain("Il lavoro è cambiato dopo l'anteprima.");
    }

    [Fact]
    public async Task Move_UnknownWorkOrder_Failure_404()
        => (await MoveAsync(999999, Future(DayOfWeek.Wednesday, 9))).Status.Should().Be(HttpStatusCode.NotFound);
}
