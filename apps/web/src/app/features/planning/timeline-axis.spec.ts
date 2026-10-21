import { describe, expect, it } from 'vitest';
import { buildAxis, centreTime, commonClosedRanges, extendAxis, intersectRanges, scrollForCentre, visibleTimeRange, mergeRanges } from './timeline-axis';
import { addDays, dayStart, formatDuration, HOUR } from './timeline-time';

const h = (n: number) => n * HOUR;
const TZ = 'Europe/Rome';

describe('range helpers', () => {
  it('merges overlapping and adjacent ranges, drops empty ones', () => {
    expect(mergeRanges([{ start: 5, end: 8 }, { start: 0, end: 3 }, { start: 3, end: 4 }, { start: 7, end: 9 }, { start: 2, end: 2 }])).toEqual([
      { start: 0, end: 4 },
      { start: 5, end: 9 },
    ]);
  });

  it('intersects range lists', () => {
    expect(intersectRanges([{ start: 0, end: 10 }, { start: 20, end: 30 }], [{ start: 5, end: 25 }])).toEqual([
      { start: 5, end: 10 },
      { start: 20, end: 25 },
    ]);
  });

  it('keeps only ranges closed for every machine (and clipped to the window)', () => {
    const win = { start: 0, end: h(48) };
    const a = [{ start: h(-5), end: h(8) }, { start: h(18), end: h(32) }];
    const b = [{ start: 0, end: h(9) }, { start: h(17), end: h(33) }];
    expect(commonClosedRanges([a, b], win)).toEqual([
      { start: 0, end: h(8) },
      { start: h(18), end: h(32) },
    ]);
    expect(commonClosedRanges([], win)).toEqual([]);
    expect(commonClosedRanges([a, []], win)).toEqual([]);
  });

  it('discards closed ranges shorter than the minimum', () => {
    const win = { start: 0, end: h(24) };
    expect(commonClosedRanges([[{ start: 0, end: h(0.5) }]], win)).toEqual([]);
  });
});

describe('buildAxis', () => {
  const win = { start: 0, end: h(48) };

  it('is linear without collapsed ranges', () => {
    const ax = buildAxis(win, [], 10, 12);
    expect(ax.width).toBe(480);
    expect(ax.toX(h(5))).toBe(50);
    expect(ax.toTime(50)).toBe(h(5));
    expect(ax.isCollapsed(h(5))).toBe(false);
  });

  it('replaces a collapsed range with a separator', () => {
    const ax = buildAxis(win, [{ start: h(18), end: h(32) }], 10, 12);
    expect(ax.width).toBe(180 + 12 + 160);
    expect(ax.toX(h(18))).toBe(180);
    expect(ax.toX(h(32))).toBe(192);
    expect(ax.toX(h(25))).toBe(186);
    expect(ax.toX(h(33))).toBe(202);
    expect(ax.isCollapsed(h(25))).toBe(true);
    expect(ax.isCollapsed(h(18))).toBe(false);
    expect(ax.isCollapsed(h(40))).toBe(false);
  });

  it('round-trips time and pixels, also outside the window', () => {
    const ax = buildAxis(win, [{ start: 0, end: h(8) }, { start: h(18), end: h(32) }], 28, 12);
    for (const t of [0, h(1), h(8), h(12), h(18), h(25), h(32), h(47), h(48), h(-2), h(50)]) {
      expect(ax.toTime(ax.toX(t))).toBeCloseTo(t, 3);
    }
    for (const x of [-20, 0, 5, 12, 100, 300, 500]) {
      expect(ax.toX(ax.toTime(x))).toBeCloseTo(x, 6);
    }
  });

  it('is monotonic', () => {
    const ax = buildAxis(win, [{ start: h(18), end: h(32) }], 10, 12);
    let prev = -Infinity;
    for (let t = -h(1); t <= h(49); t += h(0.25)) {
      const x = ax.toX(t);
      expect(x).toBeGreaterThanOrEqual(prev);
      prev = x;
    }
  });

  it('clips collapsed ranges to the window', () => {
    const ax = buildAxis(win, [{ start: h(-4), end: h(8) }, { start: h(44), end: h(60) }], 10, 12);
    expect(ax.width).toBe(12 + 360 + 12);
  });

  it('handles DST days (23h and 25h) with real elapsed hours', () => {
    const spring = { start: dayStart('2026-03-28', TZ), end: dayStart('2026-03-30', TZ) };
    expect(spring.end - spring.start).toBe(h(47));
    const nights = [0, 1].map((i) => {
      const d = addDays('2026-03-28', i);
      return { start: dayStart(d, TZ) + h(18), end: dayStart(addDays(d, 1), TZ) + h(8) };
    });
    const closed = commonClosedRanges([[...nights, { start: spring.start, end: spring.start + h(8) }]], spring);
    const ax = buildAxis(spring, closed, 28, 12);
    expect(ax.width).toBeCloseTo(((47 - closed.reduce((a, r) => a + (r.end - r.start), 0) / HOUR) * 28) + closed.length * 12, 6);
    const noon = dayStart('2026-03-29', TZ) + h(11);
    expect(ax.toTime(ax.toX(noon))).toBeCloseTo(noon, 3);
    const lin = buildAxis(spring, [], 28, 12);
    expect(lin.width).toBe(47 * 28);

    const autumn = { start: dayStart('2026-10-24', TZ), end: dayStart('2026-10-26', TZ) };
    expect(autumn.end - autumn.start).toBe(h(49));
    const ax2 = buildAxis(autumn, [{ start: autumn.start + h(18), end: autumn.start + h(32) }], 10, 12);
    const t = autumn.start + h(40);
    expect(ax2.toTime(ax2.toX(t))).toBeCloseTo(t, 3);
  });
});

