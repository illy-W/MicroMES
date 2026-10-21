import type { Edge, Node, Point, Size } from 'ng-diagram';
import { GROUP_INITIAL_SIZE, WORK_NODE_SIZE } from './overview-model';

/** Spazio riservato in cima al gruppo per l'intestazione (codice, materiale, stato, avanzamento). */
export const GROUP_HEADER = 84;
const GROUP_PADDING = 20;

export interface PlacedNode {
  id: string;
  /** Posizione GLOBALE (ng-diagram salva coordinate globali anche per i figli di un gruppo). */
  position: Point;
  /** Solo per i gruppi: dimensione calcolata da ELK (i gruppi non crescono da soli). */
  size?: Size;
}

interface ElkLike {
  id: string;
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  layoutOptions?: Record<string, string>;
  children?: ElkLike[];
  edges?: { id: string; sources: string[]; targets: string[] }[];
}

/**
 * Layout con ELK a grafi annidati: ogni gruppo ng-diagram è un nodo ELK con `children`.
 * - `elk.hierarchyHandling: INCLUDE_CHILDREN` sulla radice: ELK lavora sull'intera gerarchia in un solo passaggio,
 *   quindi gli archi tra lavori di gruppi diversi (ultimo lavoro del figlio -> primo del padre) influenzano
 *   l'ordine dei livelli e vengono instradati attraverso il bordo del gruppo.
 * - Tutti gli archi sono dichiarati sulla radice (con INCLUDE_CHILDREN ELK risolve gli id a qualsiasi profondità).
 * - ELK restituisce le posizioni relative al contenitore: qui si sommano a quelle del gruppo (globali per ng-diagram).
 * ELK è importato in modo dinamico dal bundle (`elk.bundled.js`): `elkjs` semplice richiede `web-worker`.
 */
export async function layoutOverview(nodes: Node[], edges: Edge[]): Promise<PlacedNode[]> {
  const { default: ELK } = await import('elkjs/lib/elk.bundled.js');
  const elk = new ELK();

  const ids = new Set(nodes.map((n) => n.id));
  const byParent = new Map<string | undefined, Node[]>();
  for (const n of nodes) {
    const key = n.groupId && ids.has(n.groupId) ? n.groupId : undefined;
    byParent.set(key, [...(byParent.get(key) ?? []), n]);
  }

  const toElk = (n: Node): ElkLike => {
    if ('isGroup' in n && n.isGroup) {
      return {
        id: n.id,
        layoutOptions: {
          'elk.padding': `[top=${GROUP_HEADER},left=${GROUP_PADDING},bottom=${GROUP_PADDING},right=${GROUP_PADDING}]`,
        },
        children: (byParent.get(n.id) ?? []).map(toElk),
      };
    }
    return {
      id: n.id,
      width: n.size?.width ?? WORK_NODE_SIZE.width,
      height: n.size?.height ?? WORK_NODE_SIZE.height,
    };
  };

  const graph: ElkLike = {
    id: 'root',
    layoutOptions: {
      'elk.algorithm': 'layered',
      'elk.direction': 'RIGHT',
      'elk.hierarchyHandling': 'INCLUDE_CHILDREN',
      'elk.spacing.nodeNode': '40',
      'elk.layered.spacing.nodeNodeBetweenLayers': '70',
      'elk.padding': '[top=20,left=20,bottom=20,right=20]',
    },
    children: (byParent.get(undefined) ?? []).map(toElk),
    edges: edges
      .filter((e) => ids.has(e.source) && ids.has(e.target))
      .map((e) => ({ id: e.id, sources: [e.source], targets: [e.target] })),
  };

  const result = (await elk.layout(graph as never)) as unknown as ElkLike;

  const placed: PlacedNode[] = [];
  const walk = (parent: ElkLike, offX: number, offY: number): void => {
    for (const c of parent.children ?? []) {
      const x = offX + (c.x ?? 0);
      const y = offY + (c.y ?? 0);
      const src = nodes.find((n) => n.id === c.id);
      const isGroup = src !== undefined && 'isGroup' in src && src.isGroup;
      placed.push({
        id: c.id,
        position: { x, y },
        size: isGroup
          ? {
              width: Math.max(c.width ?? 0, GROUP_INITIAL_SIZE.width),
              height: Math.max(c.height ?? 0, GROUP_HEADER + 80),
            }
          : undefined,
      });
      walk(c, x, y);
    }
  };
  walk(result, 0, 0);
  return placed;
}
