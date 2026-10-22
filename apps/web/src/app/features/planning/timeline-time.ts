/** Utility di tempo nel fuso orario aziendale (mai l'ora locale del browser). */
export const HOUR = 3_600_000;

const partsCache = new Map<string, Intl.DateTimeFormat>();

function fmt(tz: string, opts: Intl.DateTimeFormatOptions, key: string): Intl.DateTimeFormat {
  const k = `${tz}|${key}`;
  let f = partsCache.get(k);
  if (!f) {
    f = new Intl.DateTimeFormat('it-IT', { timeZone: tz, ...opts });
    partsCache.set(k, f);
  }
  return f;
}

function hms(ms: number, tz: string): { h: number; m: number; s: number } {
  const p = fmt(tz, { hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23' }, 'hms').formatToParts(ms);
  const g = (t: string) => Number(p.find((x) => x.type === t)?.value ?? 0);
  return { h: g('hour'), m: g('minute'), s: g('second') };
}

/** Chiave giorno `yyyy-mm-dd` nel fuso. */
export function dayKey(ms: number, tz: string): string {
  const p = fmt(tz, { year: 'numeric', month: '2-digit', day: '2-digit' }, 'ymd').formatToParts(ms);
  const g = (t: string) => p.find((x) => x.type === t)?.value ?? '';
  return `${g('year')}-${g('month')}-${g('day')}`;
}

export function startOfDay(ms: number, tz: string): number {
  const { h, m, s } = hms(ms, tz);
  return ms - (((h * 60 + m) * 60 + s) * 1000 + (((ms % 1000) + 1000) % 1000));
}

/** Istante di mezzanotte (fuso aziendale) del giorno `key`. */
export function dayStart(key: string, tz: string): number {
  const [y, m, d] = key.split('-').map(Number);
  return startOfDay(Date.UTC(y, m - 1, d, 12), tz);
}

export function addDays(key: string, n: number): string {
  const [y, m, d] = key.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d + n)).toISOString().slice(0, 10);
}

/** Lunedì della settimana che contiene `key`. */
export function mondayOf(key: string): string {
  const [y, m, d] = key.split('-').map(Number);
  const dow = (new Date(Date.UTC(y, m - 1, d)).getUTCDay() + 6) % 7;
  return addDays(key, -dow);
}

export function monthStart(key: string): string {
  return `${key.slice(0, 8)}01`;
}

export function addMonths(key: string, n: number): string {
  const [y, m] = key.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1 + n, 1)).toISOString().slice(0, 10);
}

export function daysInMonth(key: string): number {
  const [y, m] = key.split('-').map(Number);
  return new Date(Date.UTC(y, m, 0)).getUTCDate();
}

/** `22/10 08:00` nel fuso aziendale. */
export function formatDateTime(iso: string | number | null | undefined, tz: string): string {
  if (iso == null) return '—';
  const f = fmt(tz, { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }, 'dt');
  return f.format(typeof iso === 'number' ? iso : new Date(iso)).replace(',', '');
}

export function formatDay(ms: number, tz: string, withYear = false): string {
  const f = fmt(tz, withYear ? { day: 'numeric', month: 'short', year: 'numeric' } : { day: 'numeric', month: 'short' }, withYear ? 'dy' : 'd');
  return f.format(ms);
}

export function formatWeekday(ms: number, tz: string): string {
  return fmt(tz, { weekday: 'short', day: 'numeric' }, 'wd').format(ms);
}

export function formatMonthTitle(ms: number, tz: string): string {
  return fmt(tz, { month: 'long', year: 'numeric' }, 'my').format(ms);
}

export function hourLabel(ms: number, tz: string): string {
  return String(hms(ms, tz).h).padStart(2, '0');
}

/** Durata leggibile: `45 min`, `1 h 30 min`, `2 g 3 h`. */
export function formatDuration(ms: number): string {
  const min = Math.max(0, Math.round(ms / 60_000));
  if (min < 60) return `${min} min`;
  const h = Math.floor(min / 60);
  const m = min % 60;
  if (h < 24) return m ? `${h} h ${m} min` : `${h} h`;
  const d = Math.floor(h / 24);
  const rh = h % 24;
  return rh ? `${d} g ${rh} h` : `${d} g`;
}

/** Giorni (chiave, inizio, fine) che toccano `[from, to)`, nel fuso aziendale. */
export function daysBetween(from: number, to: number, tz: string): { key: string; start: number; end: number }[] {
  const out: { key: string; start: number; end: number }[] = [];
  let key = dayKey(from, tz);
  let start = dayStart(key, tz);
  for (let i = 0; i < 800 && start < to; i++) {
    const next = addDays(key, 1);
    const end = dayStart(next, tz);
    out.push({ key, start, end });
    key = next;
    start = end;
  }
  return out;
}

/** Titolo dell'intervallo visibile: `gio 22 ott 2026`, `gio 22 – ven 23 ott 2026`, `mer 28 ott – ven 6 nov 2026`. */
export function formatRangeTitle(a: number, b: number, tz: string): string {
  const monYear = (ms: number) => formatDay(ms, tz, true).replace(/^\d+\s/, '');
  const mon = (ms: number) => formatDay(ms, tz).replace(/^\d+\s/, '');
  const last = Math.max(a, b - 1);
  if (dayKey(a, tz) === dayKey(last, tz)) return `${formatWeekday(a, tz)} ${monYear(a)}`;
  if (monYear(a) === monYear(last)) return `${formatWeekday(a, tz)} – ${formatWeekday(last, tz)} ${monYear(last)}`;
  if (monYear(a).slice(-4) === monYear(last).slice(-4)) return `${formatWeekday(a, tz)} ${mon(a)} – ${formatWeekday(last, tz)} ${monYear(last)}`;
  return `${formatWeekday(a, tz)} ${monYear(a)} – ${formatWeekday(last, tz)} ${monYear(last)}`;
}

/** Ora `HH:mm` nel fuso. */
export function timeKey(ms: number, tz: string): string {
  const { h, m } = hms(ms, tz);
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
}
