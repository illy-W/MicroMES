using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.ToolingFeature.Services;

public static class ToolingResultMapper
{
    public static IActionResult ToResponse<T>(this ControllerBase c, ToolingResult<T> result, Func<ToolingResult<T>, IActionResult> success)
    {
        if (result.IsSuccessful) return success(result);

        var detail = string.Join("; ", result.Errors.Distinct());
        return result.Failure switch
        {
            ToolingFailure.NotFound => c.Problem(detail, statusCode: StatusCodes.Status404NotFound),
            ToolingFailure.Conflict => c.Problem(detail, statusCode: StatusCodes.Status409Conflict),
            ToolingFailure.Invalid => c.Problem(detail, statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => c.Problem(detail, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
