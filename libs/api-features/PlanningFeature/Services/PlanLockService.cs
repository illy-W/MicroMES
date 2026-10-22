using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.CalendarFeature.Domain;
using MicroMES.ApiFeatures.CalendarFeature.Services;
using MicroMES.ApiFeatures.EquipmentFeature.Services;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;

namespace MicroMES.ApiFeatures.PlanningFeature.Services;

/// <summary>
/// Manual move of a work order (start and machine), with preview. Preview and apply share the same validation and computation
/// (<see cref="ManualMoveCalculator"/>, same calculator as the engine); apply writes plan, machine and lock in one transaction.
/// The start is never shifted silently; conflicts are listed but never block; successors are not moved.
/// </summary>
public class PlanLockService(
    ProductionDataLoader loader,
    PlanningCatalogLoader catalogLoader,
    EffectiveCalendarService calendars,
    EquipmentSelector selector,
    TimeProvider timeProvider,
    IUnitOfWork unitOfWork)
{
    private IWorkOrderRepository WorkOrders => unitOfWork.GetRepository<IWorkOrderRepository>();

    private sealed record Move(ProductionData Data, WorkOrder WorkOrder, Equipment? Machine, DateTime Start, DateTime End, decimal Minutes,
        List<MoveConflictModel> Conflicts, DateTime Now);

    public async Task<ProductionOrderResult<MovePreviewModel>> PreviewAsync(int id, MoveWorkOrderRequest request)
    {
        var move = await ComputeAsync(id, request, forApply: false);
        if (!move.IsSuccessful) return ProductionOrderResult<MovePreviewModel>.Failed(move.Failure, string.Join("; ", move.Errors.Distinct()));
        var m = move.Data!;
        var wo = m.WorkOrder;
        return ProductionOrderResult<MovePreviewModel>.Ok(new MovePreviewModel
        {
            WorkOrderId = wo.Id, Code = ProductionData.CodeOf(m.Data.Orders[wo.ProductionOrderId], wo),
            Equipment = m.Data.ToEquipmentModel(m.Machine?.Id, wo.ProcessSegmentId),
            CurrentEquipment = m.Data.ToEquipmentModel(wo.AssignedEquipmentId, wo.ProcessSegmentId),
            CurrentStart = wo.PlannedStartAt, CurrentEnd = wo.PlannedEndAt,
            NewStart = m.Start, NewEnd = m.End, DurationMinutes = Math.Round(m.Minutes, 2),
            ExpectedUpdatedAt = wo.UpdatedAt, Conflicts = m.Conflicts
        });
    }

    public async Task<ProductionOrderResult<MoveApplyModel>> ApplyAsync(int id, MoveWorkOrderRequest request)
    {
        var move = await ComputeAsync(id, request, forApply: true);
        if (!move.IsSuccessful) return ProductionOrderResult<MoveApplyModel>.Failed(move.Failure, string.Join("; ", move.Errors.Distinct()));
        var m = move.Data!;
        var wo = m.WorkOrder;

        try
        {
            unitOfWork.BeginTransaction();
            wo.PlannedStartAt = m.Start;
            wo.PlannedEndAt = m.End;
            wo.PlannedAt = m.Now;
            wo.AssignedEquipmentId = m.Machine?.Id;
            wo.IsPlanLocked = true;
            wo.PlanLockedAt = m.Now;
            wo.UpdatedAt = m.Now;
            Persist.Ensure(await WorkOrders.UpdateAsync(wo), "Impossibile salvare lo spostamento del lavoro.");
            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            return ProductionOrderResult<MoveApplyModel>.Failed(ProductionOrderFailure.None, $"Impossibile spostare il lavoro: {ex.Message}");
        }

        return ProductionOrderResult<MoveApplyModel>.Ok(new MoveApplyModel { WorkOrder = m.Data.ToWorkOrderDetail(wo), Conflicts = m.Conflicts });
    }

    private async Task<ProductionOrderResult<Move>> ComputeAsync(int id, MoveWorkOrderRequest request, bool forApply)
    {
        var data = await loader.LoadAllAsync();
        if (!data.WorkOrders.TryGetValue(id, out var wo))
            return ProductionOrderResult<Move>.NotFound("Ordine di lavoro non trovato.");
        if (data.PlanCommandDenial(wo, "si sposta") is { } denial) return ProductionOrderResult<Move>.Conflict(denial);
        if (forApply && request.ExpectedUpdatedAt is { } expected && Math.Abs((AsUtc(expected) - AsUtc(wo.UpdatedAt)).TotalMilliseconds) > 1)
            return ProductionOrderResult<Move>.Conflict("Il lavoro è cambiato dopo l'anteprima.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var start = AsUtc(request.StartAt);
        if (start <= now) return ProductionOrderResult<Move>.Invalid("L'inizio deve essere dopo adesso.");

        var catalog = await catalogLoader.LoadAsync();
        foreach (var m in catalog.Machines.Values) data.Equipment.TryAdd(m.Id, m);
        foreach (var l in catalog.Links) data.EquipmentLinks.Add((l.EquipmentId, l.ProcessSegmentId));
        var directory = await calendars.LoadAsync();

        // Machine: requested, else the assigned one; required when the phase has active machines.
        var machines = await selector.MachinesOfSegmentAsync(wo.ProcessSegmentId);
        Equipment? machine = null;
        var chosen = request.EquipmentId ?? wo.AssignedEquipmentId;
        if (chosen is { } machineId)
        {
            var error = EquipmentSelector.Check(machines, machineId);
            if (error is not null)
            {
                if (request.EquipmentId is null && machines.Count > 0)
                {
                    var stale = data.Equipment.GetValueOrDefault(machineId)?.Code ?? "La macchina assegnata";
                    error = $"Scegli la macchina: {stale} non è più abilitata per questa fase.";
                }
                else if (request.EquipmentId is null)
                    error = null; // the phase lost all its machines: the old assignment is ignored
            }
            if (error is not null) return ProductionOrderResult<Move>.Invalid(error);
            machine = machines.FirstOrDefault(x => x.Equipment.Id == machineId)?.Equipment;
        }
        else if (EquipmentSelector.RequiresMachine(machines))
            return ProductionOrderResult<Move>.Invalid("Scegli la macchina: la fase ha macchine collegate.");

        var calc = directory.Calculator;
        var source = directory.ForEquipment(machine?.Id);
        var order = data.Orders[wo.ProductionOrderId];
        string Code(WorkOrder w) => ProductionData.CodeOf(data.Orders[w.ProductionOrderId], w);
        MoveJobRef Ref(WorkOrder w, DateTime? s, DateTime? e) => new(w.Id, Code(w), s, e);

        var predecessors = data.PredecessorIds(wo.Id).Select(p => data.WorkOrders[p])
            .Where(p => p.Status != WorkOrderStatus.Aborted)
            .Select(p => Ref(p, null, AsUtc(p.EndedAt ?? p.PlannedEndAt))).ToList();
        var successors = data.SuccessorIds(wo.Id).Distinct().Select(s => data.WorkOrders[s])
            .Where(s => s.Status is not (WorkOrderStatus.Ended or WorkOrderStatus.Aborted))
            .Select(s => Ref(s, AsUtc(s.PlannedStartAt), AsUtc(s.PlannedEndAt))).ToList();

        var occupants = new List<MoveJobRef>();
        if (machine is not null)
        {
            var openOrders = data.Orders.Values.Where(PlanningInputBuilder.IsPlannableOrder).Select(o => o.Id).ToHashSet();
            foreach (var other in data.WorkOrders.Values.Where(w => w.Id != wo.Id && openOrders.Contains(w.ProductionOrderId)))
            {
                if (other.Status is WorkOrderStatus.Waiting or WorkOrderStatus.Ready or WorkOrderStatus.Interrupted)
                {
                    if (other.AssignedEquipmentId == machine.Id && other.PlannedStartAt is { } s && other.PlannedEndAt is { } e)
                        occupants.Add(Ref(other, AsUtc(s), AsUtc(e)));
                }
                else if (other.Status == WorkOrderStatus.Running)
                {
                    var session = data.SessionsOf(other.Id).LastOrDefault(x => x.EndedAt is null);
                    if ((session?.EquipmentId ?? other.AssignedEquipmentId) != machine.Id) continue;
                    var left = WorkingTimeCalculator.ApplyEfficiency(ProductionEndEstimator.RemainingMinutes(data, other, now), machine.EfficiencyPercent);
                    var runEnd = calc.AddWorkingMinutes(source, now, left);
                    occupants.Add(Ref(other, AsUtc(session?.StartedAt ?? other.StartedAt ?? now), runEnd.IsSuccess ? runEnd.Value : now));
                }
            }
        }

        // Setup depends on the sequence: the job before the new start on the machine (planned or running), else the executed history.
        var requirements = MachineStartingState.RequirementsOf(data, wo);
        MachineSetupState? previous = null;
        if (machine is not null)
        {
            var before = occupants.Where(o => o.End is { } oe && oe <= start).OrderByDescending(o => o.End).ThenByDescending(o => o.Id).FirstOrDefault();
            previous = before is not null
                ? MachineSetupState.After(MachineStartingState.RequirementsOf(data, data.WorkOrders[before.Id]))
                : occupants.Any(o => o.Start is { } os && os < start) ? null : MachineStartingState.Of(data, machine.Id);
        }
        var setup = SetupCalculator.Compute(previous, requirements);
        var standard = SetupCalculator.WithSetup(ProductionEndEstimator.RemainingMinutes(data, wo, now), requirements.FullSetupMinutes, setup);
        var timing = ManualMoveCalculator.Evaluate(calc, source, machine?.Code ?? "Il calendario aziendale", start,
            standard, directory.EfficiencyOf(machine?.Id));
        if (timing.Error is not null) return ProductionOrderResult<Move>.Invalid(timing.Error);
        var end = timing.End!.Value;

        var conflicts = ManualMoveCalculator.FindConflicts(calc.Zone, Ref(wo, start, end), start, end, predecessors, successors,
            machine?.Id, machine?.Code, machine?.Capacity ?? 1, occupants, AsUtc(order.RequestedEnd));
        return ProductionOrderResult<Move>.Ok(new Move(data, wo, machine, start, end, timing.WorkMinutes, conflicts, now));
    }

    private static DateTime AsUtc(DateTime t) => t.Kind == DateTimeKind.Utc ? t : DateTime.SpecifyKind(t, DateTimeKind.Utc);
    private static DateTime? AsUtc(DateTime? t) => t is { } v ? AsUtc(v) : null;
}
