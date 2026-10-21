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

/** Scarta gli intervalli in cui cade (strettamente dentro) un istante, per esempio l'inizio o la fine di una barra. */
export function dropRangesContaining(ranges: readonly Range[], instants: readonly number[]): Range[] {
  const sorted = [...instants].sort((x, y) => x - y);
  return ranges.filter((r) => !sorted.some((t) => t > r.start && t < r.end));
}

/**
 * Ritaglia gli intervalli attorno agli istanti (inizio/fine barra) che cadono al loro interno: tiene un margine `padMs` per lato
 * e scarta i pezzi rimasti più corti di `minMs`. A differenza di `dropRangesContaining` un solo lavoro notturno
 * (es. un lavoro in corso iniziato di sera) non annulla l'intera compressione della notte/weekend.
 */
export function carveAroundInstants(ranges: readonly Range[], instants: readonly number[], padMs = HOUR / 2, minMs = HOUR): Range[] {
  const sorted = [...instants].sort((x, y) => x - y);
  const out: Range[] = [];
  for (const r of ranges) {
    let pieces: Range[] = [r];
    for (const t of sorted) {
      if (t <= r.start || t >= r.end) continue;
      pieces = pieces.flatMap((p) =>
        t > p.start - padMs && t < p.end + padMs
          ? [{ start: p.start, end: t - padMs }, { start: t + padMs, end: p.end }].filter((q) => q.end > q.start)
          : [p],
      );
    }
    out.push(...pieces.filter((q) => q.end - q.start >= minMs));
  }
  return out;
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