describe('formatDuration', () => {
  it('formats minutes, hours and days', () => {
    expect(formatDuration(h(0.75))).toBe('45 min');
    expect(formatDuration(h(1.5))).toBe('1 h 30 min');
    expect(formatDuration(h(2))).toBe('2 h');
    expect(formatDuration(h(51))).toBe('2 g 3 h');
  });
});

import { clampSpan } from './timeline-axis';
describe('clampSpan / larghezza asse', () => {
  it('scarta ciò che è fuori e ritaglia ai bordi', () => {
    expect(clampSpan(-50, 40, 1000)).toBeNull();
    expect(clampSpan(1000, 40, 1000)).toBeNull();
    expect(clampSpan(-20, 100, 1000)).toEqual({ left: 0, width: 80 });
    expect(clampSpan(950, 100, 1000)).toEqual({ left: 950, width: 50 });
    expect(clampSpan(10, 20, 1000)).toEqual({ left: 10, width: 20 });
  });
});

describe('extendAxis (asse continuo estensibile)', () => {
  // Chiuso ogni giorno 18:00 → 08:00 del giorno dopo (14 h), a partire da 0.
  const day = h(24);
  const closedIn = (w: { start: number; end: number }) => {
    const out = [];
    for (let d = Math.floor(w.start / day) - 1; d <= Math.ceil(w.end / day); d++) out.push({ start: d * day + h(18), end: d * day + h(32) });
    return out.filter((r) => r.end > w.start && r.start < w.end);
  };
  const base = { start: 0, end: 4 * day };
  const build = (w: { start: number; end: number }) => buildAxis(w, commonClosedRanges([closedIn(w)], w), 10, 12);

  it('extending right never moves existing positions and keeps shift 0', () => {
    const a = build(base);
    const win2 = { start: 0, end: 8 * day };
    const { axis, shift } = extendAxis(a, 'right', { start: base.end, end: win2.end }, commonClosedRanges([closedIn(win2)], win2), 10, 12);
    expect(shift).toBe(0);
    for (const t of [0, h(10), h(17), h(40), h(60), h(80)]) expect(axis.toX(t)).toBeCloseTo(a.toX(t), 6);
    expect(axis.width).toBeGreaterThan(a.width);
  });

  it('extending left shifts everything by the added width (stable reference)', () => {
    const a = build(base);
    const win2 = { start: -4 * day, end: base.end };
    const { axis, shift } = extendAxis(a, 'left', { start: win2.start, end: base.start }, commonClosedRanges([closedIn(win2)], win2), 10, 12);
    expect(shift).toBeGreaterThan(0);
    for (const t of [h(10), h(17), h(40), h(60), h(80)]) expect(axis.toX(t) - a.toX(t)).toBeCloseTo(shift, 6);
    // Una chiusura a cavallo del bordo resta un solo separatore (12 px), non due.
    const sepsAtEdge = axis.segments.filter((s) => s.collapsed && s.start < 0 && s.end > 0);
    expect(sepsAtEdge.length).toBe(1);
  });

  it('has cumulative closed ranges: every full day is 10 working hours + one separator', () => {
    const w = { start: h(8), end: h(8) + 6 * day };
    const ax = build(w);
    expect(ax.width).toBeCloseTo(6 * 10 * 10 + 6 * 12, 6);
    expect(ax.segments.filter((s) => s.collapsed).length).toBe(6);
  });

  it('merges a closure split across two chunks into a single separator', () => {
    const w1 = { start: 0, end: day + h(22) };
    const w2 = { start: 0, end: 3 * day };
    const a = build(w1);
    const { axis } = extendAxis(a, 'right', { start: w1.end, end: w2.end }, commonClosedRanges([closedIn(w2)], w2), 10, 12);
    expect(axis.segments.filter((s) => s.collapsed && s.start <= day + h(22) && s.end > day + h(22)).length).toBe(1);
    expect(axis.toX(day + h(8))).toBeCloseTo(a.toX(day + h(8)), 6);
  });
});

describe('ancoraggio dello zoom e intervallo visibile', () => {
  const win = { start: 0, end: 10 * 24 * HOUR };
  const closed = Array.from({ length: 10 }, (_, d) => ({ start: d * 24 * HOUR + h(18), end: d * 24 * HOUR + h(32) })).filter((r) => r.end <= win.end);
  const viewW = 1000;

  it('keeps the centre time stable when the density changes', () => {
    const a = buildAxis(win, closed, 90, 12);
    const left = 3000;
    const c = centreTime(a, left, viewW);
    for (const pph of [28, 10, 90, 40]) {
      const b = buildAxis(win, closed, pph, 12);
      const l2 = scrollForCentre(b, c, viewW);
      if (l2 > 0 && l2 < b.width - viewW) expect(centreTime(b, l2, viewW)).toBeCloseTo(c, 3);
    }
  });

  it('computes the visible time range clipped to the loaded window', () => {
    const a = buildAxis(win, closed, 28, 12);
    const v = visibleTimeRange(a, 100, 500);
    expect(v.start).toBeCloseTo(a.toTime(100), 6);
    expect(v.end).toBeCloseTo(a.toTime(600), 6);
    expect(visibleTimeRange(a, -50, viewW).start).toBe(win.start);
    expect(visibleTimeRange(a, a.width - 10, viewW).end).toBe(win.end);
  });
});
