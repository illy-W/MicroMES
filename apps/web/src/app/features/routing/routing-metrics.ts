export interface MetricsNode {
  id: string;
  /** Attrezzaggio effettivo (minuti). */
  setup: number;
  /** Tempo di lavorazione effettivo per unità (minuti). */
  run: number;
}

export interface RoutingMetrics {
  stepCount: number;
  /** Somma di attrezzaggio e lavorazione di tutti i passi per 1 unità; null se il grafo è ciclico. */
  workContent: number | null;
  /** Tempo del cammino più lungo (rami paralleli) per 1 unità; null se il grafo è ciclico. */
  longestPath: number | null;
  /** Passi sul percorso più lungo (vuoto se non calcolabile). */
  criticalNodeIds: ReadonlySet<string>;
  /** Archi sul percorso più lungo, nella forma `source->target` (vedi `edgeKey`). */
  criticalEdgeKeys: ReadonlySet<string>;
}

export function edgeKey(source: string, target: string): string {
  return `${source}->${target}`;
}

const EMPTY: ReadonlySet<string> = new Set<string>();

/**
 * Funzione pura: contenuto di lavoro e percorso più lungo (cammino di costo massimo in un DAG) per 1 unità.
 * Con un grafo ciclico i valori sono null e nessun passo è evidenziato.
 */
export function computeMetrics(nodes: MetricsNode[], edges: { source: string; target: string }[]): RoutingMetrics {
  const byId = new Map(nodes.map((n) => [n.id, n]));
  const valid = edges.filter((e) => byId.has(e.source) && byId.has(e.target) && e.source !== e.target);
  const time = (n: MetricsNode) => (Number.isFinite(n.setup) ? n.setup : 0) + (Number.isFinite(n.run) ? n.run : 0);

  const indeg = new Map(nodes.map((n) => [n.id, 0]));
  const preds = new Map<string, string[]>(nodes.map((n) => [n.id, []]));
  const succs = new Map<string, string[]>(nodes.map((n) => [n.id, []]));
  for (const e of valid) {
    indeg.set(e.target, (indeg.get(e.target) ?? 0) + 1);
    preds.get(e.target)?.push(e.source);
    succs.get(e.source)?.push(e.target);
  }

  const order: string[] = [];
  const queue = nodes.filter((n) => indeg.get(n.id) === 0).map((n) => n.id);
  while (queue.length) {
    const id = queue.shift() as string;
    order.push(id);
    for (const t of succs.get(id) ?? []) {
      const d = (indeg.get(t) ?? 0) - 1;
      indeg.set(t, d);
      if (d === 0) queue.push(t);
    }
  }
  if (order.length < nodes.length) {
    return { stepCount: nodes.length, workContent: null, longestPath: null, criticalNodeIds: EMPTY, criticalEdgeKeys: EMPTY };
  }

  const workContent = nodes.reduce((sum, n) => sum + time(n), 0);
  const dist = new Map<string, number>();
  const prev = new Map<string, string>();
  for (const id of order) {
    let best = 0;
    let from: string | undefined;
    for (const p of preds.get(id) ?? []) {
      const d = dist.get(p) ?? 0;
      if (from === undefined || d > best) {
        best = d;
        from = p;
      }
    }
    dist.set(id, best + time(byId.get(id) as MetricsNode));
    if (from !== undefined) prev.set(id, from);
  }

  let end: string | undefined;
  for (const id of order) {
    if (end === undefined || (dist.get(id) ?? 0) > (dist.get(end) ?? 0)) end = id;
  }
  const criticalNodeIds = new Set<string>();
  const criticalEdgeKeys = new Set<string>();
  for (let cur = end; cur !== undefined; cur = prev.get(cur)) {
    criticalNodeIds.add(cur);
    const p = prev.get(cur);
    if (p !== undefined) criticalEdgeKeys.add(edgeKey(p, cur));
  }
  return {
    stepCount: nodes.length,
    workContent,
    longestPath: end === undefined ? 0 : (dist.get(end) ?? 0),
    criticalNodeIds,
    criticalEdgeKeys,
  };
}

const minutesFmt = new Intl.NumberFormat('it-IT', { maximumFractionDigits: 1 });

/** "95 min · 1 h 35 min" (solo minuti sotto l'ora); "n/d" se il valore non è calcolabile. */
export function formatMinutes(minutes: number | null): string {
  if (minutes === null) return 'n/d';
  const base = `${minutesFmt.format(minutes)} min`;
  const total = Math.round(minutes);
  if (total < 60) return base;
  const h = Math.floor(total / 60);
  const m = total % 60;
  return `${base} · ${h} h${m ? ` ${m} min` : ''}`;
}
