using illyW.Framework.Core.ResultPattern;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Services;

public enum ProductionOrderFailure
{
    None,
    NotFound,
    Conflict,
    Invalid
}

/// <summary>Result carrying the failure kind so the controller can map it to 404/409/422.</summary>
public class ProductionOrderResult<T> : Result<T>
{
    public ProductionOrderFailure Failure { get; set; }

    public static ProductionOrderResult<T> Ok(T data)
    {
        var r = new ProductionOrderResult<T>();
        r.Succeed(data);
        return r;
    }

    public static ProductionOrderResult<T> Failed(ProductionOrderFailure failure, string message)
    {
        var r = new ProductionOrderResult<T> { Failure = failure };
        r.Fail(message);
        r.AddError(message);
        return r;
    }

    public static ProductionOrderResult<T> NotFound(string message) => Failed(ProductionOrderFailure.NotFound, message);
    public static ProductionOrderResult<T> Conflict(string message) => Failed(ProductionOrderFailure.Conflict, message);
    public static ProductionOrderResult<T> Invalid(string message) => Failed(ProductionOrderFailure.Invalid, message);
}
