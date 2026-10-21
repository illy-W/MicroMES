using illyW.Framework.Core.ResultPattern;

namespace MicroMES.ApiFeatures.ScrapFeature.Services;

public enum ScrapFailure
{
    None,
    NotFound,
    Conflict,
    Invalid
}

/// <summary>Result carrying the failure kind so the controller can map it to 404/409/422.</summary>
public class ScrapResult<T> : Result<T>
{
    public ScrapFailure Failure { get; set; }

    public static ScrapResult<T> Ok(T data)
    {
        var r = new ScrapResult<T>();
        r.Succeed(data);
        return r;
    }

    public static ScrapResult<T> Failed(ScrapFailure failure, string message, IEnumerable<string>? errors = null)
    {
        var r = new ScrapResult<T> { Failure = failure };
        r.Fail(message);
        r.AddError(message);
        if (errors is not null) r.AddErrors(errors.ToList());
        return r;
    }
}
