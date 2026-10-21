using MicroMES.ApiCore.Persistence.Violations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MicroMES.Api;

internal static class ConstraintViolationProblem
{
    public static ProblemDetails Create(ConstraintViolation violation) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = "Conflitto",
        Detail = violation.Message
    };
}

/// <summary>
/// Repositories swallow SaveChanges exceptions into failed results (so a 500 would follow): when the request
/// recorded a unique/foreign-key violation and the action answered 500, answer 409 with the friendly message.
/// </summary>
public sealed class ConstraintViolationResultFilter(
    ConstraintViolationTracker tracker, ILogger<ConstraintViolationResultFilter> logger) : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (tracker.Last is not { } violation) return;
        if (context.Result is not ObjectResult { StatusCode: StatusCodes.Status500InternalServerError }) return;

        logger.LogWarning("Constraint violation {Kind} on '{Constraint}' mapped to 409", violation.Kind, violation.ConstraintName);
        context.Result = new ObjectResult(ConstraintViolationProblem.Create(violation))
        {
            StatusCode = StatusCodes.Status409Conflict
        };
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}

/// <summary>Safety net for violations that escape as exceptions (the request scope disposal rolls the open transaction back).</summary>
public sealed class ConstraintViolationExceptionHandler(
    IProblemDetailsService problemDetails, ILogger<ConstraintViolationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (ConstraintViolation.From(exception) is not { } violation) return false;

        logger.LogWarning(exception, "Constraint violation {Kind} on '{Constraint}' mapped to 409", violation.Kind, violation.ConstraintName);
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = ConstraintViolationProblem.Create(violation),
            Exception = exception
        });
    }
}
