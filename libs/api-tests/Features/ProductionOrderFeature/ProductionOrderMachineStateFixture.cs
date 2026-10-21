using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.MachineStates;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.ProductionOrderFeature;

public class ProductionOrderMachineStateFixture
{
    private readonly FakeTimeProvider _clock = new();

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private ProductionOrderMachineState Create(ProductionOrderStatus status, out ProductionOrder order, bool hasStartedWork = false)
    {
        order = new ProductionOrder { Id = 3, Status = status };
        return new ProductionOrderMachineState(order, hasStartedWork, _clock);
    }

    public static TheoryData<ProductionOrderStatus> Terminal => new(
        ProductionOrderStatus.Completed, ProductionOrderStatus.Cancelled, ProductionOrderStatus.Aborted);

    public static TheoryData<ProductionOrderStatus> NotPlanned => new(
        Enum.GetValues<ProductionOrderStatus>().Where(s => s != ProductionOrderStatus.Planned).ToArray());

    public static TheoryData<ProductionOrderStatus> NotReleased => new(
        Enum.GetValues<ProductionOrderStatus>().Where(s => s != ProductionOrderStatus.Released).ToArray());

    // ---- AllowedActions ------------------------------------------------------------------------------

    [Theory]
    [InlineData(ProductionOrderStatus.Planned, false, new[] { "Release", "Cancel" })]
    [InlineData(ProductionOrderStatus.Released, false, new[] { "Cancel", "Abort" })]
    [InlineData(ProductionOrderStatus.Released, true, new[] { "Abort" })]
    [InlineData(ProductionOrderStatus.InProgress, true, new[] { "Abort" })]
    [InlineData(ProductionOrderStatus.Completed, true, new string[0])]
    [InlineData(ProductionOrderStatus.Cancelled, false, new string[0])]
    [InlineData(ProductionOrderStatus.Aborted, false, new string[0])]
    public void AllowedActions_PerState_Success(ProductionOrderStatus status, bool startedWork, string[] expected)
    {
        var machine = Create(status, out _, startedWork);

        machine.AllowedActions.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void AllowedActions_Released_Success_DoesNotExposeServiceDrivenTriggers()
    {
        var machine = Create(ProductionOrderStatus.Released, out _);

        machine.CanFire(ProductionOrderTrigger.Start).Should().BeTrue();
        machine.AllowedActions.Should().NotContain(["Start", "Complete"]);
    }

    // ---- Release -------------------------------------------------------------------------------------

    [Fact]
    public void Release_FromPlanned_Success_SetsReleasedAt()
    {
        var machine = Create(ProductionOrderStatus.Planned, out var order);

        machine.Fire(ProductionOrderTrigger.Release);

        order.Status.Should().Be(ProductionOrderStatus.Released);
        order.ReleasedAt.Should().Be(Now);
        order.UpdatedAt.Should().Be(Now);
    }

    [Theory]
    [MemberData(nameof(NotPlanned))]
    public void Release_NotFromPlanned_Failure_Throws(ProductionOrderStatus status)
    {
        var machine = Create(status, out var order);

        machine.CanFire(ProductionOrderTrigger.Release).Should().BeFalse();
        var act = () => machine.Fire(ProductionOrderTrigger.Release);
        act.Should().Throw<InvalidOperationException>();
        order.Status.Should().Be(status);
    }

    // ---- Start ---------------------------------------------------------------------------------------

    [Fact]
    public void Start_FromReleased_Success_BecomesInProgress()
    {
        var machine = Create(ProductionOrderStatus.Released, out var order);

        machine.Fire(ProductionOrderTrigger.Start);

        order.Status.Should().Be(ProductionOrderStatus.InProgress);
        order.StartedAt.Should().Be(Now);
    }

    [Fact]
    public void Start_FromInProgress_Success_IsIgnored()
    {
        var firstStart = Now.AddDays(-1);
        var machine = Create(ProductionOrderStatus.InProgress, out var order);
        order.StartedAt = firstStart;

        machine.CanFire(ProductionOrderTrigger.Start).Should().BeTrue();
        machine.Fire(ProductionOrderTrigger.Start);

        order.Status.Should().Be(ProductionOrderStatus.InProgress);
        order.StartedAt.Should().Be(firstStart);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Planned)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Cancelled)]
    [InlineData(ProductionOrderStatus.Aborted)]
    public void Start_FromPlannedOrTerminal_Failure_Throws(ProductionOrderStatus status)
    {
        var machine = Create(status, out var order);

        machine.CanFire(ProductionOrderTrigger.Start).Should().BeFalse();
        var act = () => machine.Fire(ProductionOrderTrigger.Start);
        act.Should().Throw<InvalidOperationException>();
        order.Status.Should().Be(status);
    }

    // ---- Complete ------------------------------------------------------------------------------------

    [Fact]
    public void Complete_FromInProgress_Success_SetsEndedAt()
    {
        var machine = Create(ProductionOrderStatus.InProgress, out var order);

        machine.Fire(ProductionOrderTrigger.Complete);

        order.Status.Should().Be(ProductionOrderStatus.Completed);
        order.EndedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Planned)]
    [InlineData(ProductionOrderStatus.Released)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Cancelled)]
    [InlineData(ProductionOrderStatus.Aborted)]
    public void Complete_NotFromInProgress_Failure_Throws(ProductionOrderStatus status)
    {
        var machine = Create(status, out _);

        machine.CanFire(ProductionOrderTrigger.Complete).Should().BeFalse();
        var act = () => machine.Fire(ProductionOrderTrigger.Complete);
        act.Should().Throw<InvalidOperationException>();
    }

    // ---- Cancel --------------------------------------------------------------------------------------

    [Fact]
    public void Cancel_FromPlanned_Success_EvenWithStartedWorkFlag()
    {
        var machine = Create(ProductionOrderStatus.Planned, out var order, hasStartedWork: true);

        machine.Fire(ProductionOrderTrigger.Cancel);

        order.Status.Should().Be(ProductionOrderStatus.Cancelled);
        order.EndedAt.Should().Be(Now);
    }

    [Fact]
    public void Cancel_FromReleasedWithoutStartedWork_Success()
    {
        var machine = Create(ProductionOrderStatus.Released, out var order, hasStartedWork: false);

        machine.Fire(ProductionOrderTrigger.Cancel);

        order.Status.Should().Be(ProductionOrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromReleasedWithStartedWork_Failure_GuardBlocksAndMessageExplainsWhy()
    {
        var machine = Create(ProductionOrderStatus.Released, out var order, hasStartedWork: true);

        machine.CanFire(ProductionOrderTrigger.Cancel).Should().BeFalse();
        var act = () => machine.Fire(ProductionOrderTrigger.Cancel);
        act.Should().Throw<InvalidOperationException>();
        order.Status.Should().Be(ProductionOrderStatus.Released);
        machine.DeniedMessage(ProductionOrderTrigger.Cancel).Should().Contain("lavori già avviati");
    }

    [Theory]
    [InlineData(ProductionOrderStatus.InProgress)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Cancelled)]
    [InlineData(ProductionOrderStatus.Aborted)]
    public void Cancel_FromInProgressOrTerminal_Failure_Throws(ProductionOrderStatus status)
    {
        var machine = Create(status, out _);

        machine.CanFire(ProductionOrderTrigger.Cancel).Should().BeFalse();
    }

    // ---- Abort ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(ProductionOrderStatus.Released)]
    [InlineData(ProductionOrderStatus.InProgress)]
    public void Abort_FromOpenState_Success_EndsWithNotes(ProductionOrderStatus status)
    {
        var machine = Create(status, out var order, hasStartedWork: true);

        machine.Abort("cliente ha cambiato idea");

        order.Status.Should().Be(ProductionOrderStatus.Aborted);
        order.EndedAt.Should().Be(Now);
        order.Notes.Should().Be("Interrotto: cliente ha cambiato idea");
    }

    [Fact]
    public void Abort_WithExistingNotes_Success_AppendsAndTruncatesTo1000()
    {
        var machine = Create(ProductionOrderStatus.Released, out var order);
        order.Notes = new string('n', 990);

        machine.Abort("motivo molto lungo");

        order.Notes.Should().HaveLength(1000).And.StartWith(new string('n', 990));
    }

    [Fact]
    public void Abort_FromFireWithoutNotes_Success_LeavesNotesUntouched()
    {
        var machine = Create(ProductionOrderStatus.InProgress, out var order);

        machine.Fire(ProductionOrderTrigger.Abort);

        order.Status.Should().Be(ProductionOrderStatus.Aborted);
        order.Notes.Should().BeNull();
    }

    [Fact]
    public void Abort_FromPlanned_Failure_PlannedOrderCanOnlyBeCancelled()
    {
        var machine = Create(ProductionOrderStatus.Planned, out _);

        machine.CanFire(ProductionOrderTrigger.Abort).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(Terminal))]
    public void Abort_FromTerminalState_Failure_Throws(ProductionOrderStatus status)
    {
        var machine = Create(status, out var order);

        var act = () => machine.Abort("x");
        act.Should().Throw<InvalidOperationException>();
        order.Status.Should().Be(status);
    }

    // ---- Terminal states / messages ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Terminal))]
    public void TerminalState_Success_NoTriggerCanBeFired(ProductionOrderStatus status)
    {
        var machine = Create(status, out _);

        Enum.GetValues<ProductionOrderTrigger>().Should().OnlyContain(t => !machine.CanFire(t));
        machine.DeniedMessage(ProductionOrderTrigger.Release).Should().Contain("nessuna (stato terminale)");
    }

    [Fact]
    public void DeniedMessage_FromPlanned_Success_ListsAllowedActionsInItalian()
    {
        var machine = Create(ProductionOrderStatus.Planned, out _);

        machine.DeniedMessage(ProductionOrderTrigger.Abort)
            .Should().Contain("'Interrompi'").And.Contain("'Pianificato'")
            .And.Contain("'Rilascia'").And.Contain("'Annulla'");
    }

    [Fact]
    public void ExportMermaid_Success_ContainsTheLeafStates()
    {
        ProductionOrderMachineState.ExportMermaid().Should().Contain("Released").And.Contain("InProgress");
    }
}
