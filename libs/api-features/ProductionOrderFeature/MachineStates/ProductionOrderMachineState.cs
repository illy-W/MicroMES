using MicroMES.ApiCore.Persistence.Entities;
using Stateless;
using Stateless.Graph;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.MachineStates;

public enum ProductionOrderTrigger
{
    Release,
    /// <summary>Fired by the work order service when the first work order starts.</summary>
    Start,
    /// <summary>Fired by the work order service when all the work orders are Ended.</summary>
    Complete,
    Cancel,
    Abort
}

/// <summary>
/// Production order state machine. Internal enum with the superstates <c>Open</c> (Released, InProgress)
/// and <c>Closed</c> (Completed, Cancelled, Aborted); only the leaf states are persisted as
/// <see cref="ProductionOrderStatus"/>.
/// </summary>
public class ProductionOrderMachineState
{
    internal enum State
    {
        Planned,
        Open,
        Released,
        InProgress,
        Closed,
        Completed,
        Cancelled,
        Aborted
    }

    /// <summary>Triggers fired by the services, not offered as UI commands.</summary>
    private static readonly ProductionOrderTrigger[] InternalTriggers = [ProductionOrderTrigger.Start, ProductionOrderTrigger.Complete];

    private readonly ProductionOrder _order;
    private readonly TimeProvider _timeProvider;
    private readonly StateMachine<State, ProductionOrderTrigger> _machine;
    private readonly StateMachine<State, ProductionOrderTrigger>.TriggerWithParameters<string?> _abort;

