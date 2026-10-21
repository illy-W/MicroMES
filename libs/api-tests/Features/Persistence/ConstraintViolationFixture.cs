using FluentAssertions;
using MicroMES.ApiCore.Persistence.Violations;
using Npgsql;

namespace MicroMES.ApiTests.Features.Persistence;

public class ConstraintViolationFixture
{
    private static PostgresException Pg(string sqlState, string? constraint)
        => new("boom", "ERROR", "ERROR", sqlState, constraintName: constraint);

    [Theory]
    [InlineData("IX_Materials_Code", "Esiste già un materiale con questo codice.")]
    [InlineData("IX_ProcessSegments_Code", "Esiste già un segmento di processo con questo codice.")]
    [InlineData("IX_WorkCalendars_Code", "Esiste già un calendario con questo codice.")]
    [InlineData("IX_WorkCalendars_IsDefault", "Esiste già un calendario predefinito: ne è consentito uno solo.")]
    [InlineData("IX_SystemSettings_Key", "L'impostazione esiste già.")]
    [InlineData("IX_Equipment_Code", "Esiste già una macchina o postazione con questo codice.")]
    [InlineData("IX_EquipmentProcessSegments_EquipmentId_ProcessSegmentId", "La macchina è già collegata a questa fase di processo.")]
    [InlineData("IX_ProductionOrders_Code", "Esiste già un ordine di produzione con questo codice.")]
    [InlineData("IX_BomItems_ParentMaterialId_ComponentMaterialId", "Il componente è già presente nella distinta.")]
    [InlineData("IX_RoutingSteps_MaterialId_OperationNumber", "Esiste già un passo del ciclo con questo numero di operazione.")]
    [InlineData("IX_WorkOrderDependencies_PredecessorWorkOrderId_SuccessorWorkO", "La dipendenza tra gli ordini di lavoro esiste già.")]
    public void Message_KnownUniqueConstraint_Success_FriendlyMessage(string constraint, string expected)
    {
        var violation = ConstraintViolation.From(Pg(ConstraintViolation.UniqueSqlState, constraint));

        violation.Should().NotBeNull();
        violation!.Kind.Should().Be(ConstraintViolationKind.Unique);
        violation.Message.Should().Be(expected);
    }

    [Fact]
    public void Message_AnyViolation_Success_NeverAsksToRetry()
    {
        foreach (var c in new[] { "IX_Materials_Code", "IX_WorkCalendars_Code", "IX_WorkCalendars_IsDefault", "IX_ProductionOrders_Code", "IX_WorkOrders_ProductionOrderId_OperationNumber", "IX_Unknown", null })
            ConstraintViolation.From(Pg(ConstraintViolation.UniqueSqlState, c))!.Message.ToLowerInvariant().Should().NotContain("riprova");
        ConstraintViolation.From(Pg(ConstraintViolation.ForeignKeySqlState, "FK_x"))!.Message.ToLowerInvariant().Should().NotContain("riprova");
    }

    [Fact]
    public void Message_UnknownUniqueConstraint_Success_GenericMessageWithoutConstraintName()
    {
        var violation = ConstraintViolation.From(Pg(ConstraintViolation.UniqueSqlState, "IX_Something_Else"));

        violation!.Message.Should().Be("Il valore inserito esiste già.").And.NotContain("IX_");
        violation.ConstraintName.Should().Be("IX_Something_Else");
    }

    [Fact]
    public void From_ForeignKey_Success_ForeignKeyKind()
    {
        var violation = ConstraintViolation.From(Pg(ConstraintViolation.ForeignKeySqlState, "FK_x"));

        violation!.Kind.Should().Be(ConstraintViolationKind.ForeignKey);
        violation.Message.Should().NotContain("FK_");
    }

    [Fact]
    public void From_WrappedInInnerException_Success_Found()
    {
        var wrapped = new InvalidOperationException("outer", new Exception("mid", Pg(ConstraintViolation.UniqueSqlState, "IX_Materials_Code")));

        ConstraintViolation.From(wrapped)!.Message.Should().Contain("materiale");
    }

    [Fact]
    public void From_OtherErrors_Success_Null()
    {
        ConstraintViolation.From(new InvalidOperationException("x")).Should().BeNull();
        ConstraintViolation.From(Pg("42P01", null)).Should().BeNull();
        ConstraintViolation.From(null).Should().BeNull();
    }
}
