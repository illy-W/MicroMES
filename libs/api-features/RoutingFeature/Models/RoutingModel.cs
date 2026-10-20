namespace MicroMES.ApiFeatures.RoutingFeature.Models;

public class RoutingModel
{
    public required List<RoutingStepModel> Steps { get; set; }
    public required List<RoutingDependencyModel> Dependencies { get; set; }
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
}

public class RoutingDependencyModel
{
    public required int Id { get; set; }
    public required int PredecessorStepId { get; set; }
    public required int SuccessorStepId { get; set; }
}
