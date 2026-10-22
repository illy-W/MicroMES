import type { MoveConflictModel, StaleCause } from '@micromes/api-client/models';
import { dayKey, timeKey } from './timeline-time';

/** Testo italiano del motivo per cui un lavoro è obsoleto o fissato in conflitto (`None` = nessun testo). */
export function staleCauseLabel(cause: StaleCause | null | undefined): string | null {
  switch (cause) {
    case 'PredecessorEndsAfterStart':
      return 'Un predecessore finisce dopo l’inizio di questo lavoro';
    case 'MachineNotUsable':
      return 'La macchina assegnata non è più utilizzabile';
    case 'RunningPastPlannedEnd':
      return 'Il lavoro in corso supera la fine prevista';
    case 'LockedPredecessorConflict':
      return 'Fissato, ma in conflitto con un predecessore';
    case 'LockedMachineNotUsable':
      return 'Fissato, ma la macchina non è utilizzabile';
    default:
      return null;
  }
}

/** Istante UTC (ms) corrispondente a data `yyyy-mm-dd` e ora `HH:mm` nel fuso indicato; `null` se i valori non sono validi. */
export function wallToInstant(date: string, time: string, tz: string): number | null {
  const d = /^(\d{4})-(\d{2})-(\d{2})$/.exec(date);
  const t = /^(\d{2}):(\d{2})$/.exec(time);
  if (!d || !t) return null;
  const target = Date.UTC(+d[1], +d[2] - 1, +d[3], +t[1], +t[2]);
  let utc = target;
  // Due passate: la seconda assorbe il cambio di offset (ora legale).
  for (let i = 0; i < 2; i++) {
    const w = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dayKey(utc, tz));
    const hm = /^(\d{2}):(\d{2})$/.exec(timeKey(utc, tz));
    if (!w || !hm) return null;
    utc += target - Date.UTC(+w[1], +w[2] - 1, +w[3], +hm[1], +hm[2]);
  }
  return Number.isNaN(utc) ? null : utc;
}

/** Data e ora (valori per input nativi) di un istante nel fuso. */
export function instantToWall(ms: number, tz: string): { date: string; time: string } {
  return { date: dayKey(ms, tz), time: timeKey(ms, tz) };
}

/** Arrotonda per eccesso ai 5 minuti. */
export function ceilTo5Min(ms: number): number {
  const step = 5 * 60_000;
  return Math.ceil(ms / step) * step;
}

/**
 * Ricava `{date, time}` dal suggerimento «Prossimo orario disponibile: dd/MM HH:mm» di un 422.
 * L'anno non è nel messaggio: si usa quello della data richiesta (+1 se il suggerimento cadrebbe prima).
 */
export function parseSuggestion(message: string, requested: { date: string; time: string }): { date: string; time: string } | null {
  const m = /Prossimo orario disponibile:\s*(\d{2})\/(\d{2})(?:\/(\d{4}))?\s+(\d{2}):(\d{2})/.exec(message);
  if (!m) return null;
  let year = m[3] ? +m[3] : +requested.date.slice(0, 4);
  const build = (y: number) => `${y}-${m[2]}-${m[1]}`;
  if (!m[3] && build(year) < requested.date) year += 1;
  return { date: build(year), time: `${m[4]}:${m[5]}` };
}

export interface ConflictLine {
  icon: string;
  text: string;
}

/** Riga di avviso per un conflitto dell'anteprima di spostamento (icona + testo). */
export function conflictLine(c: MoveConflictModel): ConflictLine {
  const codes = c.workOrderCodes.join(', ');
  switch (c.code) {
    case 'PredecessorEndsAfterStart':
      return { icon: 'lucideTriangleAlert', text: `Il predecessore${codes ? ` ${codes}` : ''} finisce dopo il nuovo inizio${c.minutes ? ` (${c.minutes} min)` : ''}` };
    case 'SuccessorsStartBeforeEnd':
      return { icon: 'lucideTriangleAlert', text: `${c.workOrderCodes.length === 1 ? 'Il successore' : 'I successori'} ${codes} diventerà obsoleto: inizia prima della nuova fine` };
    case 'MachineOverCapacity':
      return { icon: 'lucideCircleAlert', text: `La macchina risulta sovraccarica${c.minutes ? ` (${c.minutes} min oltre la capacità)` : ''}` };
    case 'Late':
      return { icon: 'lucideAlarmClock', text: `Il lavoro sarà in ritardo${c.lateDays ? ` di ${c.lateDays} g` : ''}` };
    case 'ToolingOverCapacity':
      return { icon: 'lucideWrench', text: `Attrezzatura oltre la disponibilità${c.minutes ? ` (${c.minutes} min)` : ''}${c.message ? `: ${c.message}` : ''}` };
    case 'ToolingIncompatible':
      return { icon: 'lucideWrench', text: `Attrezzatura non compatibile con la macchina${c.message ? `: ${c.message}` : ''}` };
    default:
      return { icon: 'lucideTriangleAlert', text: c.message };
  }
}
