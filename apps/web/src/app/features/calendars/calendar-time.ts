import type { DayOfWeek, TimeSlotModel, WorkCalendarSlotModel, WorkCalendarSlotRequest } from '@micromes/api-client/models';

export interface TimeRange {
  start: string;
  end: string;
}

/** Giorni della settimana, con il lunedì come primo giorno. */
export const WEEK_DAYS: readonly { key: DayOfWeek; label: string; short: string }[] = [
  { key: 'Monday', label: 'Lunedì', short: 'Lun' },
  { key: 'Tuesday', label: 'Martedì', short: 'Mar' },
  { key: 'Wednesday', label: 'Mercoledì', short: 'Mer' },
  { key: 'Thursday', label: 'Giovedì', short: 'Gio' },
  { key: 'Friday', label: 'Venerdì', short: 'Ven' },
  { key: 'Saturday', label: 'Sabato', short: 'Sab' },
  { key: 'Sunday', label: 'Domenica', short: 'Dom' },
];

export const toHHmm = (t: string): string => t.slice(0, 5);

export function toMinutes(t: string): number {
  const [h, m] = t.split(':').map(Number);
  return h * 60 + m;
}

export function slotsToDays(slots: readonly WorkCalendarSlotModel[]): TimeRange[][] {
  return WEEK_DAYS.map((d) =>
    slots
      .filter((s) => s.dayOfWeek === d.key)
      .map((s) => ({ start: toHHmm(s.startTime), end: toHHmm(s.endTime) }))
      .sort((a, b) => toMinutes(a.start) - toMinutes(b.start)),
  );
}

export function daysToSlots(days: readonly (readonly TimeRange[])[]): WorkCalendarSlotRequest[] {
  return days.flatMap((ranges, i) =>
    ranges.map((r) => ({ dayOfWeek: WEEK_DAYS[i].key, startTime: r.start, endTime: r.end })),
  );
}

export const rangesToRequest = (ranges: readonly TimeRange[]) =>
  ranges.map((r) => ({ startTime: r.start, endTime: r.end }));

export const slotModelsToRanges = (slots: readonly TimeSlotModel[]): TimeRange[] =>
  slots.map((s) => ({ start: toHHmm(s.startTime), end: toHHmm(s.endTime) }));

/** Errore per ciascuna fascia (stesso indice), null se valida. Specchio delle regole del backend. */
export function validateRanges(ranges: readonly TimeRange[]): (string | null)[] {
  const errors: (string | null)[] = ranges.map((r) => {
    if (!r.start || !r.end) return 'Indica inizio e fine';
    if (toMinutes(r.end) <= toMinutes(r.start)) return 'La fine deve essere dopo l’inizio';
    return null;
  });
  const order = ranges.map((_, i) => i).filter((i) => !errors[i]).sort((a, b) => toMinutes(ranges[a].start) - toMinutes(ranges[b].start));
  for (let k = 1; k < order.length; k++) {
    const prev = ranges[order[k - 1]];
    const cur = ranges[order[k]];
    if (toMinutes(cur.start) < toMinutes(prev.end)) errors[order[k]] = 'Si sovrappone a un’altra fascia';
  }
  return errors;
}

export const hasErrors = (errors: readonly (string | null)[]): boolean => errors.some((e) => e !== null);

export function rangesMinutes(ranges: readonly TimeRange[]): number {
  return ranges.reduce((sum, r) => (validateRanges([r])[0] ? sum : sum + toMinutes(r.end) - toMinutes(r.start)), 0);
}

export function weekMinutes(days: readonly (readonly TimeRange[])[]): number {
  return days.reduce((sum, d) => sum + rangesMinutes(d), 0);
}

export function formatHours(minutes: number): string {
  const h = minutes / 60;
  return `${h.toLocaleString('it-IT', { maximumFractionDigits: 2 })} h`;
}

/** Riassunto leggibile di un giorno, es. «08:00–12:00, 13:00–17:00». */
export const summarizeRanges = (ranges: readonly TimeRange[]): string =>
  ranges.length ? ranges.map((r) => `${r.start}–${r.end}`).join(', ') : 'Chiuso';

// ---- Date e fuso aziendale ----

export const pad2 = (n: number): string => String(n).padStart(2, '0');

export const toIsoDate = (d: Date): string => `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;

export function fromIsoDate(s: string): Date {
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, m - 1, d);
}

export const formatDate = (iso: string): string => fromIsoDate(iso).toLocaleDateString('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' });

function tzOffsetMs(utcMs: number, tz: string): number {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: tz,
    hourCycle: 'h23',
    year: 'numeric',
    month: 'numeric',
    day: 'numeric',
    hour: 'numeric',
    minute: 'numeric',
    second: 'numeric',
  }).formatToParts(new Date(utcMs));
  const v = (t: string) => Number(parts.find((p) => p.type === t)?.value);
  return Date.UTC(v('year'), v('month') - 1, v('day'), v('hour'), v('minute'), v('second')) - Math.floor(utcMs / 1000) * 1000;
}

/** Converte data (yyyy-MM-dd) e ora (HH:mm) del fuso aziendale in istante UTC ISO. */
export function zonedToUtcIso(date: string, time: string, tz: string): string {
  const [y, mo, d] = date.split('-').map(Number);
  const [h, mi] = time.split(':').map(Number);
  const guess = Date.UTC(y, mo - 1, d, h, mi);
  let utc = guess - tzOffsetMs(guess, tz);
  utc = guess - tzOffsetMs(utc, tz);
  return new Date(utc).toISOString();
}

const WEEKDAY_FMT = (tz: string) => new Intl.DateTimeFormat('it-IT', { timeZone: tz, weekday: 'long', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });

/** «lunedì 08:00» nel fuso indicato. */
export function formatWeekdayTime(iso: string, tz: string): string {
  return WEEKDAY_FMT(tz).format(new Date(iso)).replace(/^(\S+)\s+/, '$1 ');
}

export const formatDateTime = (iso: string, tz: string): string =>
  new Intl.DateTimeFormat('it-IT', { timeZone: tz, day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(new Date(iso));
