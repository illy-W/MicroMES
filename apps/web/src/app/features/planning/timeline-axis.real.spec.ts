import { describe, expect, it } from 'vitest';
import { buildAxis, carveAroundInstants, clampSpan, commonClosedRanges } from './timeline-axis';
import { HOUR } from './timeline-time';


/** Frammento ridotto di GET /api/planning/timeline (settimana 19-26/10/2026, Europe/Rome): STANDARD 08-12/13-17 e DOPPIO-TURNO 06-22. */
const NW = {"1":[["2026-10-19T00:00:00Z","2026-10-19T06:00:00Z"],["2026-10-19T10:00:00Z","2026-10-19T11:00:00Z"],["2026-10-19T15:00:00Z","2026-10-20T06:00:00Z"],["2026-10-20T10:00:00Z","2026-10-20T11:00:00Z"],["2026-10-20T15:00:00Z","2026-10-21T06:00:00Z"],["2026-10-21T10:00:00Z","2026-10-21T11:00:00Z"],["2026-10-21T15:00:00Z","2026-10-22T06:00:00Z"],["2026-10-22T10:00:00Z","2026-10-22T11:00:00Z"],["2026-10-22T15:00:00Z","2026-10-23T06:00:00Z"],["2026-10-23T10:00:00Z","2026-10-23T11:00:00Z"],["2026-10-23T15:00:00Z","2026-10-26T00:00:00Z"]],"6":[["2026-10-19T00:00:00Z","2026-10-19T04:00:00Z"],["2026-10-19T20:00:00Z","2026-10-20T04:00:00Z"],["2026-10-20T20:00:00Z","2026-10-21T04:00:00Z"],["2026-10-21T20:00:00Z","2026-10-22T04:00:00Z"],["2026-10-22T20:00:00Z","2026-10-23T04:00:00Z"],["2026-10-23T20:00:00Z","2026-10-26T00:00:00Z"]],"None":[["2026-10-19T00:00:00Z","2026-10-19T06:00:00Z"],["2026-10-19T10:00:00Z","2026-10-19T11:00:00Z"],["2026-10-19T15:00:00Z","2026-10-20T06:00:00Z"],["2026-10-20T10:00:00Z","2026-10-20T11:00:00Z"],["2026-10-20T15:00:00Z","2026-10-21T06:00:00Z"],["2026-10-21T10:00:00Z","2026-10-21T11:00:00Z"],["2026-10-21T15:00:00Z","2026-10-22T06:00:00Z"],["2026-10-22T10:00:00Z","2026-10-22T11:00:00Z"],["2026-10-22T15:00:00Z","2026-10-23T06:00:00Z"],["2026-10-23T10:00:00Z","2026-10-23T11:00:00Z"],["2026-10-23T15:00:00Z","2026-10-26T00:00:00Z"]]} as Record<string, [string, string][]>;
const JOBS = [["Ended",null,null,"2026-10-20T09:00:00Z","2026-10-20T09:10:00Z"],["Ended",null,null,"2026-10-20T14:00:00Z","2026-10-20T14:44:00Z"],["Ended",null,null,"2026-10-20T15:00:00Z","2026-10-20T15:30:00Z"],["Running","2026-10-21T16:46:28.287305Z","2026-10-21T17:46:28.287305Z","2026-10-21T16:46:28.287305Z",null],["Running",null,null,"2026-10-21T17:41:28.287305Z",null],["Running",null,null,"2026-10-21T17:56:28.287305Z",null],["Running",null,null,"2026-10-21T17:56:28.287305Z",null],["Ready","2026-10-22T06:00:00Z","2026-10-22T09:05:00Z",null,null],["Interrupted","2026-10-22T06:00:00Z","2026-10-22T06:39:00Z",null,null],["Ready","2026-10-22T06:39:00Z","2026-10-22T07:19:00Z",null,null],["Waiting","2026-10-22T06:39:00Z","2026-10-22T07:53:00Z",null,null],["Waiting","2026-10-22T07:53:00Z","2026-10-22T08:48:00Z",null,null],["Waiting","2026-10-22T08:14:00Z","2026-10-22T12:34:00Z",null,null],["Waiting","2026-10-22T08:14:00Z","2026-10-22T10:06:00Z",null,null],["Waiting","2026-10-22T09:05:00Z","2026-10-22T12:55:00Z",null,null],["Waiting","2026-10-22T11:00:00Z","2026-10-22T12:34:00Z",null,null],["Waiting","2026-10-22T12:34:00Z","2026-10-22T13:15:00Z",null,null],["Waiting","2026-10-22T12:34:00Z","2026-10-22T14:25:00Z",null,null],["Waiting","2026-10-22T12:34:00Z","2026-10-22T12:51:00Z",null,null],["Waiting","2026-10-22T12:51:00Z","2026-10-22T13:14:00Z",null,null],["Waiting","2026-10-22T12:55:00Z","2026-10-22T14:50:00Z",null,null],["Waiting","2026-10-22T13:15:00Z","2026-10-22T14:57:00Z",null,null],["Waiting","2026-10-22T14:25:00Z","2026-10-22T14:54:00Z",null,null],["Waiting","2026-10-22T14:50:00Z","2026-10-23T09:20:00Z",null,null],["Waiting","2026-10-22T14:57:00Z","2026-10-22T16:25:00Z",null,null],["Waiting","2026-10-23T09:20:00Z","2026-10-23T11:05:00Z",null,null],["Waiting","2026-10-23T09:20:00Z","2026-10-23T12:42:00Z",null,null],["Waiting","2026-10-23T11:05:00Z","2026-10-23T12:10:00Z",null,null],["Waiting","2026-10-23T12:42:00Z","2026-10-23T13:11:00Z",null,null],["Waiting","2026-10-23T12:42:00Z","2026-10-23T14:12:00Z",null,null],["Waiting","2026-10-23T13:11:00Z","2026-10-23T13:52:00Z",null,null],["Waiting","2026-10-23T14:12:00Z","2026-10-23T14:33:00Z",null,null],["Waiting","2026-10-23T14:33:00Z","2026-10-26T07:02:00Z",null,null]] as [string, string | null, string | null, string | null, string | null][];

