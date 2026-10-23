using MicroMES.ApiFeatures.VersionFeature.Models;
using MicroMES.ApiFeatures.VersionFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.VersionFeature.Controllers;

[ApiController]
[Route("api/version")]
[Tags("Version")]
[Produces("application/json")]
public class VersionController(VersionService service) : ControllerBase
{
    /// <summary>Installed version and update availability (never fails because of the network).</summary>
    [HttpGet(Name = "GetVersion")]
    [ProducesResponseType<VersionModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersion(CancellationToken ct)
    {
        var result = await service.GetVersionAsync(ct);
        return result.IsSuccessful ? Ok(result.Data) : Problem(string.Join("; ", result.Errors), statusCode: StatusCodes.Status500InternalServerError);
    }
}
