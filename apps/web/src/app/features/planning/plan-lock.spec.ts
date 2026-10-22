import { describe, expect, it } from 'vitest';
import { ceilTo5Min, conflictLine, instantToWall, parseSuggestion, staleCauseLabel, wallToInstant } from './plan-lock';

const TZ = 'Europe/Rome';

describe('plan-lock', () => {
  it('converte ora aziendale (inverno, CET) in UTC e ritorno', () => {
    const ms = wallToInstant('2026-01-15', '08:30', TZ);
    expect(new Date(ms as number).toISOString()).toBe('2026-01-15T07:30:00.000Z');
    expect(instantToWall(ms as number, TZ)).toEqual({ date: '2026-01-15', time: '08:30' });
  });

  it('gestisce l’ora legale (CEST)', () => {
    const ms = wallToInstant('2026-07-15', '08:30', TZ);
    expect(new Date(ms as number).toISOString()).toBe('2026-07-15T06:30:00.000Z');
  });

  it('rifiuta valori non validi', () => {
    expect(wallToInstant('', '08:00', TZ)).toBeNull();
    expect(wallToInstant('2026-01-15', '8', TZ)).toBeNull();
  });

  it('arrotonda ai 5 minuti', () => {
    expect(ceilTo5Min(Date.UTC(2026, 0, 1, 10, 1))).toBe(Date.UTC(2026, 0, 1, 10, 5));
    expect(ceilTo5Min(Date.UTC(2026, 0, 1, 10, 5))).toBe(Date.UTC(2026, 0, 1, 10, 5));
  });

  it('etichette dei motivi', () => {
    expect(staleCauseLabel('None')).toBeNull();
    expect(staleCauseLabel('LockedPredecessorConflict')).toContain('predecessore');
    expect(staleCauseLabel('LockedMachineNotUsable')).toContain('macchina');
  });

  it('legge il suggerimento del 422', () => {
    const msg = 'SAL-1 è fuori turno il 03/11. Prossimo orario disponibile: 03/11 13:00';
    expect(parseSuggestion(msg, { date: '2026-11-03', time: '09:00' })).toEqual({ date: '2026-11-03', time: '13:00' });
    expect(parseSuggestion('Prossimo orario disponibile: 02/01 08:00', { date: '2026-12-30', time: '09:00' })).toEqual({ date: '2027-01-02', time: '08:00' });
    expect(parseSuggestion('altro', { date: '2026-11-03', time: '09:00' })).toBeNull();
  });

  it('descrive i conflitti', () => {
    const base = { message: 'm', workOrderIds: [1], workOrderCodes: ['A', 'B'] };
    expect(conflictLine({ ...base, code: 'SuccessorsStartBeforeEnd' }).text).toContain('A, B');
    expect(conflictLine({ ...base, code: 'Late', lateDays: 2 }).text).toContain('2 g');
    expect(conflictLine({ ...base, code: 'ToolingOverCapacity', minutes: 30 })).toMatchObject({ icon: 'lucideWrench' });
    expect(conflictLine({ ...base, code: 'ToolingIncompatible' }).text).toContain('non compatibile');
  });
});
