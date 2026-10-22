namespace MicroMES.ApiFeatures.PlanningFeature.Models;

public class EquipmentHistorySummaryModel
{
    /// <summary>Distinct work orders with sessions on this machine in the period.</summary>
    public required int WorkOrders { get; set; }
    /// <summary>Of those, the ones in Ended status.</summary>
    public required int CompletedWorkOrders { get; set; }
    public required decimal WorkedMinutes { get; set; }
    public required decimal AvailableMinutes { get; set; }
    /// <summary>Worked / available * 100, raw (may exceed 100); null when there is no available time.</summary>
    public decimal? UtilizationPercent { get; set; }
    public required decimal GoodQuantity { get; set; }
    public required decimal ScrapQuantity { get; set; }
    /// <summary>Scrap / (good + scrap) * 100; null when both are zero.</summary>
    public decimal? ScrapPercent { get; set; }
}

public class EquipmentScrapByReasonModel
{
    public int? ReasonId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required decimal Quantity { get; set; }
}

public class EquipmentHistorySessionModel
{
    public required int SessionId { get; set; }
    public required int WorkOrderId { get; set; }
    public required string WorkOrderCode { get; set; }
    public required int ProductionOrderId { get; set; }
    public required string ProductionOrderCode { get; set; }
    public required string ProcessSegmentName { get; set; }
    public required DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public required decimal DurationMinutes { get; set; }
    public required decimal GoodQuantity { get; set; }
    public required decimal ScrapQuantity { get; set; }
    public required bool IsRunning { get; set; }
}

public class EquipmentHistorySessionPageModel
{
    public required List<EquipmentHistorySessionModel> Items { get; set; }
    public required int Page { get; set; }
    public required int PageSize { get; set; }
    public required int TotalCount { get; set; }
}

public class EquipmentHistoryModel
{
    public required int EquipmentId { get; set; }
    public required int Days { get; set; }
    public required DateTime PeriodStart { get; set; }
    public required DateTime PeriodEnd { get; set; }
    public required EquipmentHistorySummaryModel Summary { get; set; }
    public required List<EquipmentScrapByReasonModel> ScrapByReason { get; set; }
    public required EquipmentHistorySessionPageModel Sessions { get; set; }
}
