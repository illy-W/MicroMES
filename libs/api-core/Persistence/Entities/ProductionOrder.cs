using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

public enum ProductionOrderStatus
{
    Planned,
    Released,
    InProgress,
    Completed,
    Cancelled,
    Aborted
}

public class ProductionOrder : IEntity<int>
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int MaterialId { get; set; }
    public decimal Quantity { get; set; }
    public ProductionOrderStatus Status { get; set; }
    public int Priority { get; set; }
    public DateTime? RequestedStart { get; set; }
    public DateTime? RequestedEnd { get; set; }
    public string? Notes { get; set; }
    public int? ParentOrderId { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
