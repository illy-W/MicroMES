using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;
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
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetWorkOrders(
        [FromQuery] WorkOrderStatus? status,
        [FromQuery] int? productionOrderId,
        [FromQuery] string? search,
        [FromQuery] int? equipmentId,
        [FromQuery] string? sort,
        [FromQuery] PlanStatus? planStatus,
        [FromQuery] bool? locked)
        => ToResponse(await service.GetAllAsync(status, productionOrderId, search, equipmentId, sort, planStatus, locked));

    [HttpGet("{id:int}", Name = "GetWorkOrder")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkOrder(int id)
        => ToResponse(await service.GetAsync(id));

    [HttpPost("{id:int}/start", Name = "StartWorkOrder")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> StartWorkOrder(int id, [FromBody] OpenSessionRequest? request)
        => ToResponse(await service.StartAsync(id, request));

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
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ResumeWorkOrder(int id, [FromBody] OpenSessionRequest? request)
        => ToResponse(await service.ResumeAsync(id, request));

    [HttpPost("{id:int}/stop", Name = "StopWorkOrder")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> StopWorkOrder(int id, [FromBody] CloseSessionRequest request)
        => ToResponse(await service.StopAsync(id, request));

    /// <summary>Assigns the planned machine (null removes it). Only for Waiting, Ready and Interrupted work orders (409 otherwise); not a state transition.</summary>
    [HttpPut("{id:int}/equipment", Name = "AssignWorkOrderEquipment")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignWorkOrderEquipment(int id, [FromBody] AssignEquipmentRequest request)
        => ToResponse(await service.AssignEquipmentAsync(id, request));

    /// <summary>Locks the current plan of the work order (no body). 409 when not planned or status/order not allowed.</summary>
    [HttpPost("{id:int}/lock", Name = "LockWorkOrderPlan")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LockWorkOrderPlan(int id)
        => ToResponse(await service.LockPlanAsync(id));

    /// <summary>Removes the plan lock; changes nothing else. 409 when not locked.</summary>
    [HttpPost("{id:int}/unlock", Name = "UnlockWorkOrderPlan")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UnlockWorkOrderPlan(int id)
        => ToResponse(await service.UnlockPlanAsync(id));

    [HttpPost("{id:int}/abort", Name = "AbortWorkOrder")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AbortWorkOrder(int id, [FromBody] AbortRequest? request)
        => ToResponse(await service.AbortAsync(id, request?.Notes));

    /// <summary>Corrects a closed session (new values, mandatory note). 409 when the order is Completed/Cancelled or the session is open.</summary>
    [HttpPost("{id:int}/responses/{responseId:int}/corrections", Name = "CorrectWorkOrderSession")]
    [ProducesResponseType<WorkOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CorrectWorkOrderSession(int id, int responseId, [FromBody] CorrectSessionRequest request)
        => ToResponse(await service.CorrectSessionAsync(id, responseId, request));

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
