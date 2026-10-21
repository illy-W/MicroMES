export const NUMBER_STEP = 10;

/** Numero proposto per un nuovo passo: massimo esistente + 10 (10 se il ciclo è vuoto). */
export function proposeNumber(existing: (number | null)[]): number {
  const max = Math.max(0, ...existing.map((n) => n ?? 0));
  return max + NUMBER_STEP;
}

export interface NumberingNode {
  id: string;
  operationNumber: number | null;
  positionY: number;
}

/**
 * Rinumera 10, 20, 30... in ordine topologico. A parità (passi pronti insieme) vince la posizione verticale,
 * poi il numero attuale. I passi di un eventuale ciclo seguono in coda con lo stesso criterio.
 * Restituisce id → nuovo numero; gli id non cambiano.
 */
export function renumber(nodes: NumberingNode[], edges: { source: string; target: string }[]): Map<string, number> {
  const ids = new Set(nodes.map((n) => n.id));
  const valid = edges.filter((e) => ids.has(e.source) && ids.has(e.target) && e.source !== e.target);
  const indeg = new Map(nodes.map((n) => [n.id, 0]));
  for (const e of valid) indeg.set(e.target, (indeg.get(e.target) ?? 0) + 1);

  const cmp = (a: NumberingNode, b: NumberingNode) =>
    a.positionY - b.positionY || (a.operationNumber ?? Infinity) - (b.operationNumber ?? Infinity);

  const done = new Set<string>();
  const order: string[] = [];
  while (order.length < nodes.length) {
    let ready = nodes.filter((n) => !done.has(n.id) && indeg.get(n.id) === 0);
    if (!ready.length) ready = nodes.filter((n) => !done.has(n.id)); // ciclo
    const next = [...ready].sort(cmp)[0];
    done.add(next.id);
    order.push(next.id);
    for (const e of valid) if (e.source === next.id) indeg.set(e.target, (indeg.get(e.target) ?? 0) - 1);
  }
  return new Map(order.map((id, i) => [id, (i + 1) * NUMBER_STEP]));
}

/** True se un passo ha numero maggiore o uguale a quello di un suo successore. */
export function numbersOutOfFlow(
  nodes: { id: string; operationNumber: number | null }[],
  edges: { source: string; target: string }[],
): boolean {
  const num = new Map(nodes.map((n) => [n.id, n.operationNumber]));
  return edges.some((e) => {
    const a = num.get(e.source);
    const b = num.get(e.target);
    return a != null && b != null && a >= b;
  });
}
