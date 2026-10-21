using MicroMES.ApiCore.Persistence.Entities;
using Stateless;
using Stateless.Graph;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.MachineStates;

public enum WorkOrderTrigger
{
    Start,
    Pause,
    Resume,
    Stop,
    Abort,
    /// <summary>Internal: all the predecessors are Ended (Waiting -> Ready).</summary>
    PredecessorsEnded
}

/// <summary>
/// Work order state machine. The machine uses its own enum (leaf states plus the abstract superstates
/// <c>Open</c> and <c>Prepared</c>); only the leaf states are persisted as <see cref="WorkOrderStatus"/>.
/// Side effects live in the OnEntry of the leaf states (a superstate OnEntry does not fire between its substates).
/// </summary>
public class WorkOrderMachineState
{
    // Superstates (never stored, never exposed) and leaf states.
    internal enum State
    {
        Open,
        Prepared,
        Waiting,
        Ready,
        Running,
        Interrupted,
        Ended,
        Aborted
    }

    private static readonly WorkOrderTrigger[] InternalTriggers = [WorkOrderTrigger.PredecessorsEnded];

    private readonly WorkOrder _workOrder;
    private readonly WorkOrderResponse? _openSession;
    private readonly WorkOrderResponse? _lastSession;
    private readonly TimeProvider _timeProvider;
    private readonly StateMachine<State, WorkOrderTrigger> _machine;
    private readonly StateMachine<State, WorkOrderTrigger>.TriggerWithParameters<decimal, decimal, string?> _pause;
    private readonly StateMachine<State, WorkOrderTrigger>.TriggerWithParameters<decimal, decimal, string?> _stop;
    private readonly StateMachine<State, WorkOrderTrigger>.TriggerWithParameters<string?> _abort;

    /// <summary>Session created by the last transition (to be inserted by the caller).</summary>
    public WorkOrderResponse? OpenedSession { get; private set; }

    /// <summary>Existing session updated by the last transition (to be saved by the caller).</summary>
    public WorkOrderResponse? ClosedSession { get; private set; }

    /// <param name="openSession">The session with no EndedAt, if any.</param>
    /// <param name="lastSession">The most recent closed session, if any (receives the quantities of a Stop from Interrupted).</param>
    public WorkOrderMachineState(
        WorkOrder workOrder,
        WorkOrderResponse? openSession,
        WorkOrderResponse? lastSession,
        TimeProvider timeProvider)
    {
        _workOrder = workOrder;
        _openSession = openSession;
        _lastSession = lastSession;
        _timeProvider = timeProvider;
        _machine = new StateMachine<State, WorkOrderTrigger>(() => ToMachine(workOrder.Status), s => workOrder.Status = ToPersisted(s));
        _pause = _machine.SetTriggerParameters<decimal, decimal, string?>(WorkOrderTrigger.Pause);
        _stop = _machine.SetTriggerParameters<decimal, decimal, string?>(WorkOrderTrigger.Stop);
        _abort = _machine.SetTriggerParameters<string?>(WorkOrderTrigger.Abort);

        // Open: every non terminal state. Abort is allowed from all of its substates.
        _machine.Configure(State.Open)
            .Permit(WorkOrderTrigger.Abort, State.Aborted);

        // Prepared (AllowedToStart): Waiting and Ready.
        _machine.Configure(State.Prepared).SubstateOf(State.Open);

        _machine.Configure(State.Waiting).SubstateOf(State.Prepared)
            .Permit(WorkOrderTrigger.PredecessorsEnded, State.Ready);

        _machine.Configure(State.Ready).SubstateOf(State.Prepared)
            .Permit(WorkOrderTrigger.Start, State.Running);

        _machine.Configure(State.Running).SubstateOf(State.Open)
            .Permit(WorkOrderTrigger.Pause, State.Interrupted)
            .Permit(WorkOrderTrigger.Stop, State.Ended)
            .OnEntry(OpenSession, "apre sessione");

        _machine.Configure(State.Interrupted).SubstateOf(State.Open)
            .Permit(WorkOrderTrigger.Resume, State.Running)
            .Permit(WorkOrderTrigger.Stop, State.Ended)
            .OnEntryFrom(_pause, (good, scrap, notes) => CloseSession(good, scrap, notes), "chiude sessione");

        _machine.Configure(State.Ended)
            .OnEntryFrom(_stop, (good, scrap, notes) =>
            {
                CloseSession(good, scrap, notes);
                _workOrder.EndedAt = Now();
                _workOrder.UpdatedAt = Now();
            }, "chiude sessione, EndedAt");

        _machine.Configure(State.Aborted)
            .OnEntryFrom(_abort, notes =>
            {
                CloseSession(0m, 0m, notes);
                _workOrder.EndedAt = Now();
                _workOrder.UpdatedAt = Now();
                if (!string.IsNullOrWhiteSpace(notes))
                    _workOrder.Notes = Truncate(string.IsNullOrEmpty(_workOrder.Notes) ? $"Interrotto: {notes}" : $"{_workOrder.Notes} | Interrotto: {notes}", 1000);
            }, "chiude sessione, EndedAt");
    }

    public WorkOrderStatus Status => _workOrder.Status;

