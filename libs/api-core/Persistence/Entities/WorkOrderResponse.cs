using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>One work session of a work order (ISA-95 Job Response).</summary>
public class WorkOrderResponse : IEntity<int>
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public decimal GoodQuantity { get; set; }
    public decimal ScrapQuantity { get; set; }
    public string? Notes { get; set; }
    /// <summary>Machine actually used (ISA-95 equipment actual); null when the phase has no machines.</summary>
    public int? EquipmentId { get; set; }
    /// <summary>Snapshot taken when the session was opened (or corrected): history survives renames.</summary>
    public string? EquipmentCode { get; set; }
    public string? EquipmentName { get; set; }
}
