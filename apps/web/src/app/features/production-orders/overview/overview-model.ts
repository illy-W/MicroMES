import type { ProductionOrderOverviewModel, ProductionOrderOverviewWorkOrderModel } from '@micromes/api-client/models';
import type { Edge, GroupNode, Node } from 'ng-diagram';
import { formatMinutes } from '../../routing/routing-metrics';
import { orderStatus, workStatus, type StatusStyle } from '../production-order-labels';

/** Dimensioni iniziali (prima della misura/del layout). La card del lavoro ha larghezza e struttura fisse nel template. */
export const WORK_NODE_SIZE = { width: 288, height: 140 } as const;
export const GROUP_INITIAL_SIZE = { width: 300, height: 200 } as const;

export const WORK_NODE_TYPE = 'work';
export const ORDER_GROUP_TYPE = 'order';
export const OVERVIEW_EDGE_TYPE = 'dep';

export const workNodeId = (id: number): string => `wo-${id}`;
export const orderNodeId = (id: number): string => `po-${id}`;

export interface WorkNodeData {
  workOrderId: number;
  orderId: number;
  code: string;
  operationNumber: number;
  phaseCode: string;
  phaseName: string;
  status: string;
  plannedQuantity: number;
  goodQuantity: number;
  scrapQuantity: number;
  plannedMinutes: number;
  workedMinutes: number;
  critical: boolean;
  planStatus?: string;
  /** Macchina da mostrare: quella in uso se `Running`, altrimenti l'assegnata (assente se la fase non ne ha). */
  equipmentCode?: string;
  equipmentName?: string;
  /** True se la macchina è quella della sessione aperta (lavoro `Running`). */
  equipmentInUse?: boolean;
  [key: string]: unknown;
}

export interface OrderGroupData {
  orderId: number;
  code: string;
  materialCode: string;
  materialName: string;
  unitOfMeasure: string;
  quantity: number;
  status: string;
  producedQuantity: number;
  progressPercent: number;
  [key: string]: unknown;
}

export interface OverviewEdgeData {
  /** True se l'arco attraversa il bordo di un ordine figlio (ultimo lavoro del figlio -> lavoro iniziale del padre). */
  crossOrder: boolean;
  critical: boolean;
  [key: string]: unknown;
}

export interface OverviewGraph {
  nodes: Node[];
  edges: Edge[];
}

/** Macchina del nodo: in uso (sessione aperta) oppure assegnata; nessuna chiave se il lavoro non ne ha. */
function equipmentData(w: ProductionOrderOverviewWorkOrderModel): Pick<WorkNodeData, 'equipmentCode' | 'equipmentName' | 'equipmentInUse'> {
  const inUse = w.status === 'Running' ? w.currentEquipment : null;
  const eq = inUse ?? w.assignedEquipment;
  return eq ? { equipmentCode: eq.code, equipmentName: eq.name, equipmentInUse: !!inUse } : {};
}

/**
 * Costruisce nodi e archi ngDiagram dalla panoramica (nessun layout: posizioni a 0,0, il layout le scrive).
 * - `rootOrderId`: l'ordine richiesto. I suoi lavori sono al livello più alto; ogni altro ordine è un gruppo.
 * - L'appartenenza ai gruppi usa `groupId` (annidamento per BOM multilivello); le posizioni sono globali.
 */
