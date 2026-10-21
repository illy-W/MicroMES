using illyW.Framework.Core.ResultPattern;

namespace MicroMES.ApiFeatures.EquipmentFeature.Services;

public enum EquipmentFailure
{
    None,
    NotFound,
    Conflict,
    Invalid
}

/// <summary>Result carrying the failure kind so the controller can map it to 404/409/422.</summary>
public class EquipmentResult<T> : Result<T>
{
    public EquipmentFailure Failure { get; set; }

    public static EquipmentResult<T> Ok(T data)
    {
        var r = new EquipmentResult<T>();
        r.Succeed(data);
        return r;
    }

    public static EquipmentResult<T> Failed(EquipmentFailure failure, string message, IEnumerable<string>? errors = null)
    {
        var r = new EquipmentResult<T> { Failure = failure };
        r.Fail(message);
        r.AddError(message);
        if (errors is not null) r.AddErrors(errors.ToList());
        return r;
    }
}
