using MicroMES.ApiFeatures.ScrapFeature.Models;
using MicroMES.ApiFeatures.ScrapFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.ScrapFeature.Controllers;

[ApiController]
[Route("api/scrap-reasons")]
[Tags("ScrapReasons")]
[Produces("application/json")]
public class ScrapReasonsController(ScrapReasonService service) : ControllerBase
{
    /// <summary>With processSegmentId only the reasons valid for that segment (linked to it or to no segment).</summary>
    [HttpGet(Name = "GetScrapReasons")]
    [ProducesResponseType<List<ScrapReasonModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScrapReasons([FromQuery] bool includeInactive = false, [FromQuery] int? processSegmentId = null)
        => ToResponse(await service.GetAllAsync(includeInactive, processSegmentId), r => Ok(r.Data));

    [HttpGet("{id:int}", Name = "GetScrapReason")]
    [ProducesResponseType<ScrapReasonModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScrapReason(int id)
        => ToResponse(await service.GetAsync(id), r => Ok(r.Data));

    [HttpPost(Name = "CreateScrapReason")]
    [ProducesResponseType<ScrapReasonModel>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateScrapReason([FromBody] CreateScrapReasonRequest request)
        => ToResponse(await service.CreateAsync(request), r => CreatedAtRoute("GetScrapReason", new { id = r.Data!.Id }, r.Data));

    [HttpPut("{id:int}", Name = "UpdateScrapReason")]
    [ProducesResponseType<ScrapReasonModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateScrapReason(int id, [FromBody] UpdateScrapReasonRequest request)
        => ToResponse(await service.UpdateAsync(id, request), r => Ok(r.Data));

    [HttpDelete("{id:int}", Name = "DeactivateScrapReason")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateScrapReason(int id)
        => ToResponse(await service.DeactivateAsync(id), _ => NoContent());

    private IActionResult ToResponse<T>(ScrapResult<T> result, Func<ScrapResult<T>, IActionResult> success)
    {
        if (result.IsSuccessful) return success(result);

        var detail = string.Join("; ", result.Errors.Distinct());
        return result.Failure switch
        {
            ScrapFailure.NotFound => Problem(detail, statusCode: StatusCodes.Status404NotFound),
            ScrapFailure.Conflict => Problem(detail, statusCode: StatusCodes.Status409Conflict),
            ScrapFailure.Invalid => Problem(detail, statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Problem(detail, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
