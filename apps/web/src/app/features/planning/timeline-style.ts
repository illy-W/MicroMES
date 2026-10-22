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
 * Stato = pattern, non solo colore. Le tinte sono classi letterali `tl-tint-*` (styles.scss): Tailwind non genera classi interpolate.
 * In attesa tratteggio chiaro · Pronto bordo pieno · In esecuzione verde pieno · In pausa punteggiato · Terminato grigio · Interrotto barrato.
 * Tratteggi/punteggiati solo su barre >= 96 px (`narrow` altrove).
 */
export const STATUS_STYLE: Record<WorkOrderStatus, StatusStyle> = {
  Waiting: { real: 'border border-dashed border-(color:--oc) tl-tint-7', narrow: 'tl-tint-7', ext: 'tl-tint-7', text: 'text-foreground', strip: `${STRIP} before:w-1` },
  Ready: { real: 'border border-(color:--oc) tl-tint-16', narrow: 'border border-(color:--oc) tl-tint-16', ext: 'tl-tint-7', text: 'text-foreground', strip: `${STRIP} before:w-1` },
  Running: {
    real: 'tl-running border border-running bg-running shadow-sm',
    narrow: 'tl-running border border-running bg-running shadow-sm',
    ext: 'bg-running',
    text: 'text-running-foreground',
    strip: `${STRIP} before:w-1.5 before:border-r-2 before:border-running-foreground/80`,
  },
  Interrupted: { real: 'border-2 border-dotted border-(color:--oc) tl-tint-10', narrow: 'tl-tint-10', ext: 'tl-tint-7', text: 'text-foreground', strip: `${STRIP} before:w-1` },
  Ended: { real: 'border border-border bg-muted', narrow: 'border border-border bg-muted', ext: 'bg-muted', text: 'text-foreground/70', strip: `${STRIP} before:w-1 before:opacity-50` },
  Aborted: { real: 'border border-border bg-muted', narrow: 'border border-border bg-muted', ext: 'bg-muted', text: 'text-foreground/70 line-through', strip: `${STRIP} before:w-1 before:opacity-50` },
};

/** Nome della colonna "Op." dal codice lavoro `OP-2026-0003/20` → `20`. */
export const operationNumber = (workOrderCode: string): string | null => {
  const i = workOrderCode.lastIndexOf('/');
  return i >= 0 && i < workOrderCode.length - 1 ? workOrderCode.slice(i + 1) : null;
};
