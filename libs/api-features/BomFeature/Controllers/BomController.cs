using MicroMES.ApiFeatures.BomFeature.Models;
using MicroMES.ApiFeatures.BomFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.BomFeature.Controllers;

[ApiController]
[Tags("Bom")]
[Produces("application/json")]
public class BomController(BomService service) : ControllerBase
{
    [HttpGet("api/materials/{id:int}/bom", Name = "GetBom")]
    [ProducesResponseType<List<BomNodeModel>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBom(int id)
        => ToResponse(await service.GetTreeAsync(id), r => Ok(r.Data));

    [HttpPost("api/materials/{id:int}/bom/items", Name = "AddBomItem")]
    [ProducesResponseType<BomItemModel>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddBomItem(int id, [FromBody] CreateBomItemRequest request)
        => ToResponse(await service.AddAsync(id, request), r => Created($"api/bom-items/{r.Data!.Id}", r.Data));

    [HttpPut("api/bom-items/{id:int}", Name = "UpdateBomItem")]
    [ProducesResponseType<BomItemModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateBomItem(int id, [FromBody] UpdateBomItemRequest request)
        => ToResponse(await service.UpdateAsync(id, request), r => Ok(r.Data));

    [HttpDelete("api/bom-items/{id:int}", Name = "DeleteBomItem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBomItem(int id)
        => ToResponse(await service.DeleteAsync(id), _ => NoContent());

    private IActionResult ToResponse<T>(BomResult<T> result, Func<BomResult<T>, IActionResult> success)
    {
        if (result.IsSuccessful) return success(result);

        var detail = string.Join("; ", result.Errors.Distinct());
        return result.Failure switch
        {
            BomFailure.NotFound => Problem(detail, statusCode: StatusCodes.Status404NotFound),
            BomFailure.Conflict => Problem(detail, statusCode: StatusCodes.Status409Conflict),
            BomFailure.Invalid => Problem(detail, statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Problem(detail, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
