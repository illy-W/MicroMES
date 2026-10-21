using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.ProductionOrderFeature.MachineStates;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Planning;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Services;

/// <summary>
/// In-memory snapshot of one family of orders (a root order with all its descendants), or of every order.
/// Holds the tracked entities the commands mutate and builds the read models from them.
/// </summary>
public class ProductionData(TimeProvider timeProvider)
{
    public Dictionary<int, ProductionOrder> Orders { get; } = [];
    public Dictionary<int, WorkOrder> WorkOrders { get; } = [];
    public List<WorkOrderDependency> Dependencies { get; } = [];
    public List<WorkOrderResponse> Sessions { get; } = [];
    public List<WorkOrderMaterial> Lines { get; } = [];
    public List<WorkOrderResponseScrap> Scraps { get; } = [];
    public List<WorkOrderResponseCorrection> Corrections { get; } = [];
    public Dictionary<int, Material> Materials { get; } = [];
    public Dictionary<int, Equipment> Equipment { get; } = [];
    /// <summary>(machine, process segment) pairs of the machines linked to the segments of the loaded work orders.</summary>
    public HashSet<(int EquipmentId, int ProcessSegmentId)> EquipmentLinks { get; } = [];

    public TimeProvider TimeProvider => timeProvider;

    // ---- navigation helpers -------------------------------------------------------------------------

    public IEnumerable<WorkOrder> WorkOrdersOf(int orderId) => WorkOrders.Values.Where(w => w.ProductionOrderId == orderId);

    public IEnumerable<ProductionOrder> ChildrenOf(int orderId) => Orders.Values.Where(o => o.ParentOrderId == orderId).OrderBy(o => o.Id);

    public IEnumerable<int> PredecessorIds(int workOrderId)
        => Dependencies.Where(d => d.SuccessorWorkOrderId == workOrderId).Select(d => d.PredecessorWorkOrderId);

    public IEnumerable<int> SuccessorIds(int workOrderId)
        => Dependencies.Where(d => d.PredecessorWorkOrderId == workOrderId).Select(d => d.SuccessorWorkOrderId);

    public IEnumerable<WorkOrderResponse> SessionsOf(int workOrderId)
        => Sessions.Where(s => s.WorkOrderId == workOrderId).OrderBy(s => s.StartedAt).ThenBy(s => s.Id);

    public WorkOrderMachineState MachineOf(WorkOrder wo)
    {
        var sessions = SessionsOf(wo.Id).ToList();
        var open = sessions.LastOrDefault(s => s.EndedAt is null);
        var last = sessions.LastOrDefault(s => s.EndedAt is not null);
        return new WorkOrderMachineState(wo, open, last, timeProvider);
    }

