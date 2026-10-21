using Npgsql;

namespace MicroMES.ApiCore.Persistence.Violations;

public enum ConstraintViolationKind
{
    Unique,
    ForeignKey
}

/// <summary>A database constraint violation caused by user input (PostgreSQL 23505 / 23503).</summary>
public sealed record ConstraintViolation(ConstraintViolationKind Kind, string? ConstraintName)
{
    public const string UniqueSqlState = PostgresErrorCodes.UniqueViolation;
    public const string ForeignKeySqlState = PostgresErrorCodes.ForeignKeyViolation;

    private const string GenericUnique = "Il valore inserito esiste già.";
    private const string GenericForeignKey = "Il riferimento indicato non esiste oppure è ancora in uso.";

    // Prefixes of the (possibly truncated to 63 chars by PostgreSQL) unique index names -> friendly message.
    private static readonly (string Prefix, string Message)[] UniqueMessages =
    [
        ("IX_Materials_Code", "Esiste già un materiale con questo codice."),
        ("IX_ProcessSegments_Code", "Esiste già un segmento di processo con questo codice."),
        ("IX_ScrapReasons_Code", "Esiste già un motivo di scarto con questo codice."),
        ("IX_ScrapReasonProcessSegments_", "La fase è già collegata a questo motivo di scarto."),
        ("IX_Equipment_Code", "Esiste già una macchina o postazione con questo codice."),
        ("IX_EquipmentProcessSegments_", "La macchina è già collegata a questa fase di processo."),
        ("IX_ProductionOrders_Code", "Esiste già un ordine di produzione con questo codice."),
        ("IX_BomItems_ParentMaterialId_ComponentMaterialId", "Il componente è già presente nella distinta."),
        ("IX_RoutingSteps_MaterialId_OperationNumber", "Esiste già un passo del ciclo con questo numero di operazione."),
        ("IX_RoutingStepDependencies_", "La dipendenza tra i passi del ciclo esiste già."),
        ("IX_WorkOrders_ProductionOrderId_OperationNumber", "Esiste già un ordine di lavoro con questo numero di operazione."),
        ("IX_WorkOrderDependencies_", "La dipendenza tra gli ordini di lavoro esiste già.")
    ];

    /// <summary>User-facing (Italian) message; the constraint name is never part of it.</summary>
    public string Message => Kind == ConstraintViolationKind.ForeignKey
        ? GenericForeignKey
        : UniqueMessages.FirstOrDefault(m => ConstraintName?.StartsWith(m.Prefix, StringComparison.Ordinal) == true).Message
          ?? GenericUnique;

    /// <summary>Finds a unique/foreign-key violation in the exception chain, or null.</summary>
    public static ConstraintViolation? From(Exception? exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is not PostgresException pg) continue;
            return pg.SqlState switch
            {
                UniqueSqlState => new ConstraintViolation(ConstraintViolationKind.Unique, pg.ConstraintName),
                ForeignKeySqlState => new ConstraintViolation(ConstraintViolationKind.ForeignKey, pg.ConstraintName),
                _ => null
            };
        }
        return null;
    }
}
