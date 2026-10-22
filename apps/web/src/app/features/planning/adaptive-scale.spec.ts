import { describe, expect, it } from 'vitest';
import { adaptiveDayPph, dayAxisHours, shouldAutoScale } from './adaptive-scale';
import { HOUR } from './timeline-time';

const d0 = Date.UTC(2026, 9, 22);
const day = { start: d0, end: d0 + 24 * HOUR };
const closed = (a: number, b: number) => ({ start: d0 + a * HOUR, end: d0 + b * HOUR });

describe('dayAxisHours', () => {
  it('uses 24h when nothing is compressed', () => expect(dayAxisHours(day, [])).toEqual({ hours: 24, seps: 0 }));
  it('8h calendar: 16h closed in two ranges', () => expect(dayAxisHours(day, [closed(0, 6), closed(14, 24)])).toEqual({ hours: 8, seps: 2 }));
  it('16h calendar', () => expect(dayAxisHours(day, [closed(0, 4), closed(20, 24)])).toEqual({ hours: 16, seps: 2 }));
  it('clips ranges crossing the day boundary', () => expect(dayAxisHours(day, [{ start: d0 - 10 * HOUR, end: d0 + 6 * HOUR }])).toEqual({ hours: 18, seps: 1 }));
});

describe('adaptiveDayPph', () => {
  it('fills the width: 8h vs 16h', () => {
    expect(adaptiveDayPph(1000, 8, 0, 12)).toBe(125);
    expect(adaptiveDayPph(1000, 16, 0, 12)).toBe(63);
  });
  it('subtracts separators', () => expect(adaptiveDayPph(1024, 8, 2, 12)).toBe(125));
  it('clamps to 40..260', () => {
    expect(adaptiveDayPph(300, 24, 0, 12)).toBe(40);
    expect(adaptiveDayPph(1900, 4, 0, 12)).toBe(260);
  });
  it('returns null without width or hours', () => {
    expect(adaptiveDayPph(0, 8, 0, 12)).toBeNull();
    expect(adaptiveDayPph(1000, 0, 0, 12)).toBeNull();
  });
});

describe('shouldAutoScale', () => {
  it('only in day mode without manual override', () => {
    expect(shouldAutoScale('day', false)).toBe(true);
    expect(shouldAutoScale('day', true)).toBe(false);
    expect(shouldAutoScale('week', false)).toBe(false);
  });
});
