import type { WorkOrderStatus } from '@micromes/api-client/models';

/** Numero di tonalità `--chart-n` per l'identità degli ordini. */
export const PALETTE_SIZE = 8;

/** Valore CSS (custom property `--oc` sulla barra/chip) per l'n-esimo ordine, in ordine di prima apparizione. */
export const orderColor = (index: number): string => `var(--chart-${(index % PALETTE_SIZE) + 1})`;

interface StatusStyle {
  /** Livello "durata reale" (bordo pieno/tratteggiato, tinta) per barre larghe e per barre strette (senza bordi tratteggiati). */
  real: string;
  narrow: string;
  /** Livello esteso oltre la durata reale: tinta leggera, senza bordo. */
  ext: string;
  /** Colore del testo. */
  text: string;
  /** Striscia piena sul bordo sinistro (4 px; 6 px se in esecuzione). */
  strip: string;
}

const STRIP = 'before:absolute before:inset-y-0 before:left-0 before:bg-(--oc)';

/**
 * Stato = pattern, non solo colore. Nessuna tinta dell'ordine: lo sfondo è neutro (card), l'ordine si legge solo da striscia, traccia e chip; i bordi sono neutri.
 * In attesa tratteggio chiaro · Pronto bordo pieno · In esecuzione verde tenue · In pausa punteggiato · Terminato grigio · Interrotto barrato.
 * Tratteggi/punteggiati solo su barre >= 96 px (`narrow` altrove).
 */
export const STATUS_STYLE: Record<WorkOrderStatus, StatusStyle> = {
  Waiting: { real: 'border border-dashed border-foreground/40 bg-card', narrow: 'bg-card', ext: 'bg-muted/40', text: 'text-foreground', strip: `${STRIP} before:w-1` },
  Ready: { real: 'border border-foreground/50 bg-card', narrow: 'border border-foreground/50 bg-card', ext: 'bg-muted/40', text: 'text-foreground', strip: `${STRIP} before:w-1` },
  Running: {
    real: 'tl-running border border-running-foreground/25 bg-running shadow-sm',
    narrow: 'tl-running border border-running-foreground/25 bg-running shadow-sm',
    ext: 'bg-running/35',
    text: 'text-running-foreground',
    strip: `${STRIP} before:w-1.5 before:border-r-2 before:border-running-foreground/60`,
  },
  Interrupted: { real: 'border-2 border-dotted border-foreground/50 bg-card', narrow: 'bg-card', ext: 'bg-muted/40', text: 'text-foreground', strip: `${STRIP} before:w-1` },
  Ended: { real: 'border border-border bg-muted', narrow: 'border border-border bg-muted', ext: 'bg-muted', text: 'text-foreground/70', strip: `${STRIP} before:w-1 before:opacity-50` },
  Aborted: { real: 'border border-border bg-muted', narrow: 'border border-border bg-muted', ext: 'bg-muted', text: 'text-foreground/70 line-through', strip: `${STRIP} before:w-1 before:opacity-50` },
};

/** Nome della colonna "Op." dal codice lavoro `OP-2026-0003/20` → `20`. */
export const operationNumber = (workOrderCode: string): string | null => {
  const i = workOrderCode.lastIndexOf('/');
  return i >= 0 && i < workOrderCode.length - 1 ? workOrderCode.slice(i + 1) : null;
};
