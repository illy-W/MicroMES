import type { WorkOrderStatus } from '@micromes/api-client/models';

/** Numero di tonalità `--chart-n` per l'identità degli ordini. */
export const PALETTE_SIZE = 8;

/** Valore CSS (custom property `--oc` sulla barra/chip) per l'n-esimo ordine, in ordine di prima apparizione. */
export const orderColor = (index: number): string => `var(--chart-${(index % PALETTE_SIZE) + 1})`;

interface StatusStyle {
  /** Bordo, sfondo (tinta dell'ordine) e testo. */
  box: string;
  /** Striscia piena sul bordo sinistro (4 px; 6 px se in esecuzione). */
  strip: string;
}

const STRIP = 'before:absolute before:inset-y-0 before:left-0 before:bg-(--oc)';
const TINT = (pct: number) => `bg-[color-mix(in_oklch,var(--oc)_${pct}%,var(--card))]`;

/**
 * Stato = pattern, non solo colore:
 * In attesa tratteggio chiaro · Pronto bordo pieno sottile · In esecuzione striscia larga + diagonale ·
 * In pausa punteggiato · Terminato grigio · Interrotto grigio con testo barrato.
 */
export const STATUS_STYLE: Record<WorkOrderStatus, StatusStyle> = {
  Waiting: { box: `border border-dashed border-(color:--oc) ${TINT(7)} text-foreground`, strip: `${STRIP} before:w-1` },
  Ready: { box: `border border-(color:--oc) ${TINT(16)} text-foreground`, strip: `${STRIP} before:w-1` },
  Running: { box: `tl-running border border-(color:--oc) ${TINT(28)} text-foreground shadow-sm`, strip: `${STRIP} before:w-1.5` },
  Interrupted: { box: `border-2 border-dotted border-(color:--oc) ${TINT(10)} text-foreground`, strip: `${STRIP} before:w-1` },
  Ended: { box: 'border border-border bg-muted text-muted-foreground', strip: `${STRIP} before:w-1 before:opacity-50` },
  Aborted: { box: 'border border-border bg-muted text-muted-foreground line-through', strip: `${STRIP} before:w-1 before:opacity-50` },
};

/** Nome della colonna "Op." dal codice lavoro `OP-2026-0003/20` → `20`. */
export const operationNumber = (workOrderCode: string): string | null => {
  const i = workOrderCode.lastIndexOf('/');
  return i >= 0 && i < workOrderCode.length - 1 ? workOrderCode.slice(i + 1) : null;
};

/** Livello di dettaglio della barra in base alla larghezza: 3 icona+2 righe, 2 solo fase, 1 solo codice fase, 0 blocco pieno. */
export const detailLevel = (width: number): 0 | 1 | 2 | 3 => (width >= 120 ? 3 : width >= 72 ? 2 : width >= 40 ? 1 : 0);
