using Microsoft.AspNetCore.Http;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Models;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Services;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.ProcessSegmentFeature.Controllers;

[ApiController]
[Route("api/process-segments")]
[Tags("ProcessSegments")]
[Produces("application/json")]
public class ProcessSegmentsController(ProcessSegmentService service) : ControllerBase
{
    [HttpGet(Name = "GetProcessSegments")]
    [ProducesResponseType<List<ProcessSegmentModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProcessSegments([FromQuery] bool includeInactive = false)
    {
        var result = await service.GetAllAsync(includeInactive);
        return result.IsSuccessful ? Ok(result.Data) : Problem(string.Join("; ", result.Errors));
    }

    [HttpGet("{id:int}", Name = "GetProcessSegment")]
    [ProducesResponseType<ProcessSegmentModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProcessSegment(int id)
        => ToResponse(await service.GetAsync(id), r => Ok(r.Data));

    [HttpPost(Name = "CreateProcessSegment")]
    [ProducesResponseType<ProcessSegmentModel>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateProcessSegment([FromBody] CreateProcessSegmentRequest request)
        => ToResponse(await service.CreateAsync(request),
            r => CreatedAtRoute("GetProcessSegment", new { id = r.Data!.Id }, r.Data));

    [HttpPut("{id:int}", Name = "UpdateProcessSegment")]
    [ProducesResponseType<ProcessSegmentModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProcessSegment(int id, [FromBody] UpdateProcessSegmentRequest request)
        => ToResponse(await service.UpdateAsync(id, request), r => Ok(r.Data));

    [HttpDelete("{id:int}", Name = "DeactivateProcessSegment")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateProcessSegment(int id)
        => ToResponse(await service.DeactivateAsync(id), _ => NoContent());

    private IActionResult ToResponse(ProcessSegmentResult<ProcessSegmentModel> result, Func<ProcessSegmentResult<ProcessSegmentModel>, IActionResult> success)
    {
        if (result.IsSuccessful) return success(result);

        var detail = string.Join("; ", result.Errors.Distinct());
        return result.Failure switch
        {
            ProcessSegmentFailure.NotFound => Problem(detail, statusCode: StatusCodes.Status404NotFound),
            ProcessSegmentFailure.Conflict => Problem(detail, statusCode: StatusCodes.Status409Conflict),
            ProcessSegmentFailure.Invalid => Problem(detail, statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Problem(detail, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