    /// <summary>The order plus all its descendants.</summary>
    public List<ProductionOrder> Subtree(int orderId)
    {
        var result = new List<ProductionOrder>();
        var stack = new Stack<int>();
        stack.Push(orderId);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            result.Add(Orders[id]);
            foreach (var c in ChildrenOf(id)) stack.Push(c.Id);
        }
        return result;
    }

    public bool HasStartedWork(int orderId)
        => Subtree(orderId).Any(o => WorkOrdersOf(o.Id).Any(w => w.StartedAt is not null));

    public ProductionOrderMachineState MachineOf(ProductionOrder order)
        => new(order, HasStartedWork(order.Id), timeProvider);

    /// <summary>Work orders of the order with no successor inside the order itself.</summary>
    public List<WorkOrder> FinalWorkOrders(int orderId)
    {
        var own = WorkOrdersOf(orderId).ToList();
        var ownIds = own.Select(w => w.Id).ToHashSet();
        return own.Where(w => !SuccessorIds(w.Id).Any(ownIds.Contains)).ToList();
    }

    public decimal ProducedQuantity(int orderId)
        => FinalWorkOrders(orderId).Sum(w => SessionsOf(w.Id).Sum(s => s.GoodQuantity));

    public decimal WorkedMinutes(int workOrderId)
        => SessionsOf(workOrderId).Where(s => s.EndedAt is not null).Sum(s => Minutes(s));

    private static decimal Minutes(WorkOrderResponse s)
        => Math.Round((decimal)(s.EndedAt!.Value - s.StartedAt).TotalMinutes, 2);

    public static decimal Progress(decimal produced, decimal quantity)
        => quantity <= 0 ? 0m : Math.Round(Math.Min(100m, produced / quantity * 100m), 2);

    public static decimal PlannedMinutes(WorkOrder wo)
        => ProductionOrderPlanner.PlannedMinutes(wo.SetupMinutes, wo.RunMinutesPerUnit, wo.PlannedQuantity);

    public static string CodeOf(ProductionOrder order, WorkOrder wo) => $"{order.Code}/{wo.OperationNumber}";

    // ---- read models --------------------------------------------------------------------------------

    public ProductionOrderModel ToOrderModel(ProductionOrder o)
    {
        var material = Materials[o.MaterialId];
        var produced = ProducedQuantity(o.Id);
        return new ProductionOrderModel
        {
            Id = o.Id,
            Code = o.Code,
            MaterialId = o.MaterialId,
            MaterialCode = material.Code,
            MaterialName = material.Name,
            UnitOfMeasure = material.UnitOfMeasure,
            Quantity = o.Quantity,
            Status = o.Status,
            Priority = o.Priority,
            RequestedStart = o.RequestedStart,
            RequestedEnd = o.RequestedEnd,
            Notes = o.Notes,
            ParentOrderId = o.ParentOrderId,
            ParentOrderCode = o.ParentOrderId is { } pid && Orders.TryGetValue(pid, out var parent) ? parent.Code : null,
            ReleasedAt = o.ReleasedAt,
            StartedAt = o.StartedAt,
            EndedAt = o.EndedAt,
            ProducedQuantity = produced,
            ProgressPercent = Progress(produced, o.Quantity),
            // Child orders follow the state of their parent: no direct commands.
            AllowedActions = o.ParentOrderId is null ? MachineOf(o).AllowedActions.ToList() : [],
            CreatedAt = o.CreatedAt,
            UpdatedAt = o.UpdatedAt
        };
    }

    public WorkOrderModel ToWorkOrderModel(WorkOrder wo)
    {
        var order = Orders[wo.ProductionOrderId];
        var material = Materials[order.MaterialId];
        var sessions = SessionsOf(wo.Id).ToList();
        return new WorkOrderModel
        {
            Id = wo.Id,
            Code = CodeOf(order, wo),
            ProductionOrderId = order.Id,
            ProductionOrderCode = order.Code,
            ProductionOrderStatus = order.Status,
            MaterialId = material.Id,
            MaterialCode = material.Code,
            MaterialName = material.Name,
            UnitOfMeasure = material.UnitOfMeasure,
            OperationNumber = wo.OperationNumber,
            ProcessSegmentId = wo.ProcessSegmentId,
            ProcessSegmentCode = wo.ProcessSegmentCode,
            ProcessSegmentName = wo.ProcessSegmentName,
            SetupMinutes = wo.SetupMinutes,
            RunMinutesPerUnit = wo.RunMinutesPerUnit,
            PlannedQuantity = wo.PlannedQuantity,
            PlannedMinutes = PlannedMinutes(wo),
            WorkedMinutes = WorkedMinutes(wo.Id),
            GoodQuantity = sessions.Sum(s => s.GoodQuantity),
            ScrapQuantity = sessions.Sum(s => s.ScrapQuantity),
            ShortfallQuantity = wo.Status == WorkOrderStatus.Ended
                ? Math.Max(0m, wo.PlannedQuantity - sessions.Sum(s => s.GoodQuantity)) : 0m,
            Notes = wo.Notes,
            AssignedEquipment = ToEquipmentModel(wo.AssignedEquipmentId, wo.ProcessSegmentId),
            CurrentEquipment = wo.Status == WorkOrderStatus.Running
                ? ToEquipmentModel(sessions.LastOrDefault(s => s.EndedAt is null)?.EquipmentId, wo.ProcessSegmentId) : null,
            Status = wo.Status,
            StartedAt = wo.StartedAt,
            EndedAt = wo.EndedAt,
            Predecessors = PredecessorIds(wo.Id).Select(ToLink).OrderBy(l => l.Code, StringComparer.Ordinal).ToList(),
            Successors = SuccessorIds(wo.Id).Select(ToLink).OrderBy(l => l.Code, StringComparer.Ordinal).ToList(),
            AllowedActions = MachineOf(wo).AllowedActions.ToList()
        };
    }

    public WorkOrderEquipmentModel? ToEquipmentModel(int? equipmentId, int processSegmentId)
        => equipmentId is { } id && Equipment.TryGetValue(id, out var e)
            ? new WorkOrderEquipmentModel
            {
                Id = e.Id, Code = e.Code, Name = e.Name, IsActive = e.IsActive,
                IsEnabled = e.IsActive && EquipmentLinks.Contains((e.Id, processSegmentId))
            }
            : null;

    private WorkOrderLinkModel ToLink(int workOrderId)
    {
        var w = WorkOrders[workOrderId];
        return new WorkOrderLinkModel { Id = w.Id, Code = CodeOf(Orders[w.ProductionOrderId], w), Status = w.Status };
    }

    public WorkOrderDetailModel ToWorkOrderDetail(WorkOrder wo)
    {
        var m = ToWorkOrderModel(wo);
        return new WorkOrderDetailModel
        {
            Id = m.Id, Code = m.Code, ProductionOrderId = m.ProductionOrderId, ProductionOrderCode = m.ProductionOrderCode,
            ProductionOrderStatus = m.ProductionOrderStatus, MaterialId = m.MaterialId, MaterialCode = m.MaterialCode,
            MaterialName = m.MaterialName, UnitOfMeasure = m.UnitOfMeasure, OperationNumber = m.OperationNumber,
            ProcessSegmentId = m.ProcessSegmentId, ProcessSegmentCode = m.ProcessSegmentCode,
            ProcessSegmentName = m.ProcessSegmentName, SetupMinutes = m.SetupMinutes, RunMinutesPerUnit = m.RunMinutesPerUnit,
            PlannedQuantity = m.PlannedQuantity, PlannedMinutes = m.PlannedMinutes, WorkedMinutes = m.WorkedMinutes,
            GoodQuantity = m.GoodQuantity, ScrapQuantity = m.ScrapQuantity, ShortfallQuantity = m.ShortfallQuantity,
            Notes = m.Notes, Status = m.Status,
            AssignedEquipment = m.AssignedEquipment, CurrentEquipment = m.CurrentEquipment,
            StartedAt = m.StartedAt, EndedAt = m.EndedAt, Predecessors = m.Predecessors, Successors = m.Successors,
            AllowedActions = m.AllowedActions,
            Sessions = SessionsOf(wo.Id).Select(s => new WorkOrderSessionModel
            {
                Id = s.Id,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                GoodQuantity = s.GoodQuantity,
                ScrapQuantity = s.ScrapQuantity,
                Notes = s.Notes,
                EquipmentId = s.EquipmentId, EquipmentCode = s.EquipmentCode, EquipmentName = s.EquipmentName,
                Minutes = s.EndedAt is null ? null : Minutes(s),
                ScrapEntries = Scraps.Where(e => e.WorkOrderResponseId == s.Id).OrderBy(e => e.Id)
                    .Select(e => new WorkOrderScrapEntryModel
                    {
                        Id = e.Id, ScrapReasonId = e.ScrapReasonId, ReasonCode = e.ReasonCode,
                        ReasonName = e.ReasonName, Quantity = e.Quantity
                    }).ToList(),
                IsCorrected = Corrections.Any(c => c.WorkOrderResponseId == s.Id),
                Corrections = Corrections.Where(c => c.WorkOrderResponseId == s.Id).OrderBy(c => c.CorrectedAt).ThenBy(c => c.Id)
                    .Select(c => new WorkOrderCorrectionModel
                    {
                        Id = c.Id, CorrectedAt = c.CorrectedAt, Notes = c.Notes,
                        PreviousGoodQuantity = c.PreviousGoodQuantity, PreviousScrapQuantity = c.PreviousScrapQuantity,
                        PreviousScrapDetail = c.PreviousScrapDetail, NewGoodQuantity = c.NewGoodQuantity,
                        NewScrapQuantity = c.NewScrapQuantity, NewScrapDetail = c.NewScrapDetail,
                        PreviousEquipmentCode = c.PreviousEquipmentCode, PreviousEquipmentName = c.PreviousEquipmentName,
                        NewEquipmentCode = c.NewEquipmentCode, NewEquipmentName = c.NewEquipmentName
                    }).ToList()
            }).ToList(),
            Materials = Lines.Where(l => l.WorkOrderId == wo.Id).OrderBy(l => l.Id).Select(ToMaterialModel).ToList()
        };
    }

    private WorkOrderMaterialModel ToMaterialModel(WorkOrderMaterial l)
    {
        var wo = WorkOrders[l.WorkOrderId];
        var material = Materials[l.MaterialId];
        return new WorkOrderMaterialModel
        {
            Id = l.Id,
            WorkOrderId = wo.Id,
            WorkOrderCode = CodeOf(Orders[wo.ProductionOrderId], wo),
            MaterialId = material.Id,
            MaterialCode = material.Code,
            MaterialName = material.Name,
            UnitOfMeasure = material.UnitOfMeasure,
            RequiredQuantity = l.RequiredQuantity,
            ChildProductionOrderId = l.ChildProductionOrderId,
            ChildProductionOrderCode = l.ChildProductionOrderId is { } cid && Orders.TryGetValue(cid, out var child) ? child.Code : null
        };
    }

    public ProductionOrderDetailModel ToOrderDetail(ProductionOrder order)
    {
        var header = ToOrderModel(order);
        var own = WorkOrdersOf(order.Id).OrderBy(w => w.OperationNumber).ToList();
        var ownIds = own.Select(w => w.Id).ToHashSet();

        return new ProductionOrderDetailModel
        {
            Id = header.Id, Code = header.Code, MaterialId = header.MaterialId, MaterialCode = header.MaterialCode,
            MaterialName = header.MaterialName, UnitOfMeasure = header.UnitOfMeasure, Quantity = header.Quantity,
            Status = header.Status, Priority = header.Priority, RequestedStart = header.RequestedStart,
            RequestedEnd = header.RequestedEnd, Notes = header.Notes, ParentOrderId = header.ParentOrderId,
            ParentOrderCode = header.ParentOrderCode, ReleasedAt = header.ReleasedAt, StartedAt = header.StartedAt,
            EndedAt = header.EndedAt, ProducedQuantity = header.ProducedQuantity, ProgressPercent = header.ProgressPercent,
            AllowedActions = header.AllowedActions, CreatedAt = header.CreatedAt, UpdatedAt = header.UpdatedAt,
            WorkOrders = own.Select(ToWorkOrderModel).ToList(),
            Dependencies = Dependencies
                .Where(d => ownIds.Contains(d.PredecessorWorkOrderId) || ownIds.Contains(d.SuccessorWorkOrderId))
                .OrderBy(d => d.Id)
                .Select(d => new WorkOrderDependencyModel { PredecessorWorkOrderId = d.PredecessorWorkOrderId, SuccessorWorkOrderId = d.SuccessorWorkOrderId })
                .ToList(),
            Materials = Lines.Where(l => ownIds.Contains(l.WorkOrderId)).OrderBy(l => l.Id).Select(ToMaterialModel).ToList(),
            Children = ChildrenOf(order.Id).Select(ToTree).ToList(),
            Summary = BuildSummary(order, own)
        };
    }

    private ProductionOrderTreeModel ToTree(ProductionOrder o)
    {
        var material = Materials[o.MaterialId];
        var produced = ProducedQuantity(o.Id);
        return new ProductionOrderTreeModel
        {
            Id = o.Id,
            Code = o.Code,
            MaterialId = material.Id,
            MaterialCode = material.Code,
            MaterialName = material.Name,
            UnitOfMeasure = material.UnitOfMeasure,
            Quantity = o.Quantity,
            Status = o.Status,
            ProducedQuantity = produced,
            ProgressPercent = Progress(produced, o.Quantity),
            Children = ChildrenOf(o.Id).Select(ToTree).ToList()
        };
    }

    private ProductionOrderSummaryModel BuildSummary(ProductionOrder order, List<WorkOrder> own)
    {
        var treeOrderIds = Subtree(order.Id).Select(o => o.Id).ToHashSet();
        var treeWorkOrders = WorkOrders.Values.Where(w => treeOrderIds.Contains(w.ProductionOrderId)).ToList();
        var durations = treeWorkOrders.ToDictionary(w => w.Id, PlannedMinutes);
        var finish = ProductionOrderPlanner.EarliestFinish(durations, Dependencies.Select(d => (d.PredecessorWorkOrderId, d.SuccessorWorkOrderId)));

        var ownSessions = own.SelectMany(w => SessionsOf(w.Id)).ToList();
        return new ProductionOrderSummaryModel
        {
            WorkContentMinutes = durations.Values.Sum(),
            MinimumDurationMinutes = finish.Count == 0 ? 0m : finish.Values.Max(),
            WorkedMinutes = treeWorkOrders.Sum(w => WorkedMinutes(w.Id)),
            ProducedQuantity = ProducedQuantity(order.Id),
            ScrapQuantity = ownSessions.Sum(s => s.ScrapQuantity)
        };
    }

    /// <summary>Overview of the order and its descendants (flat), reusing the existing duration and progress logic.</summary>
    public ProductionOrderOverviewModel ToOverview(int orderId)
    {
        var orders = Subtree(orderId).OrderBy(o => o.Id).ToList();
        var orderIds = orders.Select(o => o.Id).ToHashSet();
        var workOrders = WorkOrders.Values.Where(w => orderIds.Contains(w.ProductionOrderId))
            .OrderBy(w => w.ProductionOrderId).ThenBy(w => w.OperationNumber).ToList();
        var woIds = workOrders.Select(w => w.Id).ToHashSet();
        var deps = Dependencies.Where(d => woIds.Contains(d.PredecessorWorkOrderId) && woIds.Contains(d.SuccessorWorkOrderId))
            .OrderBy(d => d.Id).ToList();

        var durations = workOrders.ToDictionary(w => w.Id, PlannedMinutes);
        var pairs = deps.Select(d => (d.PredecessorWorkOrderId, d.SuccessorWorkOrderId)).ToList();
        var finish = ProductionOrderPlanner.EarliestFinish(durations, pairs);

        int Count(WorkOrderStatus s) => workOrders.Count(w => w.Status == s);

        return new ProductionOrderOverviewModel
        {
            Orders = orders.Select(o =>
            {
                var m = ToOrderModel(o);
                return new ProductionOrderOverviewOrderModel
                {
                    Id = m.Id, Code = m.Code, ParentOrderId = o.Id == orderId ? null : m.ParentOrderId,
                    MaterialId = m.MaterialId, MaterialCode = m.MaterialCode, MaterialName = m.MaterialName,
                    UnitOfMeasure = m.UnitOfMeasure, Quantity = m.Quantity, Status = m.Status,
                    ProducedQuantity = m.ProducedQuantity, ProgressPercent = m.ProgressPercent
                };
            }).ToList(),
            WorkOrders = workOrders.Select(w =>
            {
                var m = ToWorkOrderModel(w);
                return new ProductionOrderOverviewWorkOrderModel
                {
                    Id = m.Id, ProductionOrderId = m.ProductionOrderId, Code = m.Code, OperationNumber = m.OperationNumber,
                    ProcessSegmentCode = m.ProcessSegmentCode, ProcessSegmentName = m.ProcessSegmentName, Status = m.Status,
                    AssignedEquipment = m.AssignedEquipment, CurrentEquipment = m.CurrentEquipment,
                    PlannedQuantity = m.PlannedQuantity, GoodQuantity = m.GoodQuantity, ScrapQuantity = m.ScrapQuantity,
                    PlannedMinutes = m.PlannedMinutes, WorkedMinutes = m.WorkedMinutes, AllowedActions = m.AllowedActions,
                    Materials = Lines.Where(l => l.WorkOrderId == w.Id).OrderBy(l => l.Id).Select(ToMaterialModel).ToList()
                };
            }).ToList(),
            Dependencies = deps.Select(d => new WorkOrderDependencyModel
            {
                PredecessorWorkOrderId = d.PredecessorWorkOrderId, SuccessorWorkOrderId = d.SuccessorWorkOrderId
            }).ToList(),
            Summary = new ProductionOrderOverviewSummaryModel
            {
                TotalWorkOrders = workOrders.Count,
                Counts = new WorkOrderStatusCountsModel
                {
                    Waiting = Count(WorkOrderStatus.Waiting), Ready = Count(WorkOrderStatus.Ready),
                    Running = Count(WorkOrderStatus.Running), Interrupted = Count(WorkOrderStatus.Interrupted),
                    Ended = Count(WorkOrderStatus.Ended), Aborted = Count(WorkOrderStatus.Aborted)
                },
                MinimumDurationMinutes = finish.Count == 0 ? 0m : finish.Values.Max(),
                CriticalPathWorkOrderIds = ProductionOrderPlanner.CriticalPath(durations, pairs),
                WorkContentMinutes = durations.Values.Sum(),
                WorkedMinutes = workOrders.Sum(w => WorkedMinutes(w.Id))
            },
            GeneratedAt = timeProvider.GetUtcNow().UtcDateTime
        };
    }
}