    /// <summary>Actions the UI may offer now (single source for the commands), internal triggers excluded.</summary>
    public IReadOnlyList<string> AllowedActions => Enum.GetValues<WorkOrderTrigger>()
        .Where(t => _machine.CanFire(t))
        .Where(t => !InternalTriggers.Contains(t))
        .Select(t => t.ToString())
        .ToList();

    public bool CanFire(WorkOrderTrigger trigger) => _machine.CanFire(trigger);

    public void Start() { Reset(); _machine.Fire(WorkOrderTrigger.Start); }
    public void Resume() { Reset(); _machine.Fire(WorkOrderTrigger.Resume); }
    public void PredecessorsEnded() { Reset(); _machine.Fire(WorkOrderTrigger.PredecessorsEnded); }
    public void Pause(decimal good, decimal scrap, string? notes) { Reset(); _machine.Fire(_pause, good, scrap, notes); }
    public void Stop(decimal good, decimal scrap, string? notes) { Reset(); _machine.Fire(_stop, good, scrap, notes); }
    public void Abort(string? notes) { Reset(); _machine.Fire(_abort, notes); }

    public void Fire(WorkOrderTrigger trigger)
    {
        switch (trigger)
        {
            case WorkOrderTrigger.Start: Start(); break;
            case WorkOrderTrigger.Resume: Resume(); break;
            case WorkOrderTrigger.PredecessorsEnded: PredecessorsEnded(); break;
            case WorkOrderTrigger.Pause: Pause(0m, 0m, null); break;
            case WorkOrderTrigger.Stop: Stop(0m, 0m, null); break;
            case WorkOrderTrigger.Abort: Abort(null); break;
        }
    }

    /// <summary>Italian 409 message: current state and allowed actions.</summary>
    public string DeniedMessage(WorkOrderTrigger trigger)
    {
        var allowed = AllowedActions;
        var list = allowed.Count == 0
            ? "nessuna (stato terminale)"
            : string.Join(", ", allowed.Select(a => $"'{ActionLabel(Enum.Parse<WorkOrderTrigger>(a))}'"));
        return $"Impossibile eseguire '{ActionLabel(trigger)}': il lavoro è in stato '{StatusLabel(_workOrder.Status)}'. Azioni consentite: {list}.";
    }

    public static string StatusLabel(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Waiting => "In attesa",
        WorkOrderStatus.Ready => "Pronto",
        WorkOrderStatus.Running => "In esecuzione",
        WorkOrderStatus.Interrupted => "Interrotto",
        WorkOrderStatus.Ended => "Terminato",
        WorkOrderStatus.Aborted => "Annullato",
        _ => status.ToString()
    };

    public static string ActionLabel(WorkOrderTrigger trigger) => trigger switch
    {
        WorkOrderTrigger.Start => "Avvia",
        WorkOrderTrigger.Pause => "Metti in pausa",
        WorkOrderTrigger.Resume => "Riprendi",
        WorkOrderTrigger.Stop => "Chiudi lavoro",
        WorkOrderTrigger.Abort => "Interrompi",
        WorkOrderTrigger.PredecessorsEnded => "Predecessori terminati",
        _ => trigger.ToString()
    };

    /// <summary>Transition graph in Mermaid syntax, generated from the machine configuration.</summary>
    public static string ExportMermaid()
        => MermaidGraph.Format(NewDetached()._machine.GetInfo());

    /// <summary>Transition graph in Graphviz DOT syntax.</summary>
    public static string ExportDot()
        => UmlDotGraph.Format(NewDetached()._machine.GetInfo());

    private static WorkOrderMachineState NewDetached()
        => new(new WorkOrder(), null, null, TimeProvider.System);

    private void Reset()
    {
        OpenedSession = null;
        ClosedSession = null;
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    private void OpenSession()
    {
        var now = Now();
        _workOrder.StartedAt ??= now;
        _workOrder.UpdatedAt = now;
        OpenedSession = new WorkOrderResponse { WorkOrderId = _workOrder.Id, StartedAt = now };
    }

    private void CloseSession(decimal good, decimal scrap, string? notes)
    {
        var now = Now();
        var session = _openSession ?? _lastSession;
        _workOrder.UpdatedAt = now;
        if (session is null) return;
        if (_openSession is not null) session.EndedAt = now;
        session.GoodQuantity += good;
        session.ScrapQuantity += scrap;
        if (!string.IsNullOrWhiteSpace(notes))
            session.Notes = Truncate(string.IsNullOrEmpty(session.Notes) ? notes : $"{session.Notes} | {notes}", 1000);
        ClosedSession = session;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static State ToMachine(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Waiting => State.Waiting,
        WorkOrderStatus.Ready => State.Ready,
        WorkOrderStatus.Running => State.Running,
        WorkOrderStatus.Interrupted => State.Interrupted,
        WorkOrderStatus.Ended => State.Ended,
        WorkOrderStatus.Aborted => State.Aborted,
        _ => throw new InvalidOperationException($"Stato non valido: {status}")
    };

    private static WorkOrderStatus ToPersisted(State state) => state switch
    {
        State.Waiting => WorkOrderStatus.Waiting,
        State.Ready => WorkOrderStatus.Ready,
        State.Running => WorkOrderStatus.Running,
        State.Interrupted => WorkOrderStatus.Interrupted,
        State.Ended => WorkOrderStatus.Ended,
        State.Aborted => WorkOrderStatus.Aborted,
        _ => throw new InvalidOperationException($"Lo stato astratto {state} non può essere salvato.")
    };
}
