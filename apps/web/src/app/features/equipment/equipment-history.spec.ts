import { describe, expect, it } from 'vitest';
import type { TimelineLoadModel } from '@micromes/api-client/models';
import { dailyLoad, formatAvailableHours, formatPercent, formatWorked, parseHistoryDays } from './equipment-history';

const load = (start: string, busy: number, cap: number, eq = 1): TimelineLoadModel => ({
  equipmentId: eq,
  periodStart: start,
  periodEnd: start,
  busyMinutes: busy,
  capacityMinutes: cap,
  loadPercent: cap ? (busy / cap) * 100 : 0,
});

describe('equipment-history', () => {
  it('parseHistoryDays accetta solo 7/30/90', () => {
    expect(parseHistoryDays('7')).toBe(7);
    expect(parseHistoryDays('90')).toBe(90);
    expect(parseHistoryDays('15')).toBe(30);
    expect(parseHistoryDays(null)).toBe(30);
  });
  it('formatWorked', () => {
    expect(formatWorked(0)).toBe('0 min');
    expect(formatWorked(45)).toBe('45 min');
    expect(formatWorked(180)).toBe('3 h');
    expect(formatWorked(150)).toBe('2 h 30 min');
  });
  it('formatAvailableHours e formatPercent', () => {
    expect(formatAvailableHours(7200)).toBe('120 h');
    expect(formatAvailableHours(90)).toBe('1,5 h');
    expect(formatPercent(null)).toBe('n/d');
    expect(formatPercent(12.34)).toBe('12,3%');
  });
  it('dailyLoad copre i giorni, somma i periodi e segnala >= 90%', () => {
    const rows = dailyLoad(
      [load('2026-10-22T06:00:00Z', 200, 240), load('2026-10-22T12:00:00Z', 100, 240), load('2026-10-23T06:00:00Z', 60, 480), load('2026-10-23T06:00:00Z', 999, 1, 2)],
      1,
      '2026-10-22',
      3,
      'Europe/Rome',
    );
    expect(rows.map((r) => r.key)).toEqual(['2026-10-22', '2026-10-23', '2026-10-24']);
    expect(rows[0].percent).toBe(63);
    expect(rows[0].high).toBe(false);
    expect(rows[1].percent).toBe(13);
    expect(rows[2].percent).toBeNull();
    expect(rows[2].high).toBe(false);
  });
  it('dailyLoad evidenzia >= 90%', () => {
    const [d] = dailyLoad([load('2026-10-22T06:00:00Z', 450, 480)], 1, '2026-10-22', 1, 'Europe/Rome');
    expect(d.percent).toBe(94);
    expect(d.high).toBe(true);
  });
});
