using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Controllers;

[ApiController]
[Route("api/work-orders")]
[Tags("WorkOrders")]
[Produces("application/json")]
public class WorkOrdersController(WorkOrderService service) : ControllerBase
{
    [HttpGet(Name = "GetWorkOrders")]
    [ProducesResponseType<List<WorkOrderModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkOrders(
        [FromQuery] WorkOrderStatus? status,
        [FromQuery] int? productionOrderId,
        [FromQuery] string? search)
        => ToResponse(await service.GetAllAsync(status, productionOrderId, search));

    [HttpGet("{id:int}", Name = "GetWorkOrder")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkOrder(int id)
        => ToResponse(await service.GetAsync(id));

    [HttpPost("{id:int}/start", Name = "StartWorkOrder")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartWorkOrder(int id)
        => ToResponse(await service.StartAsync(id));

    [HttpPost("{id:int}/pause", Name = "PauseWorkOrder")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PauseWorkOrder(int id, [FromBody] CloseSessionRequest request)
        => ToResponse(await service.PauseAsync(id, request));

    [HttpPost("{id:int}/resume", Name = "ResumeWorkOrder")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResumeWorkOrder(int id)
        => ToResponse(await service.ResumeAsync(id));

    [HttpPost("{id:int}/stop", Name = "StopWorkOrder")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> StopWorkOrder(int id, [FromBody] CloseSessionRequest request)
        => ToResponse(await service.StopAsync(id, request));

    [HttpPost("{id:int}/abort", Name = "AbortWorkOrder")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AbortWorkOrder(int id, [FromBody] AbortRequest? request)
        => ToResponse(await service.AbortAsync(id, request?.Notes));

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
