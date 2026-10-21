import { describe, expect, it } from 'vitest';
import { buildAxis, commonClosedRanges, dropRangesContaining, intersectRanges, mergeRanges } from './timeline-axis';
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

  it('does not collapse ranges with a bar edge strictly inside', () => {
    const r = [{ start: h(18), end: h(32) }, { start: h(42), end: h(56) }];
    expect(dropRangesContaining(r, [h(18), h(20)])).toEqual([{ start: h(42), end: h(56) }]);
    expect(dropRangesContaining(r, [h(32), h(42)])).toEqual(r);
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
