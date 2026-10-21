using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>
/// Scrapped quantity of a session with its reason. ScrapReasonId null (and no snapshot) means "no reason".
/// The code and name are a snapshot taken when the row is written.
/// </summary>
public class WorkOrderResponseScrap : IEntity<int>
{
    public int Id { get; set; }
    public int WorkOrderResponseId { get; set; }
    public int? ScrapReasonId { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonName { get; set; }
    public decimal Quantity { get; set; }
}
