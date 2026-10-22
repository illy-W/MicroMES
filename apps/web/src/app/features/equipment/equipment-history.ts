import type { TimelineLoadModel } from '@micromes/api-client/models';
import { addDays, dayKey, dayStart } from '../planning/timeline-time';
import { LOAD_HIGH } from './equipment-overview';

export const HISTORY_PERIODS = [7, 30, 90] as const;
export type HistoryDays = (typeof HISTORY_PERIODS)[number];
export const HISTORY_PAGE_SIZE = 20;

/** Valida il parametro `?days=`: valori ammessi 7, 30, 90 (default 30). */
export function parseHistoryDays(v: string | null | undefined): HistoryDays {
  const n = Number(v);
  return (HISTORY_PERIODS as readonly number[]).includes(n) ? (n as HistoryDays) : 30;
}

/** Minuti come `2 h 30 min`, `45 min`, `3 h`. */
export function formatWorked(minutes: number): string {
  const m = Math.max(0, Math.round(minutes));
  const h = Math.floor(m / 60);
  const r = m % 60;
  if (!h) return `${r} min`;
  return r ? `${h} h ${r} min` : `${h} h`;
}

/** Ore disponibili come `120 h` (arrotondate a 1 decimale se serve). */
export function formatAvailableHours(minutes: number): string {
  const h = Math.round((minutes / 60) * 10) / 10;
  return `${String(h).replace('.', ',')} h`;
}

export function formatPercent(v: number | null | undefined): string {
  return v == null ? 'n/d' : `${String(Math.round(v * 10) / 10).replace('.', ',')}%`;
}

export interface DayLoad {
  key: string;
  /** `null` se il giorno non ha capacità (chiuso). */
  percent: number | null;
  busyMinutes: number;
  capacityMinutes: number;
  high: boolean;
}

/** Carico per ciascuno dei `days` giorni (fuso aziendale) a partire da `fromKey`, dalle righe `load` della timeline. */
export function dailyLoad(load: readonly TimelineLoadModel[], equipmentId: number, fromKey: string, days: number, tz: string): DayLoad[] {
  const acc = new Map<string, { busy: number; cap: number }>();
  for (const l of load) {
    if (l.equipmentId !== equipmentId) continue;
    const k = dayKey(new Date(l.periodStart).getTime(), tz);
    const cur = acc.get(k) ?? { busy: 0, cap: 0 };
    cur.busy += l.busyMinutes;
    cur.cap += l.capacityMinutes;
    acc.set(k, cur);
  }
  return Array.from({ length: days }, (_, i) => {
    const key = addDays(fromKey, i);
    const v = acc.get(key);
    const percent = v && v.cap > 0 ? Math.round((v.busy / v.cap) * 100) : null;
    return { key, percent, busyMinutes: v?.busy ?? 0, capacityMinutes: v?.cap ?? 0, high: percent != null && percent >= LOAD_HIGH };
  });
}

/** Istante di inizio del giorno `key` nel fuso (per le richieste). */
export const dayStartIso = (key: string, tz: string): string => new Date(dayStart(key, tz)).toISOString();
