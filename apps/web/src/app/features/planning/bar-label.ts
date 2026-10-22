/** Dove sta il testo di una barra: due righe, una riga troncata, solo codice fase, niente (solo tooltip). */
export type LabelPlacement = 'inside' | 'truncated' | 'code' | 'none';

/** Larghezza visiva minima di una barra (px) quando la corsia ha spazio. */
export const BAR_MIN_VISUAL = 72;
/** Sotto questa larghezza il testo si riduce a una riga troncata, poi al codice fase, poi a niente. */
export const TRUNCATED_MIN = 40;
export const CODE_MIN = 20;
/** Bordi tratteggiati/punteggiati solo da questa larghezza. */
export const DASH_MIN = 96;
export const BAR_GAP = 2;

export interface BarLayoutInput {
  /** Inizio e larghezza REALE (durata vera) in px d'asse. */
  left: number;
  realW: number;
  /** Inizio della barra seguente nella stessa corsia (Infinity se nessuna) e limite destro dell'asse. */
  nextStart: number;
  maxRight: number;
  minVisual?: number;
  gap?: number;
}

export interface BarLayout {
  /** Larghezza visiva: mai meno della reale, mai oltre lo spazio libero fino alla barra seguente. */
  visualWidth: number;
  placement: LabelPlacement;
}

/** Larghezza visiva di una barra: almeno `minVisual`, ma ridotta allo spazio disponibile (mai sopra la barra seguente). */
export function visualWidth(i: BarLayoutInput): number {
  const room = Math.max(0, Math.min(i.nextStart, i.maxRight) - (i.gap ?? BAR_GAP) - i.left);
  return Math.max(i.realW, Math.min(i.minVisual ?? BAR_MIN_VISUAL, room));
}

export const placementFor = (width: number): LabelPlacement =>
  width >= BAR_MIN_VISUAL ? 'inside' : width >= TRUNCATED_MIN ? 'truncated' : width >= CODE_MIN ? 'code' : 'none';

export const allowsDashedBorder = (width: number): boolean => width >= DASH_MIN;

export function layoutBar(i: BarLayoutInput): BarLayout {
  const w = visualWidth(i);
  return { visualWidth: w, placement: placementFor(w) };
}
