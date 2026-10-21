using System.Globalization;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;

namespace MicroMES.ApiFeatures.PlanningFeature.Alerts;

public sealed record AlertPredecessor(int Id, string Code, DateTime? End);

/// <summary>An open work order (order Released/InProgress) as the alerts see it.</summary>
public sealed record AlertWorkOrder(
    int Id, string Code, int OrderId, string OrderCode, DateTime? OrderRequestedEnd, WorkOrderStatus Status,
    DateTime? PlannedStart, DateTime? PlannedEnd, int? EquipmentId, string? EquipmentCode, bool MachineUsable,
    IReadOnlyList<AlertPredecessor> Predecessors);

public sealed record AlertMachine(int Id, string Code, int Capacity, IAvailabilitySource Availability);

/// <summary>A work order the engine cannot plan (taken from a run of the engine on the unplanned ones).</summary>
public sealed record AlertUnplannable(int WorkOrderId, string Code, int OrderId, string OrderCode, UnplannedReasonCode Reason, string Message, int? RelatedWorkOrderId);

/// <summary>
/// Pure alerts of the plan. Rules:
/// <list type="bullet">
/// <item><b>Late</b> (critical): per order, the latest planned end of its open work orders is after the requested end; days rounded up.</item>
/// <item><b>Unplannable</b> (critical): the engine reason, named, with a suggestion by reason.</item>
/// <item><b>StalePlan</b> (warning): same criteria as <see cref="PlanStatusCalculator"/> (a predecessor ends after the planned start; a running
/// job is past its planned end). A machine that is no longer usable is reported as InactiveOrDisabledMachine instead, not twice.</item>
/// <item><b>InactiveOrDisabledMachine</b> (critical): Waiting/Ready/Interrupted work order assigned to a machine that is inactive or not enabled.</item>
/// <item><b>MachineOverloaded</b> (warning): for each local week (Monday to Sunday) the working minutes the planned jobs spend on the
/// machine, summed, exceed 100% of capacity × working minutes of the machine in that week. Jobs are non-preemptive intervals, so with the
/// engine's own plan this happens only when manual assignments or stale plans overlap.</item>
/// </list>
/// Order: critical first, then by date (undated last), then type and ids.
/// </summary>
public static class PlanAlertsCalculator
{
    public const decimal OverloadThresholdPercent = 100m;

    public static List<PlanAlertModel> Calculate(
        IReadOnlyList<AlertWorkOrder> workOrders, IReadOnlyList<AlertMachine> machines,
        IReadOnlyList<AlertUnplannable> unplannable, WorkingTimeCalculator calc, DateTime now)
    {
        var alerts = new List<PlanAlertModel>();
        AddLate(alerts, workOrders, calc);
        AddUnplannable(alerts, unplannable);
        AddStaleAndMachine(alerts, workOrders, calc, now);
        AddOverload(alerts, workOrders, machines, calc);

        return alerts
            .OrderByDescending(a => a.Severity)
            .ThenBy(a => a.Date is null ? 1 : 0).ThenBy(a => a.Date)
            .ThenBy(a => a.Type).ThenBy(a => a.WorkOrderId ?? 0).ThenBy(a => a.ProductionOrderId ?? 0).ThenBy(a => a.EquipmentId ?? 0)
            .ToList();
    }

    private static string Local(WorkingTimeCalculator calc, DateTime utc)
        => PlanningDateFormat.Local(calc.Zone, utc);

