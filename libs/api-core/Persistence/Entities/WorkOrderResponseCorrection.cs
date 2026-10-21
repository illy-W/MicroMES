using illyW.Framework.Core.GenericEntityPattern;

namespace MicroMES.ApiCore.Persistence.Entities;

/// <summary>Trace of a correction of a closed session: previous and new values (reasons as readable text).</summary>
public class WorkOrderResponseCorrection : IEntity<int>
{
    public int Id { get; set; }
    public int WorkOrderResponseId { get; set; }
    public DateTime CorrectedAt { get; set; }
    public string Notes { get; set; } = string.Empty;
    public decimal PreviousGoodQuantity { get; set; }
    public decimal PreviousScrapQuantity { get; set; }
    /// <summary>E.g. "Graffio: 2; Senza motivo: 1".</summary>
    public string? PreviousScrapDetail { get; set; }
    public decimal NewGoodQuantity { get; set; }
    public decimal NewScrapQuantity { get; set; }
    public string? NewScrapDetail { get; set; }
}
