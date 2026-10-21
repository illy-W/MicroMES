using illyW.Framework.Core.ResultPattern;

namespace MicroMES.ApiFeatures.CalendarFeature.Services;

public enum CalendarFailure
{
    None,
    NotFound,
    Conflict,
    Invalid
}

/// <summary>Result carrying the failure kind so the controller can map it to 404/409/422.</summary>
public class CalendarResult<T> : Result<T>
{
    public CalendarFailure Failure { get; set; }

    public static CalendarResult<T> Ok(T data)
    {
        var r = new CalendarResult<T>();
        r.Succeed(data);
        return r;
    }

    public static CalendarResult<T> Failed(CalendarFailure failure, string message, IEnumerable<string>? errors = null)
    {
        var r = new CalendarResult<T> { Failure = failure };
        r.Fail(message);
        r.AddError(message);
        if (errors is not null) r.AddErrors(errors.ToList());
        return r;
    }

    public static CalendarResult<T> NotFound(string message) => Failed(CalendarFailure.NotFound, message);
    public static CalendarResult<T> Conflict(string message) => Failed(CalendarFailure.Conflict, message);
    public static CalendarResult<T> Invalid(string message) => Failed(CalendarFailure.Invalid, message);
}
