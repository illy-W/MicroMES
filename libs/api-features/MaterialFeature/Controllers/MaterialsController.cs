using Microsoft.AspNetCore.Http;
using MicroMES.ApiFeatures.MaterialFeature.Models;
using MicroMES.ApiFeatures.MaterialFeature.Services;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.MaterialFeature.Controllers;

[ApiController]
[Route("api/materials")]
[Tags("Materials")]
[Produces("application/json")]
public class MaterialsController(MaterialService service) : ControllerBase
{
    [HttpGet(Name = "GetMaterials")]
    [ProducesResponseType<List<MaterialModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMaterials([FromQuery] bool includeInactive = false)
    {
        var result = await service.GetAllAsync(includeInactive);
        return result.IsSuccessful ? Ok(result.Data) : Problem(string.Join("; ", result.Errors));
    }

    [HttpGet("{id:int}", Name = "GetMaterial")]
    [ProducesResponseType<MaterialModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMaterial(int id)
        => ToResponse(await service.GetAsync(id), r => Ok(r.Data));

    [HttpPost(Name = "CreateMaterial")]
    [ProducesResponseType<MaterialModel>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateMaterial([FromBody] CreateMaterialRequest request)
        => ToResponse(await service.CreateAsync(request),
            r => CreatedAtRoute("GetMaterial", new { id = r.Data!.Id }, r.Data));

    [HttpPut("{id:int}", Name = "UpdateMaterial")]
    [ProducesResponseType<MaterialModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateMaterial(int id, [FromBody] UpdateMaterialRequest request)
        => ToResponse(await service.UpdateAsync(id, request), r => Ok(r.Data));

    [HttpDelete("{id:int}", Name = "DeactivateMaterial")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateMaterial(int id)
        => ToResponse(await service.DeactivateAsync(id), _ => NoContent());

    private IActionResult ToResponse(MaterialResult<MaterialModel> result, Func<MaterialResult<MaterialModel>, IActionResult> success)
    {
        if (result.IsSuccessful) return success(result);

        var detail = string.Join("; ", result.Errors.Distinct());
        return result.Failure switch
        {
            MaterialFailure.NotFound => Problem(detail, statusCode: StatusCodes.Status404NotFound),
            MaterialFailure.Conflict => Problem(detail, statusCode: StatusCodes.Status409Conflict),
            _ => Problem(detail, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
