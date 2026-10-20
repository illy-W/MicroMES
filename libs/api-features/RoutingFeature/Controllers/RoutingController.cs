using MicroMES.ApiFeatures.RoutingFeature.Models;
using MicroMES.ApiFeatures.RoutingFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.RoutingFeature.Controllers;

[ApiController]
[Tags("Routing")]
[Produces("application/json")]
public class RoutingController(RoutingService service) : ControllerBase
{
    [HttpGet("api/materials/{id:int}/routing", Name = "GetRouting")]
    [ProducesResponseType<RoutingModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRouting(int id)
        => ToResponse(await service.GetAsync(id));

    [HttpPut("api/materials/{id:int}/routing", Name = "UpdateRouting")]
    [ProducesResponseType<RoutingModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateRouting(int id, [FromBody] UpdateRoutingRequest request)
        => ToResponse(await service.ReplaceAsync(id, request));

    private IActionResult ToResponse(RoutingResult<RoutingModel> result)
    {
        if (result.IsSuccessful) return Ok(result.Data);

        var detail = string.Join("; ", result.Errors.Distinct());
        return result.Failure switch
        {
            RoutingFailure.NotFound => Problem(detail, statusCode: StatusCodes.Status404NotFound),
            RoutingFailure.Invalid => Problem(detail, statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Problem(detail, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
