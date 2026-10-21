using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.MachineStates;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.ProductionOrderFeature;

public class WorkOrderMachineStateFixture
{
    private readonly FakeTimeProvider _clock = new();

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private WorkOrderMachineState Create(
        WorkOrderStatus status, out WorkOrder workOrder, WorkOrderResponse? open = null, WorkOrderResponse? last = null)
    {
        workOrder = new WorkOrder { Id = 7, Status = status };
        return new WorkOrderMachineState(workOrder, open, last, _clock);
    }

    private static WorkOrderStatus[] AllExcept(params WorkOrderStatus[] excluded)
        => Enum.GetValues<WorkOrderStatus>().Except(excluded).ToArray();

    public static TheoryData<WorkOrderStatus> NotWaiting => new(AllExcept(WorkOrderStatus.Waiting));
    public static TheoryData<WorkOrderStatus> NotReady => new(AllExcept(WorkOrderStatus.Ready));
    public static TheoryData<WorkOrderStatus> NotRunning => new(AllExcept(WorkOrderStatus.Running));
    public static TheoryData<WorkOrderStatus> NotInterrupted => new(AllExcept(WorkOrderStatus.Interrupted));
    public static TheoryData<WorkOrderStatus> NotRunningNorInterrupted
        => new(AllExcept(WorkOrderStatus.Running, WorkOrderStatus.Interrupted));
    public static TheoryData<WorkOrderStatus> Terminal => new(WorkOrderStatus.Ended, WorkOrderStatus.Aborted);
    public static TheoryData<WorkOrderStatus> NonTerminal => new(
        WorkOrderStatus.Waiting, WorkOrderStatus.Ready, WorkOrderStatus.Running, WorkOrderStatus.Interrupted);

    // ---- AllowedActions ------------------------------------------------------------------------------

    [Theory]
    [InlineData(WorkOrderStatus.Waiting, new[] { "Abort" })]
    [InlineData(WorkOrderStatus.Ready, new[] { "Start", "Abort" })]
    [InlineData(WorkOrderStatus.Running, new[] { "Pause", "Stop", "Abort" })]
    [InlineData(WorkOrderStatus.Interrupted, new[] { "Resume", "Stop", "Abort" })]
    [InlineData(WorkOrderStatus.Ended, new string[0])]
    [InlineData(WorkOrderStatus.Aborted, new string[0])]
    public void AllowedActions_PerState_Success(WorkOrderStatus status, string[] expected)
    {
        var machine = Create(status, out _);

        machine.AllowedActions.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void AllowedActions_Waiting_Success_DoesNotExposeInternalTrigger()
    {
        var machine = Create(WorkOrderStatus.Waiting, out _);

        machine.CanFire(WorkOrderTrigger.PredecessorsEnded).Should().BeTrue();
        machine.AllowedActions.Should().NotContain("PredecessorsEnded");
    }

    // ---- PredecessorsEnded ---------------------------------------------------------------------------

    [Fact]
    public void PredecessorsEnded_FromWaiting_Success_BecomesReady()
    {
        var machine = Create(WorkOrderStatus.Waiting, out var wo);

        machine.PredecessorsEnded();

        wo.Status.Should().Be(WorkOrderStatus.Ready);
        machine.OpenedSession.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(NotWaiting))]
    public void PredecessorsEnded_NotFromWaiting_Failure_Throws(WorkOrderStatus status)
    {
        var machine = Create(status, out var wo);

        machine.CanFire(WorkOrderTrigger.PredecessorsEnded).Should().BeFalse();
        var act = machine.PredecessorsEnded;
        act.Should().Throw<InvalidOperationException>();
        wo.Status.Should().Be(status);
    }

    // ---- Start ---------------------------------------------------------------------------------------

