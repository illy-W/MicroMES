using MicroMES.ApiFeatures.ToolingFeature.Models;
using MicroMES.ApiFeatures.ToolingFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.ToolingFeature.Controllers;

[ApiController]
[Route("api/tooling")]
[Tags("Tooling")]
[Produces("application/json")]
public class ToolingController(ToolingService service) : ControllerBase
{
    /// <summary>q matches code or name; isActive missing = all.</summary>
    [HttpGet(Name = "GetToolingList")]
    [ProducesResponseType<List<ToolingModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetToolingList([FromQuery] string? q = null, [FromQuery] bool? isActive = null)
        => this.ToResponse(await service.GetAllAsync(q, isActive), r => Ok(r.Data));

    [HttpGet("{id:int}", Name = "GetTooling")]
    [ProducesResponseType<ToolingDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTooling(int id)
        => this.ToResponse(await service.GetAsync(id), r => Ok(r.Data));

    [HttpPost(Name = "CreateTooling")]
    [ProducesResponseType<ToolingDetailModel>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateTooling([FromBody] CreateToolingRequest request)
        => this.ToResponse(await service.CreateAsync(request), r => CreatedAtRoute("GetTooling", new { id = r.Data!.Id }, r.Data));

    [HttpPut("{id:int}", Name = "UpdateTooling")]
    [ProducesResponseType<ToolingDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateTooling(int id, [FromBody] UpdateToolingRequest request)
        => this.ToResponse(await service.UpdateAsync(id, request), r => Ok(r.Data));

    /// <summary>Soft-delete (isActive = false). Idempotent.</summary>
    [HttpDelete("{id:int}", Name = "DeactivateTooling")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateTooling(int id)
        => this.ToResponse(await service.DeactivateAsync(id), _ => NoContent());

    [HttpPost("{id:int}/reactivate", Name = "ReactivateTooling")]
    [ProducesResponseType<ToolingDetailModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateTooling(int id)
        => this.ToResponse(await service.ReactivateAsync(id), r => Ok(r.Data));
}
