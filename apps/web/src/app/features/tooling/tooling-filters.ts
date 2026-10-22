export type ActiveFilter = 'active' | 'inactive' | 'all';

export const ACTIVE_FILTER_LABELS: Record<ActiveFilter, string> = {
  active: 'Solo attive',
  inactive: 'Solo disattivate',
  all: 'Tutte',
};

interface Filterable {
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

/** Filtra per testo (codice, nome, descrizione) e stato. Funzione pura. */
export function filterItems<T extends Filterable>(items: readonly T[], search: string, active: ActiveFilter): T[] {
  const q = search.trim().toLowerCase();
  return items.filter(
    (i) =>
      (active === 'all' || (active === 'active') === i.isActive) &&
      (!q || i.code.toLowerCase().includes(q) || i.name.toLowerCase().includes(q) || (i.description ?? '').toLowerCase().includes(q)),
  );
}

/** Colonna «Macchine compatibili»: nessun collegamento = compatibile con tutte. */
export function compatibleLabel(equipmentCount: number): string {
  if (equipmentCount <= 0) return 'Tutte';
  return equipmentCount === 1 ? '1 macchina' : `${equipmentCount} macchine`;
}

export function parseActiveFilter(v: string | null): ActiveFilter {
  return v === 'inactive' || v === 'all' ? v : 'active';
}
