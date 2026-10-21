import { HOUR } from './timeline-time';

/** Intervallo di tempo in ms epoch, `[start, end)`. */
export interface Range {
  start: number;
  end: number;
}

/** Tratto contiguo dell'asse: o tempo lineare (`collapsed = false`) o un intervallo chiuso compresso in un separatore. */
export interface AxisSegment extends Range {
  x0: number;
  x1: number;
  collapsed: boolean;
}

/** Mappa tempo ↔ pixel lineare a tratti. */
export interface Axis {
  readonly win: Range;
  readonly width: number;
  readonly segments: readonly AxisSegment[];
  /** Pixel (relativi all'inizio della finestra) di un istante. Fuori finestra estrapola in modo lineare. */
  toX(t: number): number;
  /** Istante corrispondente a un pixel (inverso di `toX`). */
  toTime(x: number): number;
  /** `true` se l'istante cade strettamente dentro un intervallo compresso. */
  isCollapsed(t: number): boolean;
}

/** Unisce intervalli sovrapposti o adiacenti e scarta quelli vuoti. */
export function mergeRanges(ranges: readonly Range[]): Range[] {
  const sorted = ranges.filter((r) => r.end > r.start).map((r) => ({ ...r })).sort((a, b) => a.start - b.start);
  const out: Range[] = [];
  for (const r of sorted) {
    const last = out[out.length - 1];
    if (last && r.start <= last.end) last.end = Math.max(last.end, r.end);
    else out.push(r);
  }
  return out;
}

/** Intersezione di due liste di intervalli già unite e ordinate. */
export function intersectRanges(a: readonly Range[], b: readonly Range[]): Range[] {
  const out: Range[] = [];
  let i = 0;
  let j = 0;
  while (i < a.length && j < b.length) {
    const start = Math.max(a[i].start, b[j].start);
    const end = Math.min(a[i].end, b[j].end);
    if (end > start) out.push({ start, end });
    if (a[i].end < b[j].end) i++;
    else j++;
  }
  return out;
}

/**
 * Intervalli in cui TUTTE le macchine sono chiuse, dentro la finestra.
 * `perMachine` contiene, per ogni macchina, i suoi intervalli non lavorativi (calendario + eccezioni).
 * Senza macchine non si comprime nulla. Gli intervalli più corti di `minMs` sono scartati.
 */
export function commonClosedRanges(perMachine: readonly (readonly Range[])[], win: Range, minMs = HOUR): Range[] {
  if (!perMachine.length) return [];
  const clip = (rs: readonly Range[]) => mergeRanges(rs.map((r) => ({ start: Math.max(r.start, win.start), end: Math.min(r.end, win.end) })));
  let common = clip(perMachine[0]);
  for (let k = 1; k < perMachine.length && common.length; k++) common = intersectRanges(common, clip(perMachine[k]));
  return common.filter((r) => r.end - r.start >= minMs);
}

/** Costruisce l'asse: i tratti `collapsed` occupano `sepPx` pixel, il resto `pxPerHour` per ora reale. */
export function buildAxis(win: Range, collapsed: readonly Range[], pxPerHour: number, sepPx: number): Axis {
  const clipped = mergeRanges(collapsed.map((r) => ({ start: Math.max(r.start, win.start), end: Math.min(r.end, win.end) })));
  const segments: AxisSegment[] = [];
  let x = 0;
  let t = win.start;
  const linear = (a: number, b: number, c: boolean) => {
    if (b <= a) return;
    const w = c ? sepPx : ((b - a) / HOUR) * pxPerHour;
    segments.push({ start: a, end: b, x0: x, x1: x + w, collapsed: c });
    x += w;
  };
  for (const r of clipped) {
    linear(t, r.start, false);
    linear(r.start, r.end, true);
    t = r.end;
  }
  linear(t, win.end, false);
  const width = x;

  const findByTime = (time: number): AxisSegment | undefined => {
    let lo = 0;
    let hi = segments.length - 1;
    while (lo <= hi) {
      const mid = (lo + hi) >> 1;
      const s = segments[mid];
      if (time < s.start) hi = mid - 1;
      else if (time >= s.end) lo = mid + 1;
      else return s;
    }
    return undefined;
  };
  const findByX = (px: number): AxisSegment | undefined => {
    let lo = 0;
    let hi = segments.length - 1;
    while (lo <= hi) {
      const mid = (lo + hi) >> 1;
      const s = segments[mid];
      if (px < s.x0) hi = mid - 1;
      else if (px >= s.x1) lo = mid + 1;
      else return s;
    }
    return undefined;
  };

  return {
    win,
    width,
    segments,
    toX(time) {
      if (time <= win.start) return ((time - win.start) / HOUR) * pxPerHour;
      if (time >= win.end) return width + ((time - win.end) / HOUR) * pxPerHour;
      const s = findByTime(time) as AxisSegment;
      return s.x0 + ((time - s.start) / (s.end - s.start)) * (s.x1 - s.x0);
    },
    toTime(px) {
      if (px <= 0) return win.start + (px / pxPerHour) * HOUR;
      if (px >= width) return win.end + ((px - width) / pxPerHour) * HOUR;
      const s = findByX(px) as AxisSegment;
      return s.start + ((px - s.x0) / (s.x1 - s.x0)) * (s.end - s.start);
    },
    isCollapsed(time) {
      const s = findByTime(time);
      return !!s && s.collapsed && time > s.start;
    },
  };
}

/** Ritaglia un intervallo orizzontale `[left, left+width)` a `[0, max]`; `null` se cade interamente fuori (nessun pixel visibile). */
export function clampSpan(left: number, width: number, max: number): { left: number; width: number } | null {
  const l = Math.max(0, left);
  const r = Math.min(max, left + width);
  if (r <= l) return null;
  return { left: l, width: r - l };
}

/**
 * Estende l'asse a sinistra o a destra con la finestra `add` (adiacente a quella esistente).
 * `closedAll` sono gli intervalli compressi calcolati dai SOLI calendari sull'intera finestra risultante.
 * `shift` è lo spostamento in pixel del contenuto già esistente: 0 a destra, la larghezza aggiunta a sinistra
 * (il chiamante lo somma a `scrollLeft` per non muovere ciò che l'utente vede).
 */
export function extendAxis(prev: Axis, side: 'left' | 'right', add: Range, closedAll: readonly Range[], pxPerHour: number, sepPx: number): { axis: Axis; shift: number } {
  const win = side === 'left' ? { start: add.start, end: prev.win.end } : { start: prev.win.start, end: add.end };
  const axis = buildAxis(win, closedAll, pxPerHour, sepPx);
  return { axis, shift: side === 'left' ? axis.toX(prev.win.end) - prev.width : 0 };
}

/** Istante al centro dell'area visibile (`viewW` esclude la colonna macchina sticky). */
export function centreTime(axis: Axis, scrollLeft: number, viewW: number): number {
  return axis.toTime(scrollLeft + viewW / 2);
}

/** `scrollLeft` che porta l'istante `t` al centro dell'area visibile (limitato a `[0, width - viewW]`). */
export function scrollForCentre(axis: Axis, t: number, viewW: number): number {
  return Math.min(Math.max(0, axis.width - viewW), Math.max(0, axis.toX(t) - viewW / 2));
}

/** Intervallo di tempo visibile (ritagliato alla finestra caricata). */
export function visibleTimeRange(axis: Axis, scrollLeft: number, viewW: number): Range {
  const a = Math.max(0, scrollLeft);
  const b = Math.min(axis.width, scrollLeft + viewW);
  return { start: axis.toTime(a), end: axis.toTime(b) };
}
