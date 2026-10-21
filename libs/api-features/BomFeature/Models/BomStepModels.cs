namespace MicroMES.ApiFeatures.BomFeature.Models;

/// <summary>Routing step of the parent material a BOM row points to.</summary>
public class BomStepModel
{
    public required int Id { get; set; }
    public required int OperationNumber { get; set; }
    public required string ProcessSegmentName { get; set; }
}

/// <summary>Step where the component is consumed: the explicit one, or the automatic default (first start step).</summary>
public class BomEffectiveStepModel
{
    public required int Id { get; set; }
    public required int OperationNumber { get; set; }
    public required string ProcessSegmentName { get; set; }
    /// <summary>True when the row has no explicit step and the first start step applies.</summary>
    public required bool IsAutomatic { get; set; }
}