    private static void AddLate(List<PlanAlertModel> alerts, IReadOnlyList<AlertWorkOrder> workOrders, WorkingTimeCalculator calc)
    {
        foreach (var order in workOrders.Where(w => w.PlannedEnd is not null && w.OrderRequestedEnd is not null).GroupBy(w => w.OrderId))
        {
            var last = order.OrderByDescending(w => w.PlannedEnd).ThenBy(w => w.Id).First();
            var due = last.OrderRequestedEnd!.Value;
            if (last.PlannedEnd!.Value <= due) continue;
            var days = (int)Math.Ceiling((last.PlannedEnd.Value - due).TotalDays);
            var machine = last.EquipmentCode is null ? "" : $" su {last.EquipmentCode}";
            alerts.Add(new PlanAlertModel
            {
                Type = PlanAlertType.Late, Severity = PlanAlertSeverity.Critical,
                ProductionOrderId = last.OrderId, ProductionOrderCode = last.OrderCode,
                WorkOrderId = last.Id, WorkOrderCode = last.Code, EquipmentId = last.EquipmentId, EquipmentCode = last.EquipmentCode,
                Date = last.PlannedEnd, LateDays = days,
                Message = $"L'ordine {last.OrderCode} finisce il {Local(calc, last.PlannedEnd.Value)}, {days} {(days == 1 ? "giorno" : "giorni")} dopo la fine richiesta ({Local(calc, due)}): l'ultimo lavoro è {last.Code}{machine}.",
                Suggestion = "Anticipa l'inizio richiesto, assegna un'altra macchina ai lavori in coda oppure sposta la fine richiesta."
            });
        }
    }

    private static void AddUnplannable(List<PlanAlertModel> alerts, IReadOnlyList<AlertUnplannable> unplannable)
    {
        foreach (var u in unplannable)
            alerts.Add(new PlanAlertModel
            {
                Type = PlanAlertType.Unplannable, Severity = PlanAlertSeverity.Critical,
                ProductionOrderId = u.OrderId, ProductionOrderCode = u.OrderCode, WorkOrderId = u.WorkOrderId, WorkOrderCode = u.Code,
                CauseWorkOrderId = u.RelatedWorkOrderId,
                Message = $"Il lavoro {u.Code} non si può pianificare. {u.Message}",
                Suggestion = u.Reason switch
                {
                    UnplannedReasonCode.NoWorkingTime => "Aggiungi fasce di lavoro al calendario della macchina o rimuovi le chiusure.",
                    UnplannedReasonCode.HorizonExceeded => "Il piano copre 6 mesi: riduci i minuti del lavoro o rimuovi le chiusure del calendario.",
                    UnplannedReasonCode.NoCandidateMachine => "Attiva o abilita almeno una macchina per la fase del lavoro.",
                    UnplannedReasonCode.CyclicDependency => "Correggi le dipendenze: i lavori si aspettano a vicenda.",
                    _ => "Risolvi prima il lavoro che lo trattiene, poi ripianifica."
                }
            });
    }

    private static void AddStaleAndMachine(List<PlanAlertModel> alerts, IReadOnlyList<AlertWorkOrder> workOrders, WorkingTimeCalculator calc, DateTime now)
    {
        foreach (var w in workOrders.OrderBy(w => w.Id))
        {
            if (w.Status is WorkOrderStatus.Waiting or WorkOrderStatus.Ready or WorkOrderStatus.Interrupted
                && w.EquipmentId is not null && !w.MachineUsable)
            {
                alerts.Add(new PlanAlertModel
                {
                    Type = PlanAlertType.InactiveOrDisabledMachine, Severity = PlanAlertSeverity.Critical,
                    ProductionOrderId = w.OrderId, ProductionOrderCode = w.OrderCode, WorkOrderId = w.Id, WorkOrderCode = w.Code,
                    EquipmentId = w.EquipmentId, EquipmentCode = w.EquipmentCode, Date = w.PlannedStart,
                    Message = $"Il lavoro {w.Code} è assegnato a {w.EquipmentCode}, che è disattivata o non è più abilitata per la fase.",
                    Suggestion = "Assegna un'altra macchina abilitata al lavoro (o riabilita la macchina) e ripianifica."
                });
                continue;
            }

            var facts = new WorkPlanFacts(w.Status, w.PlannedStart, w.PlannedEnd, w.OrderRequestedEnd,
                w.Predecessors.Where(p => p.End is not null).Select(p => p.End!.Value).ToList(), w.MachineUsable, now);
            switch (PlanStatusCalculator.CauseOf(facts))
            {
                case PlanStatusCalculator.StaleCause.PredecessorEndsAfterStart:
                    var holder = w.Predecessors.Where(p => p.End is not null).OrderByDescending(p => p.End).ThenBy(p => p.Id).First();
                    alerts.Add(Stale(w, holder.Id, $"Il lavoro {holder.Code} finisce il {Local(calc, holder.End!.Value)}, dopo l'inizio pianificato di {w.Code} ({Local(calc, w.PlannedStart!.Value)}).",
                        "Ripianifica l'ordine per spostare il lavoro dopo il predecessore."));
                    break;
                case PlanStatusCalculator.StaleCause.RunningPastPlannedEnd:
                    alerts.Add(Stale(w, null, $"Il lavoro {w.Code} è ancora in corso oltre la fine pianificata ({Local(calc, w.PlannedEnd!.Value)}): i lavori che lo seguono slittano.",
                        "Ripianifica i lavori che dipendono da questo."));
                    break;
            }
        }
    }

