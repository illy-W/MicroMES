export const COLUMN_SPACING = 260;
export const ROW_SPACING = 120;
const ORIGIN = 60;

export interface LayoutPoint {
  x: number;
  y: number;
}

/**
 * Layout a strati da sinistra a destra: la colonna è la profondità (percorso più lungo dai passi iniziali),
 * la riga segue l'ordine dei numeri operazione dentro la colonna.
 * `order` è il valore di ordinamento (numero operazione) di ogni nodo.
 */
export function layeredLayout(
  ids: string[],
  edges: { source: string; target: string }[],
  order: (id: string) => number,
): Map<string, LayoutPoint> {
  const preds = new Map<string, string[]>(ids.map((id) => [id, []]));
  for (const e of edges) preds.get(e.target)?.push(e.source);

  const depth = new Map<string, number>();
  const visiting = new Set<string>();
  const depthOf = (id: string): number => {
    const known = depth.get(id);
    if (known !== undefined) return known;
    if (visiting.has(id)) return 0; // ciclo: la convalida lo segnala a parte
    visiting.add(id);
    const d = Math.max(-1, ...(preds.get(id) ?? []).map(depthOf)) + 1;
    visiting.delete(id);
    depth.set(id, d);
    return d;
  };

  const columns = new Map<number, string[]>();
  for (const id of [...ids].sort((a, b) => order(a) - order(b))) {
    const d = depthOf(id);
    columns.set(d, [...(columns.get(d) ?? []), id]);
  }

  const result = new Map<string, LayoutPoint>();
  for (const [d, col] of columns) {
    col.forEach((id, row) => result.set(id, { x: ORIGIN + d * COLUMN_SPACING, y: ORIGIN + row * ROW_SPACING }));
  }
  return result;
}