    /// <param name="hasStartedWork">True when any work order of the order or of its children was started (guards Cancel from Released).</param>
    public ProductionOrderMachineState(ProductionOrder order, bool hasStartedWork, TimeProvider timeProvider)
    {
        _order = order;
        _timeProvider = timeProvider;
        _machine = new StateMachine<State, ProductionOrderTrigger>(() => ToMachine(order.Status), s => order.Status = ToPersisted(s));
        _abort = _machine.SetTriggerParameters<string?>(ProductionOrderTrigger.Abort);

        _machine.Configure(State.Planned)
            .Permit(ProductionOrderTrigger.Release, State.Released)
            .Permit(ProductionOrderTrigger.Cancel, State.Cancelled);

        // Open (Released, InProgress): Abort is allowed from both.
        _machine.Configure(State.Open)
            .Permit(ProductionOrderTrigger.Abort, State.Aborted);

        _machine.Configure(State.Released).SubstateOf(State.Open)
            .Permit(ProductionOrderTrigger.Start, State.InProgress)
            .PermitIf(ProductionOrderTrigger.Cancel, State.Cancelled, () => !hasStartedWork, "nessun lavoro avviato")
            .OnEntry(() =>
            {
                _order.ReleasedAt = Now();
                _order.UpdatedAt = Now();
            }, "ReleasedAt");

        _machine.Configure(State.InProgress).SubstateOf(State.Open)
            .Ignore(ProductionOrderTrigger.Start)
            .Permit(ProductionOrderTrigger.Complete, State.Completed)
            .OnEntry(() =>
            {
                _order.StartedAt ??= Now();
                _order.UpdatedAt = Now();
            }, "StartedAt");

        // Closed: terminal, not modifiable.
        _machine.Configure(State.Closed);

        _machine.Configure(State.Completed).SubstateOf(State.Closed).OnEntry(Close, "EndedAt");
        _machine.Configure(State.Cancelled).SubstateOf(State.Closed).OnEntry(Close, "EndedAt");
        _machine.Configure(State.Aborted).SubstateOf(State.Closed)
            .OnEntryFrom(_abort, notes =>
            {
                Close();
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    var text = string.IsNullOrEmpty(_order.Notes) ? $"Interrotto: {notes}" : $"{_order.Notes} | Interrotto: {notes}";
                    _order.Notes = text.Length <= 1000 ? text : text[..1000];
                }
            }, "EndedAt, note");
    }

    public ProductionOrderStatus Status => _order.Status;

    /// <summary>Actions the UI may offer now (Release, Cancel, Abort), service-driven triggers excluded.</summary>
    public IReadOnlyList<string> AllowedActions => Enum.GetValues<ProductionOrderTrigger>()
        .Where(t => _machine.CanFire(t))
        .Where(t => !InternalTriggers.Contains(t))
        .Select(t => t.ToString())
        .ToList();

    public bool CanFire(ProductionOrderTrigger trigger) => _machine.CanFire(trigger);

    public void Fire(ProductionOrderTrigger trigger)
    {
        if (trigger == ProductionOrderTrigger.Abort) _machine.Fire(_abort, (string?)null);
        else _machine.Fire(trigger);
    }

    public void Abort(string? notes) => _machine.Fire(_abort, notes);

    /// <summary>Italian 409 message: current state and allowed actions.</summary>
    public string DeniedMessage(ProductionOrderTrigger trigger)
    {
        var allowed = AllowedActions;
        var list = allowed.Count == 0
            ? "nessuna (stato terminale)"
            : string.Join(", ", allowed.Select(a => $"'{ActionLabel(Enum.Parse<ProductionOrderTrigger>(a))}'"));
        var reason = trigger == ProductionOrderTrigger.Cancel && _order.Status == ProductionOrderStatus.Released
            ? " (ci sono lavori già avviati)"
            : string.Empty;
        return $"Impossibile eseguire '{ActionLabel(trigger)}': l'ordine è in stato '{StatusLabel(_order.Status)}'{reason}. Azioni consentite: {list}.";
    }

    public static string StatusLabel(ProductionOrderStatus status) => status switch
    {
        ProductionOrderStatus.Planned => "Pianificato",
        ProductionOrderStatus.Released => "Rilasciato",
        ProductionOrderStatus.InProgress => "In corso",
        ProductionOrderStatus.Completed => "Completato",
        ProductionOrderStatus.Cancelled => "Annullato",
        ProductionOrderStatus.Aborted => "Interrotto",
        _ => status.ToString()
    };

    public static string ActionLabel(ProductionOrderTrigger trigger) => trigger switch
    {
        ProductionOrderTrigger.Release => "Rilascia",
        ProductionOrderTrigger.Start => "Avvia",
        ProductionOrderTrigger.Complete => "Completa",
        ProductionOrderTrigger.Cancel => "Annulla",
        ProductionOrderTrigger.Abort => "Interrompi",
        _ => trigger.ToString()
    };

    /// <summary>Transition graph in Mermaid syntax, generated from the machine configuration.</summary>
    public static string ExportMermaid()
        => MermaidGraph.Format(NewDetached()._machine.GetInfo());

    /// <summary>Transition graph in Graphviz DOT syntax.</summary>
    public static string ExportDot()
        => UmlDotGraph.Format(NewDetached()._machine.GetInfo());

    private static ProductionOrderMachineState NewDetached()
        => new(new ProductionOrder(), false, TimeProvider.System);

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    private void Close()
    {
        _order.EndedAt = Now();
        _order.UpdatedAt = Now();
    }

    private static State ToMachine(ProductionOrderStatus status) => status switch
    {
        ProductionOrderStatus.Planned => State.Planned,
        ProductionOrderStatus.Released => State.Released,
        ProductionOrderStatus.InProgress => State.InProgress,
        ProductionOrderStatus.Completed => State.Completed,
        ProductionOrderStatus.Cancelled => State.Cancelled,
        ProductionOrderStatus.Aborted => State.Aborted,
        _ => throw new InvalidOperationException($"Stato non valido: {status}")
    };

    private static ProductionOrderStatus ToPersisted(State state) => state switch
    {
        State.Planned => ProductionOrderStatus.Planned,
        State.Released => ProductionOrderStatus.Released,
        State.InProgress => ProductionOrderStatus.InProgress,
        State.Completed => ProductionOrderStatus.Completed,
        State.Cancelled => ProductionOrderStatus.Cancelled,
        State.Aborted => ProductionOrderStatus.Aborted,
        _ => throw new InvalidOperationException($"Lo stato astratto {state} non può essere salvato.")
    };
}
