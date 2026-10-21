import type { Range } from './timeline-axis';
import { HOUR } from './timeline-time';

const DAY = 24 * HOUR;
/** Il limite dell'API è 62 giorni per richiesta: ogni richiesta ne copre al massimo 28. */
export const CHUNK_MS = 28 * DAY;
/** Finestra iniziale attorno all'ancora. */
export const BEFORE_MS = 7 * DAY;
export const AFTER_MS = 21 * DAY;
/** Massimo allontanamento dall'ancora iniziale (per lato) prima di non estendere più. */
export const CAP_MS = 180 * DAY;
/** Soglia di prefetch, in larghezze di viewport. */
export const PREFETCH_VIEWS = 1.5;

export interface Side { left: boolean; right: boolean }
export interface PlannedChunk { side: 'left' | 'right'; range: Range }

export function initialRange(anchor: number): Range {
  return { start: anchor - BEFORE_MS, end: anchor + AFTER_MS };
}
export function capRange(anchor: number): Range {
  return { start: anchor - CAP_MS, end: anchor + CAP_MS };
}

export interface ExtensionInput {
  scrollLeft: number;
  /** Larghezza dell'area visibile (senza colonna macchina). */
  viewW: number;
  axisWidth: number;
  loaded: Range;
  cap: Range;
  /** Lati con una richiesta in corso o in errore (da non ripetere). */
  busy: Side;
  chunkMs?: number;
}

/** Quali intervalli caricare quando il viewport si avvicina a un bordo dell'asse (al più uno per lato, mai oltre `chunkMs`). */
export function planExtension(i: ExtensionInput): PlannedChunk[] {
  const chunk = Math.min(i.chunkMs ?? CHUNK_MS, CHUNK_MS);
  const th = i.viewW * PREFETCH_VIEWS;
  const out: PlannedChunk[] = [];
  if (!i.busy.left && i.scrollLeft < th && i.loaded.start > i.cap.start) {
    out.push({ side: 'left', range: { start: Math.max(i.cap.start, i.loaded.start - chunk), end: i.loaded.start } });
  }
  if (!i.busy.right && i.axisWidth - (i.scrollLeft + i.viewW) < th && i.loaded.end < i.cap.end) {
    out.push({ side: 'right', range: { start: i.loaded.end, end: Math.min(i.cap.end, i.loaded.end + chunk) } });
  }
  return out;
}

/**
 * Richieste (in ordine, dalla più vicina) per includere nell'intervallo caricato l'istante `t` con `margin` di sicurezza.
 * `null` se `t` è oltre il tetto: il chiamante riparte da una nuova ancora.
 */
export function planCover(loaded: Range, t: number, cap: Range, margin = DAY, chunkMs = CHUNK_MS): PlannedChunk[] | null {
  const chunk = Math.min(chunkMs, CHUNK_MS);
  const out: PlannedChunk[] = [];
  if (t - margin < loaded.start) {
    if (t - margin < cap.start) return null;
    for (let end = loaded.start; end > t - margin; end -= chunk) out.push({ side: 'left', range: { start: Math.max(cap.start, end - chunk), end } });
  } else if (t + margin > loaded.end) {
    if (t + margin > cap.end) return null;
    for (let start = loaded.end; start < t + margin; start += chunk) out.push({ side: 'right', range: { start, end: Math.min(cap.end, start + chunk) } });
  }
  return out;
}

/** Unisce gli intervalli adiacenti già caricati (chunk ordinati e contigui). */
export function loadedUnion(chunks: readonly Range[]): Range | null {
  if (!chunks.length) return null;
  return { start: Math.min(...chunks.map((c) => c.start)), end: Math.max(...chunks.map((c) => c.end)) };
}

export interface LaneItem { id: number; row: number; s: number; e: number; lane: number }

/**
 * Corsie coerenti dopo l'unione di più richieste: ogni lavoro tiene la corsia calcolata dal server se è libera al suo inizio,
 * altrimenti prende la prima libera. Con dati coerenti il risultato coincide con quello del server.
 */
export function resolveLanes(items: readonly LaneItem[]): Map<number, number> {
  const out = new Map<number, number>();
  const rows = new Map<number, LaneItem[]>();
  for (const it of items) rows.set(it.row, [...(rows.get(it.row) ?? []), it]);
  for (const list of rows.values()) {
    const ends: number[] = [];
    for (const it of [...list].sort((a, b) => a.s - b.s || a.e - b.e || a.id - b.id)) {
      let lane = it.lane;
      if (lane < 0 || (ends[lane] ?? -Infinity) > it.s) {
        lane = 0;
        while ((ends[lane] ?? -Infinity) > it.s) lane++;
      }
      ends[lane] = it.e;
      out.set(it.id, lane);
    }
  }
  return out;
}

/** Istante da usare come `at` nell'URL (minuti, UTC) e suo inverso (accetta anche la data semplice `yyyy-mm-dd` del vecchio `from`). */
export function formatAt(ms: number): string {
  return new Date(ms).toISOString().slice(0, 16) + 'Z';
}
export function parseAt(v: string | null, dayStartOf: (key: string) => number): number | null {
  if (!v) return null;
  if (/^\d{4}-\d{2}-\d{2}$/.test(v)) return dayStartOf(v) + 12 * HOUR;
  const t = Date.parse(v);
  return Number.isFinite(t) ? t : null;
}
