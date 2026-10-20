export interface ValidationNode {
  id: string;
  operationNumber: number | null;
  label: string;
}

/** Convalida lato client del grafo; il backend resta l'autorità. Ciclo vuoto = valido. */
export function validateRouting(nodes: ValidationNode[], edges: { source: string; target: string }[]): string[] {
  const errors: string[] = [];
  if (!nodes.length) return errors;

  const invalid = nodes.filter((n) => n.operationNumber === null || !Number.isInteger(n.operationNumber) || n.operationNumber <= 0);
  if (invalid.length) errors.push('Il numero operazione deve essere un intero maggiore di zero.');

  const seen = new Map<number, number>();
  for (const n of nodes) {
    if (n.operationNumber !== null) seen.set(n.operationNumber, (seen.get(n.operationNumber) ?? 0) + 1);
  }
  const dups = [...seen].filter(([, c]) => c > 1).map(([op]) => op);
  if (dups.length) errors.push(`Numeri operazione duplicati: ${dups.join(', ')}.`);

  const ids = new Set(nodes.map((n) => n.id));
  const valid = edges.filter((e) => ids.has(e.source) && ids.has(e.target));
  if (valid.some((e) => e.source === e.target)) errors.push('Un passo non può dipendere da se stesso.');

  // Ciclo (Kahn)
  const indeg = new Map(nodes.map((n) => [n.id, 0]));
  for (const e of valid) indeg.set(e.target, (indeg.get(e.target) ?? 0) + 1);
  const queue = [...indeg].filter(([, d]) => d === 0).map(([id]) => id);
  let visited = 0;
  while (queue.length) {
    const id = queue.pop() as string;
    visited++;
    for (const e of valid) {
      if (e.source !== id) continue;
      const d = (indeg.get(e.target) ?? 0) - 1;
      indeg.set(e.target, d);
      if (d === 0) queue.push(e.target);
    }
  }
  const hasCycle = visited < nodes.length;
  if (hasCycle) errors.push('Il ciclo contiene un anello: i passi non possono dipendere l’uno dall’altro in circolo.');

  // Connesso (debolmente)
  if (nodes.length > 1) {
    const adj = new Map<string, string[]>(nodes.map((n) => [n.id, []]));
    for (const e of valid) {
      adj.get(e.source)?.push(e.target);
      adj.get(e.target)?.push(e.source);
    }
    const stack = [nodes[0].id];
    const reached = new Set(stack);
    while (stack.length) {
      for (const n of adj.get(stack.pop() as string) ?? []) {
        if (!reached.has(n)) {
          reached.add(n);
          stack.push(n);
        }
      }
    }
    if (reached.size < nodes.length) errors.push('Il ciclo deve essere connesso: ci sono passi isolati dal resto del diagramma.');
  }

  // Passo finale unico
  if (!hasCycle) {
    const withOut = new Set(valid.map((e) => e.source));
    const finals = nodes.filter((n) => !withOut.has(n.id));
    if (finals.length !== 1) {
      errors.push(
        finals.length === 0
          ? 'Manca il passo finale.'
          : `Deve esserci un solo passo finale; ne risultano ${finals.length} (${finals.map((n) => n.label).join(', ')}).`,
      );
    }
  }
  return errors;
}
