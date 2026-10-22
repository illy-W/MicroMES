using System.Security.Cryptography;
using System.Text;
using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Services;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;
using MicroMES.ApiFeatures.PlanningFeature.Alerts;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;
using MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;

namespace MicroMES.ApiFeatures.PlanningFeature.Services;

/// <summary>Loads machines and their links to the phases (read only, no transaction).</summary>
public class PlanningCatalogLoader(IEquipmentRepository equipment, IEquipmentProcessSegmentRepository links)
{
    public async Task<PlanningCatalog> LoadAsync()
    {
        var machines = new Dictionary<int, Equipment>();
        await foreach (var e in equipment.FetchAsync(_ => true)) machines[e.Id] = e;
        var all = new List<EquipmentProcessSegment>();
        await foreach (var l in links.FetchAsync(_ => true)) all.Add(l);
        return new PlanningCatalog(machines, all);
    }
}

/// <summary>
/// Preview, apply and removal of the plan, and the plan alerts. Preview and apply run the same computation (no duplicated code);
/// apply writes in one transaction. The plan of a work order is PlannedStartAt/PlannedEndAt/PlannedAt plus AssignedEquipmentId:
/// the planning writes the machine only where it was missing or no longer valid (a valid assignment is always kept), and
/// removing a plan never touches the machine. A work order the engine cannot plan loses its old plan on apply.
/// </summary>
public class PlanningService(
    ProductionDataLoader loader,
    PlanningCatalogLoader catalogLoader,
    EffectiveCalendarService calendars,
    IPlanningEngine engine,
    TimeProvider timeProvider,
    IUnitOfWork unitOfWork)
{
    private IWorkOrderRepository WorkOrders => unitOfWork.GetRepository<IWorkOrderRepository>();

    private sealed record Run(ProductionData Data, PlanningBuild Build, PlanningResult Result, DateTime Now, TimeZoneInfo Zone);

    private static string StatusLabel(ProductionOrderStatus s) => s switch
    {
        ProductionOrderStatus.Planned => "in stato Pianificato (non ancora rilasciato)",
        ProductionOrderStatus.Cancelled => "annullato",
        ProductionOrderStatus.Completed => "completato",
        ProductionOrderStatus.Aborted => "interrotto",
        _ => s.ToString()
    };

    private static ProductionOrderResult<T>? CheckOrder<T>(ProductionData data, int orderId)
    {
        if (!data.Orders.TryGetValue(orderId, out var order)) return ProductionOrderResult<T>.NotFound("Ordine di produzione non trovato.");
        return PlanningInputBuilder.IsPlannableOrder(order)
            ? null
            : ProductionOrderResult<T>.Conflict($"L'ordine {order.Code} è {StatusLabel(order.Status)}: si pianificano solo ordini rilasciati o in corso.");
    }

    private async Task<ProductionOrderResult<Run>> RunAsync(PlanningRequest request)
    {
        var data = await loader.LoadAllAsync();
        if (request.Scope == PlanningScope.Order)
        {
            if (request.ProductionOrderId is not { } orderId)
                return ProductionOrderResult<Run>.Invalid("Per pianificare un ordine indica productionOrderId.");
            var problem = CheckOrder<Run>(data, orderId);
            if (problem is not null) return problem;
        }

        var catalog = await catalogLoader.LoadAsync();
        foreach (var m in catalog.Machines.Values) data.Equipment.TryAdd(m.Id, m);
        foreach (var l in catalog.Links) data.EquipmentLinks.Add((l.EquipmentId, l.ProcessSegmentId));
        var directory = await calendars.LoadAsync();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var build = PlanningInputBuilder.Build(data, catalog, directory, request.Scope,
            request.Scope == PlanningScope.Order ? request.ProductionOrderId : null, now);
        var planned = engine.Plan(build.Input);
        return ProductionOrderResult<Run>.Ok(new Run(data, build, planned, now, directory.Calculator.Zone));
    }

    private static ProductionOrderResult<T> Forward<T>(ProductionOrderResult<Run> failed)
        => ProductionOrderResult<T>.Failed(failed.Failure, string.Join("; ", failed.Errors.Distinct()));

    public async Task<ProductionOrderResult<PlanningPreviewModel>> PreviewAsync(PlanningRequest request)
    {
        var run = await RunAsync(request);
        if (!run.IsSuccessful) return Forward<PlanningPreviewModel>(run);
        var (items, unplanned, summary, token) = Describe(run.Data!);
        var (notMoved, conflicts) = DescribeLocked(run.Data!);
        return ProductionOrderResult<PlanningPreviewModel>.Ok(new PlanningPreviewModel
        {
            Scope = request.Scope, ProductionOrderId = request.Scope == PlanningScope.Order ? request.ProductionOrderId : null,
            Items = items, Unplanned = unplanned, NotMoved = notMoved, Conflicts = conflicts, Summary = summary, Token = token, GeneratedAt = run.Data!.Now
        });
    }

    public async Task<ProductionOrderResult<PlanningApplyModel>> ApplyAsync(PlanningRequest request)
    {
        var run = await RunAsync(request);
        if (!run.IsSuccessful) return Forward<PlanningApplyModel>(run);
        var r = run.Data!;
        var (items, unplanned, summary, token) = Describe(r);
        var (notMoved, conflicts) = DescribeLocked(r);

        try
        {
            unitOfWork.BeginTransaction();
            foreach (var item in items)
            {
                var wo = r.Data.WorkOrders[item.WorkOrderId];
                wo.PlannedStartAt = item.ProposedStart;
                wo.PlannedEndAt = item.ProposedEnd;
                wo.PlannedAt = r.Now;
                if (item.AssignsEquipment) wo.AssignedEquipmentId = item.ProposedEquipment!.Id;
                wo.UpdatedAt = r.Now;
                Persist.Ensure(await WorkOrders.UpdateAsync(wo), "Impossibile salvare il piano del lavoro.");
            }
            foreach (var u in unplanned.Where(u => u.HadPlan))
            {
                var wo = r.Data.WorkOrders[u.WorkOrderId];
                wo.PlannedStartAt = null;
                wo.PlannedEndAt = null;
                wo.PlannedAt = null;
                wo.UpdatedAt = r.Now;
                Persist.Ensure(await WorkOrders.UpdateAsync(wo), "Impossibile rimuovere il piano del lavoro.");
            }
            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return ProductionOrderResult<PlanningApplyModel>.Failed(ProductionOrderFailure.None, $"Impossibile applicare il piano: {ex.Message}");
        }

        return ProductionOrderResult<PlanningApplyModel>.Ok(new PlanningApplyModel
        {
            Scope = request.Scope, ProductionOrderId = request.Scope == PlanningScope.Order ? request.ProductionOrderId : null,
            Items = items, Unplanned = unplanned, NotMoved = notMoved, Conflicts = conflicts, Summary = summary, Token = token,
            DiffersFromPreview = string.IsNullOrWhiteSpace(request.PreviewToken) ? null : request.PreviewToken != token,
            AppliedAt = r.Now
        });
    }

    /// <summary>Clears the plan (dates) of the open work orders of the order and its descendants; the machines stay assigned.</summary>
    public async Task<ProductionOrderResult<RemovePlanModel>> RemovePlanAsync(int orderId)
    {
        var data = await loader.LoadAllAsync();
        var problem = CheckOrder<RemovePlanModel>(data, orderId);
        if (problem is not null) return problem;

        var ids = data.Subtree(orderId).Select(o => o.Id).ToHashSet();
        var withPlan = data.WorkOrders.Values
            .Where(w => ids.Contains(w.ProductionOrderId) && w.Status is not (WorkOrderStatus.Ended or WorkOrderStatus.Aborted)
                        && (w.PlannedStartAt is not null || w.PlannedEndAt is not null || w.PlannedAt is not null)).ToList();
        // Locked work orders keep their plan: the answer lists them so the user can unlock them.
        var kept = withPlan.Where(w => w.IsPlanLocked).OrderBy(w => w.Id).ToList();
        var targets = withPlan.Where(w => !w.IsPlanLocked).ToList();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            unitOfWork.BeginTransaction();
            foreach (var wo in targets)
            {
                wo.PlannedStartAt = null;
                wo.PlannedEndAt = null;
                wo.PlannedAt = null;
                wo.UpdatedAt = now;
                Persist.Ensure(await WorkOrders.UpdateAsync(wo), "Impossibile rimuovere il piano del lavoro.");
            }
            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return ProductionOrderResult<RemovePlanModel>.Failed(ProductionOrderFailure.None, $"Impossibile rimuovere il piano: {ex.Message}");
        }
        return ProductionOrderResult<RemovePlanModel>.Ok(new RemovePlanModel { ProductionOrderId = orderId, ClearedWorkOrders = targets.Count,
            KeptLocked = kept.Select(w => ProductionData.CodeOf(data.Orders[w.ProductionOrderId], w)).ToList(),
            KeptLockedWorkOrderIds = kept.Select(w => w.Id).ToList() });
    }

    /// <summary>Alerts on the current plan; the unplannable ones come from a run of the engine on the work orders without a plan.</summary>
    public async Task<ProductionOrderResult<PlanAlertsModel>> GetAlertsAsync()
    {
        var run = await RunAsync(new PlanningRequest { Scope = PlanningScope.Unplanned });
        var r = run.Data!;
        var data = r.Data;
        var directory = await calendars.LoadAsync();
        var catalog = await catalogLoader.LoadAsync();

        var openOrders = data.Orders.Values.Where(PlanningInputBuilder.IsPlannableOrder).Select(o => o.Id).ToHashSet();
        var open = data.WorkOrders.Values.Where(w => openOrders.Contains(w.ProductionOrderId)
                                                      && w.Status is not (WorkOrderStatus.Ended or WorkOrderStatus.Aborted)).ToList();

        string Code(WorkOrder w) => ProductionData.CodeOf(data.Orders[w.ProductionOrderId], w);
        var alertWos = open.Select(w =>
        {
            data.Equipment.TryGetValue(w.AssignedEquipmentId ?? 0, out var eq);
            var usable = w.AssignedEquipmentId is null || (eq is not null && eq.IsActive && data.EquipmentLinks.Contains((eq.Id, w.ProcessSegmentId)));
            return new AlertWorkOrder(w.Id, Code(w), w.ProductionOrderId, data.Orders[w.ProductionOrderId].Code,
                data.Orders[w.ProductionOrderId].RequestedEnd, w.Status, AsUtc(w.PlannedStartAt), AsUtc(w.PlannedEndAt),
                w.AssignedEquipmentId, eq?.Code, usable,
                data.PredecessorIds(w.Id).Select(p => data.WorkOrders[p])
                    .Select(p => new AlertPredecessor(p.Id, Code(p), AsUtc(p.EndedAt ?? p.PlannedEndAt))).ToList(),
                w.IsPlanLocked);
        }).ToList();

        var machines = catalog.Machines.Values.Where(m => m.IsActive)
            .Select(m => new AlertMachine(m.Id, m.Code, Math.Max(1, m.Capacity), directory.ForEquipment(m.Id))).ToList();
        var unplannable = r.Result.Unplanned.Select(u =>
        {
            var w = data.WorkOrders[u.WorkOrderId];
            return new AlertUnplannable(w.Id, Code(w), w.ProductionOrderId, data.Orders[w.ProductionOrderId].Code, u.Code, u.Message, u.RelatedWorkOrderId);
        }).ToList();

        var alerts = PlanAlertsCalculator.Calculate(alertWos, machines, unplannable, directory.Calculator, r.Now);
        return ProductionOrderResult<PlanAlertsModel>.Ok(new PlanAlertsModel
        {
            Alerts = alerts,
            CriticalCount = alerts.Count(a => a.Severity == PlanAlertSeverity.Critical),
            WarningCount = alerts.Count(a => a.Severity == PlanAlertSeverity.Warning),
            GeneratedAt = r.Now
        });
    }

    /// <summary>Unspecified (read by SQLite) is UTC; the calendar functions would otherwise take it as local time.</summary>
    private static DateTime? AsUtc(DateTime? t) => t is { } v && v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : t;

    // ---- description of a run ---------------------------------------------------------------------

    private static (List<PlanningItemModel>, List<PlanningUnplannedModel>, PlanningSummaryModel, string) Describe(Run run)
    {
        var data = run.Data;
        var scope = run.Build.ScopeWorkOrderIds;
        var items = new List<PlanningItemModel>();
        foreach (var p in run.Result.Planned.Where(p => !p.IsFixed && scope.Contains(p.WorkOrderId)))
        {
            var wo = data.WorkOrders[p.WorkOrderId];
            var order = data.Orders[wo.ProductionOrderId];
            var hasPlan = wo.PlannedStartAt is not null && wo.PlannedEndAt is not null;
            var assignsEquipment = p.EquipmentId is { } eq && wo.AssignedEquipmentId != eq;
            var change = !hasPlan ? PlanChangeKind.New
                : wo.PlannedStartAt == p.Start && wo.PlannedEndAt == p.End && !assignsEquipment ? PlanChangeKind.Unchanged
                : PlanChangeKind.Moved;
            var late = order.RequestedEnd is { } due && p.End > due;
            items.Add(new PlanningItemModel
            {
                WorkOrderId = wo.Id, Code = ProductionData.CodeOf(order, wo), ProductionOrderId = order.Id, ProductionOrderCode = order.Code,
                ProcessSegmentCode = wo.ProcessSegmentCode, ProcessSegmentName = wo.ProcessSegmentName, Change = change,
                ProposedEquipment = data.ToEquipmentModel(p.EquipmentId, wo.ProcessSegmentId),
                ProposedStart = p.Start, ProposedEnd = p.End,
                CurrentEquipment = data.ToEquipmentModel(wo.AssignedEquipmentId, wo.ProcessSegmentId),
                CurrentStart = wo.PlannedStartAt, CurrentEnd = wo.PlannedEndAt, AssignsEquipment = assignsEquipment,
                RequestedEnd = order.RequestedEnd, IsLate = late,
                LateDays = late ? (int)Math.Ceiling((p.End - order.RequestedEnd!.Value).TotalDays) : 0,
                Reasons = p.Reasons.Select(r => new PlanningReasonModel
                {
                    Code = r.Code, Message = PlanningDateFormat.Resolve(r.Message, r.Until, run.Zone), Until = r.Until, RelatedWorkOrderId = r.RelatedWorkOrderId, RelatedEquipmentId = r.RelatedEquipmentId
                }).ToList()
            });
        }
        items = items.OrderBy(i => i.ProposedStart).ThenBy(i => i.ProductionOrderCode, StringComparer.Ordinal).ThenBy(i => i.WorkOrderId).ToList();

        var unplanned = run.Result.Unplanned.Where(u => scope.Contains(u.WorkOrderId)).Select(u =>
        {
            var wo = data.WorkOrders[u.WorkOrderId];
            var order = data.Orders[wo.ProductionOrderId];
            return new PlanningUnplannedModel
            {
                WorkOrderId = wo.Id, Code = ProductionData.CodeOf(order, wo), ProductionOrderId = order.Id, ProductionOrderCode = order.Code,
                Reason = u.Code, Message = u.Message, RelatedWorkOrderId = u.RelatedWorkOrderId,
                HadPlan = wo.PlannedStartAt is not null || wo.PlannedEndAt is not null || wo.PlannedAt is not null
            };
        }).OrderBy(u => u.WorkOrderId).ToList();

        var lateOrders = items.GroupBy(i => i.ProductionOrderId).Count(g => g.Any(i => i.IsLate));
        var summary = new PlanningSummaryModel
        {
            Considered = scope.Count, Planned = items.Count,
            New = items.Count(i => i.Change == PlanChangeKind.New), Moved = items.Count(i => i.Change == PlanChangeKind.Moved),
            Unchanged = items.Count(i => i.Change == PlanChangeKind.Unchanged),
            LateOrders = lateOrders, MaxLateDays = items.Count == 0 ? 0 : items.Max(i => i.LateDays),
            Unplanned = unplanned.Count, LastEnd = items.Count == 0 ? null : items.Max(i => i.ProposedEnd)
        };
        var (nm, cf) = DescribeLocked(run);
        summary.LockedCount = nm.Count;
        summary.ConflictCount = cf.Count;
        return (items, unplanned, summary, TokenOf(items, unplanned));
    }

    private static (List<PlanningNotMovedModel>, List<PlanningConflictModel>) DescribeLocked(Run run)
    {
        var data = run.Data;
        var lockedIds = run.Build.LockedWorkOrderIds ?? new HashSet<int>();
        var notMoved = run.Result.Planned.Where(p => p.IsLocked && lockedIds.Contains(p.WorkOrderId)).Select(p =>
        {
            var wo = data.WorkOrders[p.WorkOrderId];
            var order = data.Orders[wo.ProductionOrderId];
            return new PlanningNotMovedModel
            {
                WorkOrderId = wo.Id, Code = ProductionData.CodeOf(order, wo), ProductionOrderId = order.Id, ProductionOrderCode = order.Code,
                ProcessSegmentCode = wo.ProcessSegmentCode, ProcessSegmentName = wo.ProcessSegmentName,
                Equipment = data.ToEquipmentModel(p.EquipmentId, wo.ProcessSegmentId), Start = p.Start, End = p.End, IsLocked = true,
                Message = "Fissato: non spostato"
            };
        }).OrderBy(n => n.Start).ThenBy(n => n.ProductionOrderCode, StringComparer.Ordinal).ThenBy(n => n.WorkOrderId).ToList();

        string Code(int id) => ProductionData.CodeOf(data.Orders[data.WorkOrders[id].ProductionOrderId], data.WorkOrders[id]);
        var relevant = new HashSet<int>(lockedIds);
        relevant.UnionWith(run.Build.ScopeWorkOrderIds);
        var conflicts = run.Result.ConflictList.Where(c => c.WorkOrderIds.Any(relevant.Contains)).Select(c => new PlanningConflictModel
        {
            Code = c.Code, WorkOrderId = c.WorkOrderId, RelatedWorkOrderId = c.RelatedWorkOrderId, EquipmentId = c.EquipmentId,
            WorkOrderIds = c.WorkOrderIds.ToList(), Start = c.Start, End = c.End, Minutes = c.Minutes,
            Message = c.Code == PlanningConflictCode.PredecessorAfterLockedStart
                ? $"Il lavoro {Code(c.RelatedWorkOrderId!.Value)} finisce il {PlanningDateFormat.Compact(run.Zone, c.End)}, dopo l'inizio fissato di {Code(c.WorkOrderId)} ({PlanningDateFormat.Compact(run.Zone, c.Start)}): {c.Minutes} min di conflitto"
                : $"I lavori fissati {string.Join(", ", c.WorkOrderIds.Select(Code))} si sovrappongono oltre la capacità della macchina {(data.Equipment.TryGetValue(c.EquipmentId ?? 0, out var eq) ? eq.Code : "")} dal {PlanningDateFormat.Compact(run.Zone, c.Start)} al {PlanningDateFormat.Compact(run.Zone, c.End)}: {c.Minutes} min di conflitto"
        }).ToList();
        return (notMoved, conflicts);
    }

    private static string TokenOf(List<PlanningItemModel> items, List<PlanningUnplannedModel> unplanned)
    {
        var text = string.Join(";", items.OrderBy(i => i.WorkOrderId)
                       .Select(i => $"{i.WorkOrderId}|{i.ProposedEquipment?.Id}|{i.ProposedStart.Ticks}|{i.ProposedEnd.Ticks}"))
                   + "#" + string.Join(",", unplanned.Select(u => $"{u.WorkOrderId}:{u.Reason}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }
}
