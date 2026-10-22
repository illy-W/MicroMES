using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.RoutingFeature.Models;

public class RoutingModel
{
    public required List<RoutingStepModel> Steps { get; set; }
    public required List<RoutingDependencyModel> Dependencies { get; set; }
    /// <summary>
    /// Set by PUT: BOM components that were linked to a step removed by the save and went back to automatic.
    /// Always empty on GET.
    /// </summary>
    public List<RoutingUnassignedComponentModel> UnassignedComponents { get; set; } = [];
}

/// <summary>BOM component consumed in a routing step.</summary>
public class RoutingStepComponentModel
{
    public required int BomItemId { get; set; }
    public required int MaterialId { get; set; }
    public required string MaterialCode { get; set; }
    public required string MaterialName { get; set; }
    public required UnitOfMeasure UnitOfMeasure { get; set; }
    /// <summary>Quantity per unit of the routing material.</summary>
    public required decimal Quantity { get; set; }
    /// <summary>True when the row has no explicit step and it falls on the first start step.</summary>
    public required bool IsAutomatic { get; set; }
}

/// <summary>Component whose link was reset because its step was removed.</summary>
public class RoutingUnassignedComponentModel
{
    public required int BomItemId { get; set; }
    public required int MaterialId { get; set; }
    public required string MaterialCode { get; set; }
    public required string MaterialName { get; set; }
    public required int PreviousStepId { get; set; }
    public required int PreviousOperationNumber { get; set; }
    public required string PreviousProcessSegmentName { get; set; }
}

public class RoutingStepModel
{
    public required int Id { get; set; }
    public required int ProcessSegmentId { get; set; }
    public required string ProcessSegmentCode { get; set; }
    public required string ProcessSegmentName { get; set; }
    public required decimal StandardSetupMinutes { get; set; }
    public required decimal StandardRunMinutesPerUnit { get; set; }
    public required int OperationNumber { get; set; }
    public decimal? SetupMinutes { get; set; }
    public decimal? RunMinutesPerUnit { get; set; }
    public required decimal EffectiveSetupMinutes { get; set; }
    public required decimal EffectiveRunMinutesPerUnit { get; set; }
    public string? Notes { get; set; }
    public double? PositionX { get; set; }
    public double? PositionY { get; set; }
    public List<RoutingStepComponentModel> ConsumedComponents { get; set; } = [];
    /// <summary>Toolings required by the step, ordered by code.</summary>
    public List<RoutingStepToolingModel> Tooling { get; set; } = [];
    /// <summary>Setup attribute values of the step, ordered by attribute code.</summary>
    public List<RoutingStepSetupValueModel> SetupValues { get; set; } = [];
}

public class RoutingStepToolingModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required bool IsActive { get; set; }
}

public class RoutingStepSetupValueModel
{
    public required int AttributeId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Value { get; set; }
}

public class RoutingDependencyModel
{
    public required int Id { get; set; }
    public required int PredecessorStepId { get; set; }
    public required int SuccessorStepId { get; set; }
}
