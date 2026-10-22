using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.PlanningFeature.Services;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.PlanningFeature.Controllers;

[ApiController]
[Route("api/equipment")]
[Tags("Equipment")]
[Produces("application/json")]
public class EquipmentOverviewController(EquipmentOverviewService service, EquipmentHistoryService history) : ControllerBase
{
    /// <summary>Read-only operational overview of a machine: state now, running work orders, queue (max 20), mounted state, load and alerts.</summary>
    [HttpGet("{id:int}/overview", Name = "GetEquipmentOverview")]
    [ProducesResponseType<EquipmentOverviewModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetEquipmentOverview(int id)
    {
        var result = await service.GetAsync(id);
        if (result.IsSuccessful) return Ok(result.Data);
        var detail = string.Join("; ", result.Errors.Distinct());
        return result.Failure switch
        {
            ProductionOrderFailure.NotFound => Problem(detail, statusCode: StatusCodes.Status404NotFound),
            ProductionOrderFailure.Invalid => Problem(detail, statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Problem(detail, statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>Recent history of a machine (real sessions): summary, scrap by reason and paged sessions. days in {7, 30, 90}.</summary>
    [HttpGet("{id:int}/history", Name = "GetEquipmentHistory")]
    [ProducesResponseType<EquipmentHistoryModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetEquipmentHistory(int id, [FromQuery] int days = 30, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await history.GetAsync(id, days, page, pageSize);
        if (result.IsSuccessful) return Ok(result.Data);
        var detail = string.Join("; ", result.Errors.Distinct());
        return result.Failure switch
        {
            ProductionOrderFailure.NotFound => Problem(detail, statusCode: StatusCodes.Status404NotFound),
            ProductionOrderFailure.Invalid => Problem(detail, statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Problem(detail, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
