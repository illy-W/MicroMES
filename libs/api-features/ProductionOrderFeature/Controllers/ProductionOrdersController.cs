using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Controllers;

[ApiController]
[Route("api/production-orders")]
[Tags("ProductionOrders")]
[Produces("application/json")]
public class ProductionOrdersController(ProductionOrderService service) : ControllerBase
{
    [HttpGet(Name = "GetProductionOrders")]
    [ProducesResponseType<List<ProductionOrderModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductionOrders(
        [FromQuery] ProductionOrderStatus? status,
        [FromQuery] int? materialId,
        [FromQuery] string? search,
        [FromQuery] bool includeChildren = false)
        => ToResponse(await service.GetAllAsync(status, materialId, search, includeChildren), r => Ok(r.Data));

    [HttpGet("{id:int}", Name = "GetProductionOrder")]
    [ProducesResponseType<ProductionOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductionOrder(int id)
        => ToResponse(await service.GetAsync(id), r => Ok(r.Data));

    [HttpPost(Name = "CreateProductionOrder")]
    [ProducesResponseType<ProductionOrderDetailModel>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateProductionOrder([FromBody] CreateProductionOrderRequest request)
        => ToResponse(await service.CreateAsync(request),
            r => CreatedAtRoute("GetProductionOrder", new { id = r.Data!.Id }, r.Data));

    [HttpPut("{id:int}", Name = "UpdateProductionOrder")]
    [ProducesResponseType<ProductionOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProductionOrder(int id, [FromBody] UpdateProductionOrderRequest request)
        => ToResponse(await service.UpdateAsync(id, request), r => Ok(r.Data));

    [HttpPost("{id:int}/release", Name = "ReleaseProductionOrder")]
    [ProducesResponseType<ProductionOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReleaseProductionOrder(int id)
        => ToResponse(await service.ReleaseAsync(id), r => Ok(r.Data));

    [HttpPost("{id:int}/cancel", Name = "CancelProductionOrder")]
    [ProducesResponseType<ProductionOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelProductionOrder(int id)
        => ToResponse(await service.CancelAsync(id), r => Ok(r.Data));

    [HttpPost("{id:int}/abort", Name = "AbortProductionOrder")]
    [ProducesResponseType<ProductionOrderDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AbortProductionOrder(int id, [FromBody] AbortRequest? request)
        => ToResponse(await service.AbortAsync(id, request?.Notes), r => Ok(r.Data));

    private IActionResult ToResponse<T>(ProductionOrderResult<T> result, Func<ProductionOrderResult<T>, IActionResult> success)
    {
        if (result.IsSuccessful) return success(result);

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
