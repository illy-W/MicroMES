/** Posizione dell'etichetta di una barra: dentro, a destra/sinistra (stile Gantt), solo codice fase, oppure nessuna. */
export type LabelPlacement = 'inside' | 'right' | 'left' | 'code' | 'none';

export interface LabelInput {
  /** Rettangolo orizzontale della barra (px nell'area righe). */
  left: number;
  width: number;
  /** Fine della barra/etichetta precedente nella stessa corsia (0 se nessuna) e inizio della barra seguente (Infinity se nessuna). */
  prevEnd: number;
  nextStart: number;
  /** Larghezza stimata dell'etichetta completa e del solo codice fase (incluso il padding del chip). */
  labelW: number;
  codeW: number;
  /** Limite destro dell'area (larghezza totale dell'asse). */
  maxRight: number;
  /** Larghezza minima della barra per tenere l'etichetta dentro. */
  insideMin?: number;
  gap?: number;
}

export interface LabelPlacementResult {
  placement: LabelPlacement;
  /** Lato dello chip per `right`, `left` e `code`. */
  side: 'right' | 'left' | null;
}

export const LABEL_INSIDE_MIN = 120;

/** Stima della larghezza (px) di una riga di testo: `charW` px per carattere + padding. */
export const estimateTextWidth = (text: string, charW: number, pad = 12): number => Math.ceil(text.length * charW + pad);

/**
 * Sceglie dove mostrare l'etichetta di una barra senza coprire barre della stessa corsia.
 * Ordine: dentro (barra larga) → destra → sinistra → solo codice (destra, poi sinistra) → nessuna.
 */
export function placeLabel(i: LabelInput): LabelPlacementResult {
  const gap = i.gap ?? 4;
  if (i.width >= (i.insideMin ?? LABEL_INSIDE_MIN)) return { placement: 'inside', side: null };
  const right = i.left + i.width + gap;
  const rightRoom = Math.min(i.nextStart - gap, i.maxRight) - right;
  const leftRoom = i.left - gap - Math.max(i.prevEnd + gap, 0);
  if (rightRoom >= i.labelW) return { placement: 'right', side: 'right' };
  if (leftRoom >= i.labelW) return { placement: 'left', side: 'left' };
  if (rightRoom >= i.codeW) return { placement: 'code', side: 'right' };
  if (leftRoom >= i.codeW) return { placement: 'code', side: 'left' };
  return { placement: 'none', side: null };
}

/** Spazio extra del chip: bordo colore + icona stato (completo) o solo bordo (codice). */
export const CHIP_EXTRA_FULL = 22;
export const CHIP_EXTRA_CODE = 4;

/**
 * Etichetta a sinistra di una barra: `right` è il bordo destro dello chip (nelle coordinate dell'asse) e `minLeft` il primo x visibile
 * (lo scorrimento orizzontale: più a sinistra c'è la colonna macchina sticky). Restituisce cosa mostrare senza finire sotto la colonna.
 */
export function fitLeftChip(right: number, fullW: number, codeW: number, minLeft: number): 'full' | 'code' | 'none' {
  if (right - fullW >= minLeft) return 'full';
  if (right - codeW >= minLeft) return 'code';
  return 'none';
}

/** Area cliccabile di una barra con chip esterno: blocco + chip, senza spazio in mezzo (il chip occupa solo spazio libero della corsia). */
export function barHitRect(left: number, width: number, chipW: number, side: 'right' | 'left' | null): { left: number; width: number } {
  if (!side || chipW <= 0) return { left, width };
  return side === 'right' ? { left, width: width + chipW } : { left: left - chipW, width: width + chipW };
}
