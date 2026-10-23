import type { Edge, GroupNode, Node } from 'ng-diagram';

export type FlowStatus = 'Ended' | 'Running' | 'Ready' | 'Waiting';

export interface FlowStatusStyle {
  label: string;
  icon: string;
  tone: string;
}

export const FLOW_STATUS: Record<FlowStatus, FlowStatusStyle> = {
  Ended: { label: 'Terminato', icon: 'lucideCircleCheck', tone: 'text-foreground' },
  Running: { label: 'In esecuzione', icon: 'lucidePlay', tone: 'text-signal' },
  Ready: { label: 'Pronto', icon: 'lucideCircleDot', tone: 'text-signal' },
  Waiting: { label: 'In attesa', icon: 'lucideClock', tone: 'text-muted-foreground' },
};

export interface FlowWorkData {
  [key: string]: unknown;
  operation: number;
  code: string;
  phase: string;
  status: FlowStatus;
  progress: number;
  good: number;
  planned: number;
  machine: string;
  critical: boolean;
}

export interface FlowOrderData {
  [key: string]: unknown;
  code: string;
  material: string;
  quantity: number;
  status: string;
  progress: number;
}

export const FLOW_WORK_TYPE = 'flow-work';
export const FLOW_GROUP_TYPE = 'flow-order';
export const FLOW_EDGE_TYPE = 'flow-edge';

export const FLOW_NODE_SIZE = { width: 250, height: 116 } as const;

/** Esempio statico: officina di mobili in acciaio e rovere. Posizioni calcolate a mano (coordinate globali). */
const work = (
  id: string,
  x: number,
  y: number,
  data: FlowWorkData,
  groupId?: string,
): Node<FlowWorkData> => ({
  id,
  type: FLOW_WORK_TYPE,
  position: { x, y },
  draggable: false,
  data,
  ...(groupId ? { groupId } : {}),
});

const GROUP: GroupNode<FlowOrderData> = {
  id: 'ord-telaio',
  type: FLOW_GROUP_TYPE,
  isGroup: true,
  highlighted: false,
  position: { x: 0, y: 0 },
  size: { width: 890, height: 224 },
  autoSize: false,
  draggable: false,
  resizable: false,
  data: {
    code: 'OP-2026-0012/1',
    material: 'SL-SED-TEL Telaio saldato e verniciato',
    quantity: 12,
    status: 'In corso',
    progress: 33,
  },
};

export const FLOW_NODES: Node[] = [
  GROUP,
  work('w-taglio-tubi', 20, 92, { operation: 10, code: 'OP-2026-0012/1/10', phase: 'Taglio tubolare', status: 'Ended', progress: 100, good: 12, planned: 12, machine: 'TAG-TUB-1', critical: true }, 'ord-telaio'),
  work('w-saldatura', 320, 92, { operation: 20, code: 'OP-2026-0012/1/20', phase: 'Saldatura', status: 'Running', progress: 58, good: 7, planned: 12, machine: 'SAL-1', critical: true }, 'ord-telaio'),
  work('w-verniciatura', 620, 92, { operation: 30, code: 'OP-2026-0012/1/30', phase: 'Verniciatura a polvere', status: 'Waiting', progress: 0, good: 0, planned: 12, machine: 'VER-POL-1', critical: true }, 'ord-telaio'),
  work('w-taglio-rovere', 20, 276, { operation: 10, code: 'OP-2026-0012/10', phase: 'Taglio rovere', status: 'Ended', progress: 100, good: 12, planned: 12, machine: 'SEG-1', critical: false }),
  work('w-sagomatura', 320, 276, { operation: 20, code: 'OP-2026-0012/20', phase: 'Sagomatura sedile', status: 'Ready', progress: 0, good: 0, planned: 12, machine: 'FRE-1', critical: false }),
  work('w-assemblaggio', 960, 140, { operation: 30, code: 'OP-2026-0012/30', phase: 'Assemblaggio', status: 'Waiting', progress: 0, good: 0, planned: 12, machine: 'ASS-B1', critical: true }),
];

const edge = (from: string, to: string, critical: boolean, crossOrder = false): Edge => ({
  id: `e-${from}-${to}`,
  type: FLOW_EDGE_TYPE,
  source: from,
  sourcePort: 'out',
  target: to,
  targetPort: 'in',
  data: { critical, crossOrder },
});

export const FLOW_EDGES: Edge[] = [
  edge('w-taglio-tubi', 'w-saldatura', true),
  edge('w-saldatura', 'w-verniciatura', true),
  edge('w-verniciatura', 'w-assemblaggio', true, true),
  edge('w-taglio-rovere', 'w-sagomatura', false),
  edge('w-sagomatura', 'w-assemblaggio', false),
];

/** Elenco testuale equivalente al diagramma (per tastiera e lettori di schermo). */
export const FLOW_TEXT: { order: string; items: { phase: string; status: string; note: string }[] }[] = [
  {
    order: 'Ordine figlio OP-2026-0012/1, Telaio saldato e verniciato',
    items: [
      { phase: 'Taglio tubolare', status: 'Terminato', note: 'sul percorso più lungo' },
      { phase: 'Saldatura', status: 'In esecuzione', note: 'sul percorso più lungo, dopo il taglio' },
      { phase: 'Verniciatura a polvere', status: 'In attesa', note: 'sul percorso più lungo, dopo la saldatura' },
    ],
  },
  {
    order: 'Ordine OP-2026-0012, Sedia in acciaio e rovere',
    items: [
      { phase: 'Taglio rovere', status: 'Terminato', note: 'prima della sagomatura' },
      { phase: 'Sagomatura sedile', status: 'Pronto', note: 'dopo il taglio del rovere' },
      { phase: 'Assemblaggio', status: 'In attesa', note: 'parte solo con telaio verniciato e sedile sagomato; sul percorso più lungo' },
    ],
  },
];
