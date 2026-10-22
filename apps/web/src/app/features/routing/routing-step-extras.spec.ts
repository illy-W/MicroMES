import { describe, expect, it } from 'vitest';
import { buildSetupValuesRequest, duplicateAttributeIds, rowsMissingAttribute, toggleId, toolingChoices } from './routing-step-extras';

describe('routing-step-extras', () => {
  const all = [
    { id: 1, code: 'ST-01', name: 'Stampo', isActive: true },
    { id: 2, code: 'SEGA', name: 'Sega', isActive: false },
  ];

  it('offre le attive e le collegate disattivate marcate', () => {
    const c = toolingChoices(all, [{ id: 2, code: 'SEGA', name: 'Sega', isActive: false }]);
    expect(c.map((x) => [x.id, x.linkedInactive])).toEqual([[1, false], [2, true]]);
  });

  it('non offre le disattivate non collegate', () => {
    expect(toolingChoices(all, []).map((x) => x.id)).toEqual([1]);
  });

  it('toggleId aggiunge e rimuove senza duplicati', () => {
    expect(toggleId([1], 2, true)).toEqual([1, 2]);
    expect(toggleId([1, 2], 2, true)).toEqual([1, 2]);
    expect(toggleId([1, 2], 1, false)).toEqual([2]);
  });

  it('rileva attributi duplicati e righe incomplete', () => {
    const rows = [{ attributeId: 1, value: 'a' }, { attributeId: 1, value: 'b' }, { attributeId: null, value: 'x' }, { attributeId: null, value: '' }];
    expect(duplicateAttributeIds(rows)).toEqual([1]);
    expect(rowsMissingAttribute(rows)).toBe(1);
  });

  it('richiesta: valori rifilati, rimossi e vuoti inviati con valore vuoto', () => {
    const rows = [{ attributeId: 1, value: ' rosso ' }, { attributeId: 3, value: '' }, { attributeId: null, value: '' }];
    expect(buildSetupValuesRequest(rows, [1, 2, 3])).toEqual([
      { attributeId: 1, value: 'rosso' },
      { attributeId: 3, value: '' },
      { attributeId: 2, value: '' },
    ]);
  });

  it('richiesta: nulla da inviare senza righe né valori salvati', () => {
    expect(buildSetupValuesRequest([], [])).toEqual([]);
  });
});
