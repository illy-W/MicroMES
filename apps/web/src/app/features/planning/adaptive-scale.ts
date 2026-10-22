import type { Range } from './timeline-axis';
import { HOUR } from './timeline-time';

export const DAY_PPH_MIN = 40;
export const DAY_PPH_MAX = 260;

/**
 * Ore d'asse e numero di separatori compressi di un giorno `[day.start, day.end)`.
 * `closed` sono gli intervalli compressi (calendari soli); vuoto se la compressione è spenta: allora il giorno intero (24 h, o 23/25 h col cambio ora).
 */
export function dayAxisHours(day: Range, closed: readonly Range[]): { hours: number; seps: number } {
  let hours = (day.end - day.start) / HOUR;
  let seps = 0;
  for (const r of closed) {
    const s = Math.max(r.start, day.start);
    const e = Math.min(r.end, day.end);
    if (e > s) {
      hours -= (e - s) / HOUR;
      seps++;
    }
  }
  return { hours, seps };
}

/**
 * px/h con cui un giorno intero riempie la larghezza utile (separatori compresi), limitato a 40..260.
 * Senza larghezza o senza ore lavorative restituisce `null` (si mantiene la scala corrente).
 */
export function adaptiveDayPph(usableWidth: number, hours: number, seps: number, sepPx: number): number | null {
  if (!(usableWidth > 0) || !(hours > 0)) return null;
  const px = (usableWidth - seps * sepPx) / hours;
  return Math.round(Math.min(DAY_PPH_MAX, Math.max(DAY_PPH_MIN, px)));
}

/** Il ricalcolo automatico vale solo in zoom 'day' e finché l'utente non ha regolato con −/+. */
export function shouldAutoScale(zoom: string, manual: boolean): boolean {
  return zoom === 'day' && !manual;
}
