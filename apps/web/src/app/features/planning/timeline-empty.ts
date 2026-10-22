export interface EmptyMessageInput {
  /** Stato dell'avviso finestra vuota (`onlyEnded` = solo fasi terminate nascoste). */
  state: 'empty' | 'onlyEnded';
  /** Etichette dei filtri attivi (es. «Solo fissati», «Stato: In attesa»). */
  activeFilters: readonly string[];
  lockedOnly: boolean;
  /** Lavori fissati nell'intero intervallo caricato. */
  lockedCount: number;
}

export interface EmptyMessage {
  kind: 'onlyEnded' | 'filters' | 'empty';
  title: string;
  hint: string | null;
}

/** Sceglie il messaggio di finestra vuota: nomina la causa quando ci sono filtri attivi. */
export function chooseEmptyMessage(i: EmptyMessageInput): EmptyMessage {
  if (i.state === 'onlyEnded') return { kind: 'onlyEnded', title: 'Nessun lavoro attivo in questa finestra', hint: 'Ci sono solo fasi terminate.' };
  if (i.activeFilters.length > 0) {
    const hint = i.lockedOnly && i.lockedCount === 0 ? 'Nessun lavoro è fissato. Fissa un lavoro dal suo pannello con «Fissa» o «Sposta…».' : null;
    return { kind: 'filters', title: 'Nessun lavoro corrisponde ai filtri', hint };
  }
  return { kind: 'empty', title: 'Nessun lavoro in questo periodo', hint: null };
}
