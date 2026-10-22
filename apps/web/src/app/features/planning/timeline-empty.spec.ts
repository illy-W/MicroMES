import { describe, expect, it } from 'vitest';
import { chooseEmptyMessage } from './timeline-empty';

describe('chooseEmptyMessage', () => {
  it('senza filtri mantiene il messaggio base', () => {
    const m = chooseEmptyMessage({ state: 'empty', activeFilters: [], lockedOnly: false, lockedCount: 0 });
    expect(m).toEqual({ kind: 'empty', title: 'Nessun lavoro in questo periodo', hint: null });
  });
  it('con filtri nomina la causa', () => {
    const m = chooseEmptyMessage({ state: 'empty', activeFilters: ['Stato: In attesa'], lockedOnly: false, lockedCount: 0 });
    expect(m.kind).toBe('filters');
    expect(m.title).toBe('Nessun lavoro corrisponde ai filtri');
    expect(m.hint).toBeNull();
  });
  it('solo fissati senza lavori fissati mostra il suggerimento', () => {
    const m = chooseEmptyMessage({ state: 'empty', activeFilters: ['Solo fissati'], lockedOnly: true, lockedCount: 0 });
    expect(m.hint).toContain('Nessun lavoro è fissato');
  });
  it('solo fissati con lavori fissati altrove non mostra il suggerimento', () => {
    const m = chooseEmptyMessage({ state: 'empty', activeFilters: ['Solo fissati'], lockedOnly: true, lockedCount: 3 });
    expect(m.hint).toBeNull();
  });
  it('onlyEnded ha la precedenza', () => {
    expect(chooseEmptyMessage({ state: 'onlyEnded', activeFilters: ['Solo fissati'], lockedOnly: true, lockedCount: 0 }).kind).toBe('onlyEnded');
  });
});
