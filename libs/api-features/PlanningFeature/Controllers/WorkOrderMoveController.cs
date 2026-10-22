using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.PlanningFeature.Services;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.PlanningFeature.Controllers;

[ApiController]
[Route("api/work-orders")]
[Tags("WorkOrders")]
[Produces("application/json")]
public class WorkOrderMoveController(PlanLockService service) : ControllerBase
{
    /// <summary>Preview of a manual move (new start and optional machine): computed end and conflicts, nothing is saved.</summary>
    [HttpPost("{id:int}/move/preview", Name = "PreviewMoveWorkOrder")]
    [ProducesResponseType<MovePreviewModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Preview(int id, [FromBody] MoveWorkOrderRequest request)
        => ToResponse(await service.PreviewAsync(id, request));

    /// <summary>Applies the move in one transaction and locks the work order. Conflicts are returned, never blocking; successors are not moved.</summary>
    [HttpPost("{id:int}/move", Name = "MoveWorkOrder")]
    [ProducesResponseType<MoveApplyModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Move(int id, [FromBody] MoveWorkOrderRequest request)
        => ToResponse(await service.ApplyAsync(id, request));

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