export function buildOverviewGraph(overview: ProductionOrderOverviewModel, rootOrderId: number): OverviewGraph {
  const orderById = new Map(overview.orders.map((o) => [o.id, o]));
  const critical = new Set(overview.summary.criticalPathWorkOrderIds);
  const owner = new Map(overview.workOrders.map((w) => [w.id, w.productionOrderId]));

  /** Gruppo contenitore di un ordine: nessuno per la radice, altrimenti il gruppo del padre (nessuno se il padre è la radice). */
  const parentGroup = (parentOrderId?: number | null): string | undefined =>
    parentOrderId == null || parentOrderId === rootOrderId ? undefined : orderNodeId(parentOrderId);

  const depthOf = (orderId: number): number => {
    let d = 0;
    let cur = orderById.get(orderId);
    while (cur?.parentOrderId != null && d < 64) {
      cur = orderById.get(cur.parentOrderId);
      d++;
    }
    return d;
  };

  const groups: GroupNode<OrderGroupData>[] = overview.orders
    .filter((o) => o.id !== rootOrderId)
    .sort((a, b) => depthOf(a.id) - depthOf(b.id)) // i genitori prima dei figli
    .map((o) => ({
      id: orderNodeId(o.id),
      type: ORDER_GROUP_TYPE,
      isGroup: true,
      highlighted: false,
      groupId: parentGroup(o.parentOrderId),
      position: { x: 0, y: 0 },
      size: { ...GROUP_INITIAL_SIZE },
      autoSize: false,
      draggable: false,
      resizable: false,
      data: {
        orderId: o.id,
        code: o.code,
        materialCode: o.materialCode,
        materialName: o.materialName,
        unitOfMeasure: o.unitOfMeasure,
        quantity: o.quantity,
        status: o.status,
        producedQuantity: o.producedQuantity,
        progressPercent: o.progressPercent,
      },
    }));

  const works: Node<WorkNodeData>[] = overview.workOrders.map((w) => ({
    id: workNodeId(w.id),
    type: WORK_NODE_TYPE,
    groupId: w.productionOrderId === rootOrderId ? undefined : orderNodeId(w.productionOrderId),
    position: { x: 0, y: 0 },
    draggable: false,
    data: {
      workOrderId: w.id,
      orderId: w.productionOrderId,
      code: w.code,
      operationNumber: w.operationNumber,
      phaseCode: w.processSegmentCode,
      phaseName: w.processSegmentName,
      status: w.status,
      plannedQuantity: w.plannedQuantity,
      goodQuantity: w.goodQuantity,
      scrapQuantity: w.scrapQuantity,
      plannedMinutes: w.plannedMinutes,
      workedMinutes: w.workedMinutes,
      critical: critical.has(w.id),
      planStatus: w.planStatus,
      ...equipmentData(w),
    },
  }));

  const edges: Edge<OverviewEdgeData>[] = overview.dependencies
    .filter((d) => owner.has(d.predecessorWorkOrderId) && owner.has(d.successorWorkOrderId))
    .map((d) => ({
      id: `e-${d.predecessorWorkOrderId}-${d.successorWorkOrderId}`,
      type: OVERVIEW_EDGE_TYPE,
      source: workNodeId(d.predecessorWorkOrderId),
      sourcePort: 'out',
      target: workNodeId(d.successorWorkOrderId),
      targetPort: 'in',
      data: {
        crossOrder: owner.get(d.predecessorWorkOrderId) !== owner.get(d.successorWorkOrderId),
        critical: critical.has(d.predecessorWorkOrderId) && critical.has(d.successorWorkOrderId),
      },
    }));

  // groupId undefined va tolto: ng-diagram legge la presenza della chiave
  const clean = <T extends object>(n: T): T => {
    const copy = { ...n } as T & { groupId?: string };
    if (copy.groupId === undefined) delete copy.groupId;
    return copy;
  };
  return { nodes: [...groups, ...works].map(clean) as Node[], edges };
}

/** Stato e tono di un lavoro/ordine: riusa le etichette esistenti di production-orders. */
export const workStatusStyle = (status: string): StatusStyle => workStatus(status);
export const orderStatusStyle = (status: string): StatusStyle => orderStatus(status);

export interface OverviewSummaryView {
  total: number;
  ended: number;
  running: number;
  ready: number;
  waiting: number;
  /** Interrotti + abortiti. */
  problems: number;
  /** Lavori conclusi su totali (0-100, intero). */
  progressPercent: number;
  minimumDuration: string;
}

export function summarize(overview: ProductionOrderOverviewModel): OverviewSummaryView {
  const { counts, totalWorkOrders } = overview.summary;
  return {
    total: totalWorkOrders,
    ended: counts.ended,
    running: counts.running,
    ready: counts.ready,
    waiting: counts.waiting,
    problems: counts.interrupted + counts.aborted,
    progressPercent: totalWorkOrders > 0 ? Math.round((counts.ended / totalWorkOrders) * 100) : 0,
    minimumDuration: formatMinutes(totalWorkOrders > 0 ? overview.summary.minimumDurationMinutes : null),
  };
}

/** Oltre questo numero di nodi compare la minimappa. */
export const OVERVIEW_MINIMAP_THRESHOLD = 20;

/** Avanzamento di un lavoro: quantità buona su pianificata (0-100, intero). */
export const workProgressPercent = (good: number, planned: number): number =>
  planned > 0 ? Math.min(100, Math.max(0, Math.round((good / planned) * 100))) : 0;

const timeFmt = new Intl.DateTimeFormat('it-IT', { hour: '2-digit', minute: '2-digit' });
/** ISO UTC -> HH:mm in ora locale. */
export const formatTime = (iso: string): string => timeFmt.format(new Date(iso));
