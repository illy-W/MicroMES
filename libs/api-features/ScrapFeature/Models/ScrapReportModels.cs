namespace MicroMES.ApiFeatures.ScrapFeature.Models;

public class ScrapReportReasonModel
{
    public int? ScrapReasonId { get; set; }
    public string? Code { get; set; }
    /// <summary>"Senza motivo" for the rows with no reason.</summary>
    public required string Name { get; set; }
    public required decimal Quantity { get; set; }
    /// <summary>Share of the total scrap (0..1).</summary>
    public required decimal Share { get; set; }
}

public class ScrapReportSegmentModel
{
    public required int ProcessSegmentId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required decimal GoodQuantity { get; set; }
    public required decimal ScrapQuantity { get; set; }
    /// <summary>scrap / (good + scrap), 0..1.</summary>
    public required decimal ScrapRate { get; set; }
}

public class ScrapReportMaterialModel
{
    public required int MaterialId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required decimal GoodQuantity { get; set; }
    public required decimal ScrapQuantity { get; set; }
    public required decimal ScrapRate { get; set; }
}

public class ScrapReportModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public required decimal GoodQuantity { get; set; }
    public required decimal ScrapQuantity { get; set; }
    /// <summary>scrap / (good + scrap), 0..1 (0 when nothing was produced).</summary>
    public required decimal ScrapRate { get; set; }
    /// <summary>Descending by quantity; always contains the "Senza motivo" row (possibly 0).</summary>
    public required List<ScrapReportReasonModel> ByReason { get; set; }
    public required List<ScrapReportSegmentModel> ByProcessSegment { get; set; }
    public required List<ScrapReportMaterialModel> ByMaterial { get; set; }
}
