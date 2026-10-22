using MicroMES.ApiFeatures.ToolingFeature.Models;
using MicroMES.ApiFeatures.ToolingFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.ToolingFeature.Controllers;

[ApiController]
[Route("api/setup-attributes")]
[Tags("SetupAttributes")]
[Produces("application/json")]
public class SetupAttributeController(SetupAttributeService service) : ControllerBase
{
    /// <summary>q matches code or name; isActive missing = all.</summary>
    [HttpGet(Name = "GetSetupAttributesList")]
    [ProducesResponseType<List<SetupAttributeModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSetupAttributesList([FromQuery] string? q = null, [FromQuery] bool? isActive = null)
        => this.ToResponse(await service.GetAllAsync(q, isActive), r => Ok(r.Data));

    [HttpGet("{id:int}", Name = "GetSetupAttribute")]
    [ProducesResponseType<SetupAttributeModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSetupAttribute(int id)
        => this.ToResponse(await service.GetAsync(id), r => Ok(r.Data));

    [HttpPost(Name = "CreateSetupAttribute")]
    [ProducesResponseType<SetupAttributeModel>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateSetupAttribute([FromBody] CreateSetupAttributeRequest request)
        => this.ToResponse(await service.CreateAsync(request), r => CreatedAtRoute("GetSetupAttribute", new { id = r.Data!.Id }, r.Data));

    [HttpPut("{id:int}", Name = "UpdateSetupAttribute")]
    [ProducesResponseType<SetupAttributeModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateSetupAttribute(int id, [FromBody] UpdateSetupAttributeRequest request)
        => this.ToResponse(await service.UpdateAsync(id, request), r => Ok(r.Data));

    /// <summary>Soft-delete (isActive = false). Idempotent.</summary>
    [HttpDelete("{id:int}", Name = "DeactivateSetupAttribute")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateSetupAttribute(int id)
        => this.ToResponse(await service.DeactivateAsync(id), _ => NoContent());

    [HttpPost("{id:int}/reactivate", Name = "ReactivateSetupAttribute")]
    [ProducesResponseType<SetupAttributeModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateSetupAttribute(int id)
        => this.ToResponse(await service.ReactivateAsync(id), r => Ok(r.Data));
}
