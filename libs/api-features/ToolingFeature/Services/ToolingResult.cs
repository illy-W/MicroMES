using illyW.Framework.Core.ResultPattern;

namespace MicroMES.ApiFeatures.ToolingFeature.Services;

public enum ToolingFailure
{
    None,
    NotFound,
    Conflict,
    Invalid
}

/// <summary>Result carrying the failure kind so the controllers can map it to 404/409/422.</summary>
public class ToolingResult<T> : Result<T>
{
    public ToolingFailure Failure { get; set; }

    public static ToolingResult<T> Ok(T data)
    {
        var r = new ToolingResult<T>();
        r.Succeed(data);
        return r;
    }

    public static ToolingResult<T> Failed(ToolingFailure failure, string message, IEnumerable<string>? errors = null)
    {
        var r = new ToolingResult<T> { Failure = failure };
        r.Fail(message);
        r.AddError(message);
        if (errors is not null) r.AddErrors(errors.ToList());
        return r;
    }
}
