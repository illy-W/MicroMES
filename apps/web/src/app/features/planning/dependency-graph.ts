/** Grafo di dipendenze tra lavori: id → predecessori e successori diretti. */
export interface DepGraph {
  preds: ReadonlyMap<number, readonly number[]>;
  succs: ReadonlyMap<number, readonly number[]>;
}

export function buildGraph(items: Iterable<{ id: number; predecessorIds: readonly number[] }>): DepGraph {
  const preds = new Map<number, number[]>();
  const succs = new Map<number, number[]>();
  for (const w of items) {
    preds.set(w.id, [...w.predecessorIds]);
    for (const p of w.predecessorIds) {
      const l = succs.get(p) ?? [];
      l.push(w.id);
      succs.set(p, l);
    }
  }
  return { preds, succs };
}

const walk = (start: number, next: ReadonlyMap<number, readonly number[]>): Set<number> => {
  const seen = new Set<number>();
  const stack = [start];
  while (stack.length) {
    for (const n of next.get(stack.pop() as number) ?? []) {
      if (n !== start && !seen.has(n)) {
        seen.add(n);
        stack.push(n);
      }
    }
  }
  return seen;
};

/** Catena transitiva a monte e a valle (a prova di cicli). */
export function chainOf(g: DepGraph, id: number): { up: Set<number>; down: Set<number> } {
  return { up: walk(id, g.preds), down: walk(id, g.succs) };
}
