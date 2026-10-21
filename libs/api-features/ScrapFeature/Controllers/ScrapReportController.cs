using MicroMES.ApiFeatures.ScrapFeature.Models;
using MicroMES.ApiFeatures.ScrapFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.ScrapFeature.Controllers;

[ApiController]
[Route("api/scrap")]
[Tags("Scrap")]
[Produces("application/json")]
public class ScrapReportController(ScrapReportService service) : ControllerBase
{
    /// <summary>
    /// Scrap report over the sessions closed in the period (EndedAt, UTC). "from" is inclusive; "to" is inclusive
    /// too, and a "to" with no time of day covers the whole day.
    /// </summary>
    [HttpGet("report", Name = "GetScrapReport")]
    [ProducesResponseType<ScrapReportModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetScrapReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var result = await service.GetReportAsync(from, to);
        return result.IsSuccessful
            ? Ok(result.Data)
            : Problem(string.Join("; ", result.Errors.Distinct()), statusCode: StatusCodes.Status422UnprocessableEntity);
    }
}