describe('real week fixture', () => {
  const t = (s: string) => new Date(s).getTime();
  const win = { start: t('2026-10-18T22:00:00Z'), end: t('2026-10-25T22:00:00Z') };
  const mk = (k: string) => NW[k].map(([s, e]) => ({ start: t(s), end: t(e) }));
  const edges = JOBS.flatMap(([status, ps, pe, as, ae]) => {
    const running = status === 'Running' || status === 'Ended';
    const s0 = (running && as) || ps;
    if (!s0) return [];
    const s = t(s0);
    const e0 = (status === 'Ended' && ae) || pe || ae;
    return [s, e0 ? t(e0) : s + 30 * 60_000];
  });

  it('collapses nights and weekend', () => {
    // 1 macchina standard (+ calendario azienda) e la doppio turno
    const ranges = carveAroundInstants(commonClosedRanges([mk('1'), mk('6')], win), edges);
    const axis = buildAxis(win, ranges, 28, 12);
    const open = axis.segments.filter((s) => !s.collapsed).reduce((a, s) => a + (s.end - s.start), 0);
    expect(open / HOUR).toBeLessThanOrEqual(5 * 16 + 4);
    expect(axis.segments.filter((s) => s.collapsed).length).toBeGreaterThanOrEqual(5);
  });

  it('a job running since the evening does not cancel the compression of the night nor the weekend', () => {
    const lateEdges = [...edges, t('2026-10-21T21:30:00Z'), t('2026-10-21T22:00:00Z')];
    const ranges = carveAroundInstants(commonClosedRanges([mk('1'), mk('6')], win), lateEdges);
    const axis = buildAxis(win, ranges, 28, 12);
    const seps = axis.segments.filter((s) => s.collapsed);
    expect(seps.length).toBeGreaterThanOrEqual(6);
    // Il weekend (ven 22:00 → lun 00:00 locali) resta un solo separatore.
    expect(seps.some((s) => s.end - s.start >= 48 * HOUR)).toBe(true);
    // Giorno feriale: ben più corto di 24 h lineari.
    expect(axis.toX(t('2026-10-22T22:00:00Z')) - axis.toX(t('2026-10-21T22:00:00Z'))).toBeLessThan(24 * 28 - 6 * 28);
  });
});

describe('real day window 2026-10-22: asse == estensione massima', () => {
  const t = (s: string) => new Date(s).getTime();
  const win = { start: t('2026-10-21T22:00:00Z'), end: t('2026-10-22T22:00:00Z') };
  const mk = (k: string) => NW[k].map(([s, e]) => ({ start: t(s), end: t(e) }));
  const axis = buildAxis(win, carveAroundInstants(commonClosedRanges([mk('1'), mk('6')], win), []), 56, 12);
  it('nessuna barra ritagliata supera la larghezza dell\'asse', () => {
    let max = 0;
    for (const [, ps, pe] of JOBS) {
      if (!ps || !pe) continue;
      const l = axis.toX(t(ps));
      const c = clampSpan(l, axis.toX(t(pe)) - l, axis.width);
      if (c) max = Math.max(max, c.left + c.width);
    }
    expect(axis.width).toBeGreaterThan(0);
    expect(max).toBeLessThanOrEqual(axis.width);
    // Il lavoro del 23/10 (margine di prefetch) è scartato.
    const l = axis.toX(t('2026-10-23T09:20:00Z'));
    expect(clampSpan(l, axis.toX(t('2026-10-23T11:05:00Z')) - l, axis.width)).toBeNull();
  });
});
