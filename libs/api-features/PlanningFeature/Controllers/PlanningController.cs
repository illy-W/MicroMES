using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.PlanningFeature.Services;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.PlanningFeature.Controllers;

[ApiController]
[Route("api/planning")]
[Tags("Planning")]
[Produces("application/json")]
public class PlanningController(PlanningService service, TimelineService timeline) : ControllerBase
{
    /// <summary>Proposed plan for the scope, without saving anything.</summary>
    [HttpPost("preview", Name = "PreviewPlan")]
    [ProducesResponseType<PlanningPreviewModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Preview([FromBody] PlanningRequest request)
        => ToResponse(await service.PreviewAsync(request));

    /// <summary>Computes and saves the plan of the scope in one transaction. <c>previewToken</c> (optional) tells whether it differs from the preview.</summary>
    [HttpPost("apply", Name = "ApplyPlan")]
    [ProducesResponseType<PlanningApplyModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Apply([FromBody] PlanningRequest request)
        => ToResponse(await service.ApplyAsync(request));

    /// <summary>Clears the plan of the open work orders of the order and its children (the assigned machines stay).</summary>
    [HttpDelete("orders/{id:int}", Name = "RemoveOrderPlan")]
    [ProducesResponseType<RemovePlanModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveOrderPlan(int id)
        => ToResponse(await service.RemovePlanAsync(id));

    /// <summary>Alerts on the current plan: late orders, unplannable, stale plans, overloaded or disabled machines.</summary>
    [HttpGet("alerts", Name = "GetPlanAlerts")]
    [ProducesResponseType<PlanAlertsModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlerts()
        => ToResponse(await service.GetAlertsAsync());

    /// <summary>
    /// Read-only timeline of the window [from, to) (UTC, max 62 days): machines, work orders with plan or real dates, non-working spans and load per day
    /// (windows up to 14 days) or per week. Filters: machine, status, order.
    /// </summary>
    [HttpGet("timeline", Name = "GetPlanningTimeline")]
    [ProducesResponseType<TimelineModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetTimeline(
        [FromQuery] DateTime from, [FromQuery] DateTime to,
        [FromQuery] int? equipmentId, [FromQuery] WorkOrderStatus? status, [FromQuery] int? productionOrderId)
        => ToResponse(await timeline.GetAsync(from, to, equipmentId, status, productionOrderId));

    private IActionResult ToResponse<T>(ProductionOrderResult<T> result)
    {
        if (result.IsSuccessful) return Ok(result.Data);
        var detail = string.Join("; ", result.Errors.Distinct());
        return result.Failure switch
        {
            ProductionOrderFailure.NotFound => Problem(detail, statusCode: StatusCodes.Status404NotFound),
            ProductionOrderFailure.Conflict => Problem(detail, statusCode: StatusCodes.Status409Conflict),
            ProductionOrderFailure.Invalid => Problem(detail, statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Problem(detail, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
