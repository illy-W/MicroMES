using illyW.Framework.Core.ResultPattern;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Services;

internal static class Persist
{
    /// <summary>Throws when a repository call failed: the calling command rolls the transaction back.</summary>
    public static T Ensure<T>(IResult<T> result, string message)
    {
        if (!result.IsSuccessful || result.Data is null)
            throw new InvalidOperationException($"{message} {string.Join("; ", result.Errors)}".Trim());
        return result.Data;
    }
}
