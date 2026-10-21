using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiFeatures.EquipmentFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.EquipmentFeature.Controllers;

[ApiController]
[Route("api/equipment")]
[Tags("Equipment")]
[Produces("application/json")]
public class EquipmentController(EquipmentService service) : ControllerBase
{
    /// <summary>With processSegmentId only the machines enabled for that segment; search matches code or name.</summary>
    [HttpGet(Name = "GetEquipmentList")]
    [ProducesResponseType<List<EquipmentModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEquipmentList([FromQuery] bool includeInactive = false,
        [FromQuery] string? search = null, [FromQuery] int? processSegmentId = null)
        => ToResponse(await service.GetAllAsync(includeInactive, search, processSegmentId), r => Ok(r.Data));

    [HttpGet("{id:int}", Name = "GetEquipment")]
    [ProducesResponseType<EquipmentModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEquipment(int id)
        => ToResponse(await service.GetAsync(id), r => Ok(r.Data));

    [HttpPost(Name = "CreateEquipment")]
    [ProducesResponseType<EquipmentModel>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateEquipment([FromBody] CreateEquipmentRequest request)
        => ToResponse(await service.CreateAsync(request), r => CreatedAtRoute("GetEquipment", new { id = r.Data!.Id }, r.Data));

    [HttpPut("{id:int}", Name = "UpdateEquipment")]
    [ProducesResponseType<EquipmentModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateEquipment(int id, [FromBody] UpdateEquipmentRequest request)
        => ToResponse(await service.UpdateAsync(id, request), r => Ok(r.Data));

    [HttpDelete("{id:int}", Name = "DeactivateEquipment")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateEquipment(int id)
        => ToResponse(await service.DeactivateAsync(id), _ => NoContent());

    private IActionResult ToResponse<T>(EquipmentResult<T> result, Func<EquipmentResult<T>, IActionResult> success)
    {
        if (result.IsSuccessful) return success(result);

        var detail = string.Join("; ", result.Errors.Distinct());
        return result.Failure switch
        {
            EquipmentFailure.NotFound => Problem(detail, statusCode: StatusCodes.Status404NotFound),
            EquipmentFailure.Conflict => Problem(detail, statusCode: StatusCodes.Status409Conflict),
            EquipmentFailure.Invalid => Problem(detail, statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Problem(detail, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