    [Fact]
    public void Start_FromReady_Success_OpensSessionAndSetsStartedAt()
    {
        var machine = Create(WorkOrderStatus.Ready, out var wo);

        machine.Start();

        wo.Status.Should().Be(WorkOrderStatus.Running);
        wo.StartedAt.Should().Be(Now);
        wo.UpdatedAt.Should().Be(Now);
        machine.OpenedSession.Should().NotBeNull();
        machine.OpenedSession!.WorkOrderId.Should().Be(7);
        machine.OpenedSession.StartedAt.Should().Be(Now);
        machine.OpenedSession.EndedAt.Should().BeNull();
        machine.ClosedSession.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(NotReady))]
    public void Start_NotFromReady_Failure_Throws(WorkOrderStatus status)
    {
        var machine = Create(status, out var wo);

        machine.CanFire(WorkOrderTrigger.Start).Should().BeFalse();
        var act = machine.Start;
        act.Should().Throw<InvalidOperationException>();
        wo.Status.Should().Be(status);
        wo.StartedAt.Should().BeNull();
    }

    // ---- Pause ---------------------------------------------------------------------------------------

    [Fact]
    public void Pause_FromRunning_Success_ClosesSessionWithQuantities()
    {
        var session = new WorkOrderResponse { Id = 1, WorkOrderId = 7, StartedAt = Now };
        var machine = Create(WorkOrderStatus.Running, out var wo, open: session);
        _clock.Advance(TimeSpan.FromMinutes(45));

        machine.Pause(good: 8m, scrap: 1m, notes: "cambio turno");

        wo.Status.Should().Be(WorkOrderStatus.Interrupted);
        machine.ClosedSession.Should().BeSameAs(session);
        session.EndedAt.Should().Be(Now);
        session.GoodQuantity.Should().Be(8m);
        session.ScrapQuantity.Should().Be(1m);
        session.Notes.Should().Be("cambio turno");
        wo.EndedAt.Should().BeNull("a pause does not end the work order");
        machine.OpenedSession.Should().BeNull();
    }

    [Fact]
    public void Pause_FromRunning_Success_AppendsNotesToExistingSessionNotes()
    {
        var session = new WorkOrderResponse { WorkOrderId = 7, StartedAt = Now, Notes = "prima" };
        var machine = Create(WorkOrderStatus.Running, out _, open: session);

        machine.Pause(0m, 0m, "dopo");

        session.Notes.Should().Be("prima | dopo");
    }

    [Fact]
    public void Pause_FromRunning_Success_TruncatesLongNotesTo1000Characters()
    {
        var session = new WorkOrderResponse { WorkOrderId = 7, StartedAt = Now };
        var machine = Create(WorkOrderStatus.Running, out _, open: session);

        machine.Pause(0m, 0m, new string('x', 1500));

        session.Notes.Should().HaveLength(1000);
    }

    [Fact]
    public void Pause_FromRunning_Success_WithoutOpenSessionOnlyChangesState()
    {
        var machine = Create(WorkOrderStatus.Running, out var wo);

        machine.Pause(1m, 0m, null);

        wo.Status.Should().Be(WorkOrderStatus.Interrupted);
        machine.ClosedSession.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(NotRunning))]
    public void Pause_NotFromRunning_Failure_Throws(WorkOrderStatus status)
    {
        var machine = Create(status, out var wo);

        machine.CanFire(WorkOrderTrigger.Pause).Should().BeFalse();
        var act = () => machine.Pause(1m, 0m, null);
        act.Should().Throw<InvalidOperationException>();
        wo.Status.Should().Be(status);
    }

    // ---- Resume --------------------------------------------------------------------------------------

    [Fact]
    public void Resume_FromInterrupted_Success_OpensNewSessionAndKeepsStartedAt()
    {
        var firstStart = Now.AddHours(-3);
        var machine = Create(WorkOrderStatus.Interrupted, out var wo);
        wo.StartedAt = firstStart;

        machine.Resume();

        wo.Status.Should().Be(WorkOrderStatus.Running);
        wo.StartedAt.Should().Be(firstStart);
        machine.OpenedSession.Should().NotBeNull();
        machine.OpenedSession!.StartedAt.Should().Be(Now);
    }

    [Theory]
    [MemberData(nameof(NotInterrupted))]
    public void Resume_NotFromInterrupted_Failure_Throws(WorkOrderStatus status)
    {
        var machine = Create(status, out var wo);

        machine.CanFire(WorkOrderTrigger.Resume).Should().BeFalse();
        var act = machine.Resume;
        act.Should().Throw<InvalidOperationException>();
        wo.Status.Should().Be(status);
    }

    // ---- Stop ----------------------------------------------------------------------------------------

    [Fact]
    public void Stop_FromRunning_Success_ClosesSessionAndEndsWorkOrder()
    {
        var session = new WorkOrderResponse { WorkOrderId = 7, StartedAt = Now };
        var machine = Create(WorkOrderStatus.Running, out var wo, open: session);
        _clock.Advance(TimeSpan.FromHours(2));

        machine.Stop(good: 20m, scrap: 2m, notes: "fine lavoro");

        wo.Status.Should().Be(WorkOrderStatus.Ended);
        wo.EndedAt.Should().Be(Now);
        session.EndedAt.Should().Be(Now);
        session.GoodQuantity.Should().Be(20m);
        session.ScrapQuantity.Should().Be(2m);
        machine.ClosedSession.Should().BeSameAs(session);
    }

    [Fact]
    public void Stop_FromInterrupted_Success_AddsQuantitiesToLastClosedSession()
    {
        var closedAt = Now.AddMinutes(-30);
        var last = new WorkOrderResponse { WorkOrderId = 7, StartedAt = Now.AddHours(-1), EndedAt = closedAt, GoodQuantity = 5m };
        var machine = Create(WorkOrderStatus.Interrupted, out var wo, last: last);

        machine.Stop(good: 3m, scrap: 1m, notes: null);

        wo.Status.Should().Be(WorkOrderStatus.Ended);
        wo.EndedAt.Should().Be(Now);
        last.EndedAt.Should().Be(closedAt, "an already closed session keeps its end");
        last.GoodQuantity.Should().Be(8m);
        last.ScrapQuantity.Should().Be(1m);
        machine.ClosedSession.Should().BeSameAs(last);
    }

    [Theory]
    [MemberData(nameof(NotRunningNorInterrupted))]
    public void Stop_FromOtherStates_Failure_Throws(WorkOrderStatus status)
    {
        var machine = Create(status, out var wo);

        machine.CanFire(WorkOrderTrigger.Stop).Should().BeFalse();
        var act = () => machine.Stop(1m, 0m, null);
        act.Should().Throw<InvalidOperationException>();
        wo.Status.Should().Be(status);
        wo.EndedAt.Should().BeNull();
    }

    // ---- Abort ---------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(NonTerminal))]
    public void Abort_FromNonTerminalState_Success_EndsWithNotes(WorkOrderStatus status)
    {
        var machine = Create(status, out var wo);

        machine.Abort("materiale difettoso");

        wo.Status.Should().Be(WorkOrderStatus.Aborted);
        wo.EndedAt.Should().Be(Now);
        wo.Notes.Should().Be("Interrotto: materiale difettoso");
    }

    [Fact]
    public void Abort_FromRunning_Success_ClosesOpenSessionWithoutQuantities()
    {
        var session = new WorkOrderResponse { WorkOrderId = 7, StartedAt = Now, GoodQuantity = 2m };
        var machine = Create(WorkOrderStatus.Running, out _, open: session);
        _clock.Advance(TimeSpan.FromMinutes(10));

        machine.Abort(null);

        session.EndedAt.Should().Be(Now);
        session.GoodQuantity.Should().Be(2m);
        machine.ClosedSession.Should().BeSameAs(session);
    }

    [Fact]
    public void Abort_WithoutNotes_Success_LeavesNotesUntouched()
    {
        var machine = Create(WorkOrderStatus.Ready, out var wo);

        machine.Abort("  ");

        wo.Notes.Should().BeNull();
    }

    [Fact]
    public void Abort_WithExistingNotes_Success_AppendsReason()
    {
        var machine = Create(WorkOrderStatus.Ready, out var wo);
        wo.Notes = "nota";

        machine.Abort("guasto");

        wo.Notes.Should().Be("nota | Interrotto: guasto");
    }

    [Theory]
    [MemberData(nameof(Terminal))]
    public void Abort_FromTerminalState_Failure_Throws(WorkOrderStatus status)
    {
        var machine = Create(status, out var wo);

        machine.CanFire(WorkOrderTrigger.Abort).Should().BeFalse();
        var act = () => machine.Abort("x");
        act.Should().Throw<InvalidOperationException>();
        wo.Status.Should().Be(status);
    }

    // ---- Fire / messages / export --------------------------------------------------------------------

    [Fact]
    public void Fire_Start_Success_EquivalentToStart()
    {
        var machine = Create(WorkOrderStatus.Ready, out var wo);

        machine.Fire(WorkOrderTrigger.Start);

        wo.Status.Should().Be(WorkOrderStatus.Running);
    }

    [Fact]
    public void FullLifecycle_Success_Waiting_To_Ended_WithPauseAndResume()
    {
        var machine = Create(WorkOrderStatus.Waiting, out var wo);
        machine.PredecessorsEnded();
        machine.Start();
        var first = machine.OpenedSession!;
        _clock.Advance(TimeSpan.FromMinutes(30));

        var paused = new WorkOrderMachineState(wo, first, null, _clock);
        paused.Pause(4m, 0m, null);
        _clock.Advance(TimeSpan.FromMinutes(10));
        var resumed = new WorkOrderMachineState(wo, null, first, _clock);
        resumed.Resume();
        var second = resumed.OpenedSession!;
        _clock.Advance(TimeSpan.FromMinutes(20));
        new WorkOrderMachineState(wo, second, first, _clock).Stop(6m, 1m, null);

        wo.Status.Should().Be(WorkOrderStatus.Ended);
        first.GoodQuantity.Should().Be(4m);
        second.GoodQuantity.Should().Be(6m);
        (second.EndedAt - second.StartedAt).Should().Be(TimeSpan.FromMinutes(20));
    }

    [Fact]
    public void DeniedMessage_FromReady_Success_ListsStateAndAllowedActions()
    {
        var machine = Create(WorkOrderStatus.Ready, out _);

        var message = machine.DeniedMessage(WorkOrderTrigger.Pause);

        message.Should().Contain("'Metti in pausa'").And.Contain("'Pronto'")
            .And.Contain("'Avvia'").And.Contain("'Interrompi'");
    }

    [Fact]
    public void DeniedMessage_FromTerminalState_Success_SaysNoActionAllowed()
    {
        var machine = Create(WorkOrderStatus.Ended, out _);

        machine.DeniedMessage(WorkOrderTrigger.Start).Should().Contain("nessuna (stato terminale)");
    }

    [Fact]
    public void ExportMermaid_Success_ContainsTheLeafStates()
    {
        var graph = WorkOrderMachineState.ExportMermaid();

        graph.Should().Contain("Waiting").And.Contain("Running").And.Contain("Interrupted");
    }
}