    private static PlanAlertModel Stale(AlertWorkOrder w, int? cause, string message, string suggestion) => new()
    {
        Type = PlanAlertType.StalePlan, Severity = PlanAlertSeverity.Warning,
        ProductionOrderId = w.OrderId, ProductionOrderCode = w.OrderCode, WorkOrderId = w.Id, WorkOrderCode = w.Code,
        EquipmentId = w.EquipmentId, EquipmentCode = w.EquipmentCode, CauseWorkOrderId = cause, Date = w.PlannedStart,
        Message = message, Suggestion = suggestion
    };

    private static decimal Minutes(WorkingTimeCalculator calc, IAvailabilitySource source, DateTime from, DateTime to)
    {
        var r = calc.WorkingMinutesBetween(source, from, to);
        return r.IsSuccess ? r.Value : 0m;
    }

    private static void AddOverload(List<PlanAlertModel> alerts, IReadOnlyList<AlertWorkOrder> workOrders,
        IReadOnlyList<AlertMachine> machines, WorkingTimeCalculator calc)
    {
        foreach (var m in machines.OrderBy(m => m.Id))
        {
            var jobs = workOrders.Where(w => w.EquipmentId == m.Id && w.PlannedStart is not null && w.PlannedEnd is not null
                                             && w.PlannedEnd > w.PlannedStart).ToList();
            if (jobs.Count == 0) continue;
            var first = calc.LocalDate(jobs.Min(j => j.PlannedStart!.Value));
            var monday = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
            var lastDate = calc.LocalDate(jobs.Max(j => j.PlannedEnd!.Value));
            for (var weeks = 0; monday <= lastDate && weeks < 60; monday = monday.AddDays(7), weeks++)
            {
                var from = calc.ToUtc(monday, TimeOnly.MinValue);
                var to = calc.ToUtc(monday.AddDays(7), TimeOnly.MinValue);
                var available = Minutes(calc, m.Availability, from, to) * Math.Max(1, m.Capacity);
                if (available <= 0) continue;
                var inWeek = jobs.Where(j => j.PlannedStart < to && j.PlannedEnd > from).ToList();
                var load = inWeek.Sum(j => Minutes(calc, m.Availability, j.PlannedStart > from ? j.PlannedStart!.Value : from,
                    j.PlannedEnd < to ? j.PlannedEnd!.Value : to));
                var percent = Math.Round(load / available * 100m, 0);
                if (load <= available + 0.01m) continue;
                var heaviest = inWeek.OrderByDescending(j => j.PlannedEnd - j.PlannedStart).ThenBy(j => j.Id).First();
                alerts.Add(new PlanAlertModel
                {
                    Type = PlanAlertType.MachineOverloaded, Severity = PlanAlertSeverity.Warning,
                    EquipmentId = m.Id, EquipmentCode = m.Code, Date = from, LoadPercent = percent,
                    WorkOrderId = heaviest.Id, WorkOrderCode = heaviest.Code, ProductionOrderId = heaviest.OrderId, ProductionOrderCode = heaviest.OrderCode,
                    Message = $"{m.Code} è sovraccarica nella settimana dal {Local(calc, from)[..10]}: {percent:0}% della capacità ({inWeek.Count} lavori, il più lungo è {heaviest.Code}).",
                    Suggestion = "Sposta alcuni lavori su un'altra macchina abilitata e ripianifica."
                });
            }
        }
    }
}
