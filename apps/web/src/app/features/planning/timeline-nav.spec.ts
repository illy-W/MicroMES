import { describe, expect, it } from 'vitest';
import { CHUNK_MS, capRange, formatAt, initialRange, parseAt, planCover, planExtension, resolveLanes } from './timeline-nav';
import { daysBetween, formatRangeTitle, HOUR } from './timeline-time';

const DAY = 24 * HOUR;
const anchor = Date.UTC(2026, 9, 22, 12);
const loaded = initialRange(anchor);
const cap = capRange(anchor);

describe('initial range', () => {
  it('is [anchor - 7d, anchor + 21d], a single request within the API limit', () => {
    expect(loaded.start).toBe(anchor - 7 * DAY);
    expect(loaded.end).toBe(anchor + 21 * DAY);
    expect(loaded.end - loaded.start).toBeLessThanOrEqual(CHUNK_MS);
  });
});

describe('planExtension', () => {
  const base = { viewW: 1000, axisWidth: 20000, loaded, cap, busy: { left: false, right: false } };
  it('does nothing in the middle', () => {
    expect(planExtension({ ...base, scrollLeft: 9000 })).toEqual([]);
  });
  it('extends left within 1.5 viewports of the left edge, by at most 28 days', () => {
    const p = planExtension({ ...base, scrollLeft: 1400 });
    expect(p).toEqual([{ side: 'left', range: { start: loaded.start - CHUNK_MS, end: loaded.start } }]);
    expect(p[0].range.end - p[0].range.start).toBeLessThanOrEqual(CHUNK_MS);
  });
  it('extends right near the right edge', () => {
    const p = planExtension({ ...base, scrollLeft: 20000 - 1000 - 1400 });
    expect(p).toEqual([{ side: 'right', range: { start: loaded.end, end: loaded.end + CHUNK_MS } }]);
  });
  it('does not repeat a side already busy or failed (coalescing), nor go past the cap', () => {
    expect(planExtension({ ...base, scrollLeft: 0, busy: { left: true, right: false } })).toEqual([]);
    const atCap = { start: cap.start, end: loaded.end };
    expect(planExtension({ ...base, loaded: atCap, scrollLeft: 0 })).toEqual([]);
    const near = { start: cap.start + 10 * DAY, end: loaded.end };
    expect(planExtension({ ...base, loaded: near, scrollLeft: 0 })[0].range.start).toBe(cap.start);
  });
});

describe('planCover', () => {
  it('returns nothing when the target is already covered', () => {
    expect(planCover(loaded, anchor, cap)).toEqual([]);
  });
  it('splits a far target into <=28 day contiguous chunks, nearest first', () => {
    const p = planCover(loaded, loaded.end + 50 * DAY, cap) ?? [];
    expect(p.length).toBe(2);
    expect(p.every((c) => c.side === 'right' && c.range.end - c.range.start <= CHUNK_MS)).toBe(true);
    expect(p[0].range.start).toBe(loaded.end);
    expect(p[1].range.start).toBe(p[0].range.end);
    expect(p[1].range.end).toBeGreaterThan(loaded.end + 50 * DAY);
    const l = planCover(loaded, loaded.start - 30 * DAY, cap) ?? [];
    expect(l.map((c) => c.side)).toEqual(['left', 'left']);
    expect(l[0].range.end).toBe(loaded.start);
  });
  it('returns null beyond the cap (restart from a new anchor)', () => {
    expect(planCover(loaded, cap.end + DAY, cap)).toBeNull();
  });
});

describe('resolveLanes', () => {
  it('keeps consistent server lanes and fixes collisions between chunks', () => {
    const ok = resolveLanes([
      { id: 1, row: 1, s: 0, e: 10, lane: 0 },
      { id: 2, row: 1, s: 5, e: 15, lane: 1 },
      { id: 3, row: 1, s: 10, e: 20, lane: 0 },
    ]);
    expect([ok.get(1), ok.get(2), ok.get(3)]).toEqual([0, 1, 0]);
    const clash = resolveLanes([
      { id: 1, row: 1, s: 0, e: 10, lane: 0 },
      { id: 2, row: 1, s: 5, e: 15, lane: 0 },
    ]);
    expect(clash.get(2)).toBe(1);
  });
});

describe('at / titolo', () => {
  it('round-trips the URL instant and reads the legacy date', () => {
    expect(parseAt(formatAt(anchor), () => 0)).toBe(Math.floor(anchor / 60000) * 60000);
    expect(parseAt('2026-10-22', () => 1000)).toBe(1000 + 12 * HOUR);
    expect(parseAt('boh', () => 0)).toBeNull();
    expect(parseAt(null, () => 0)).toBeNull();
  });
  it('formats the visible range title', () => {
    const tz = 'Europe/Rome';
    const d = (day: number, month = 10, h = 8) => Date.UTC(2026, month - 1, day, h - 2);
    expect(formatRangeTitle(d(22), d(23, 10, 20), tz)).toBe('gio 22 – ven 23 ott 2026');
    expect(formatRangeTitle(d(22), d(22, 10, 18), tz)).toBe('gio 22 ott 2026');
    expect(formatRangeTitle(d(28), d(6, 11), tz)).toBe('mer 28 ott – ven 6 nov 2026');
  });
  it('lists the days touching a range', () => {
    const tz = 'Europe/Rome';
    const days = daysBetween(Date.UTC(2026, 9, 22, 10), Date.UTC(2026, 9, 24, 10), tz);
    expect(days.map((x) => x.key)).toEqual(['2026-10-22', '2026-10-23', '2026-10-24']);
  });
});
