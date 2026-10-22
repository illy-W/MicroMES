import { describe, expect, it } from 'vitest';
import { compatibleLabel, filterItems, parseActiveFilter } from './tooling-filters';

const items = [
  { code: 'ST-01', name: 'Stampo rosso', description: null, isActive: true },
  { code: 'SEGA', name: 'Sega', description: 'lama fine', isActive: false },
];

describe('tooling-filters', () => {
  it('filtra per stato', () => {
    expect(filterItems(items, '', 'active').map((i) => i.code)).toEqual(['ST-01']);
    expect(filterItems(items, '', 'inactive').map((i) => i.code)).toEqual(['SEGA']);
    expect(filterItems(items, '', 'all')).toHaveLength(2);
  });
  it('cerca in codice, nome e descrizione', () => {
    expect(filterItems(items, ' lama ', 'all').map((i) => i.code)).toEqual(['SEGA']);
    expect(filterItems(items, 'stampo', 'all').map((i) => i.code)).toEqual(['ST-01']);
  });
  it('etichetta macchine compatibili', () => {
    expect(compatibleLabel(0)).toBe('Tutte');
    expect(compatibleLabel(1)).toBe('1 macchina');
    expect(compatibleLabel(3)).toBe('3 macchine');
  });
  it('parse del filtro', () => {
    expect(parseActiveFilter('all')).toBe('all');
    expect(parseActiveFilter('x')).toBe('active');
    expect(parseActiveFilter(null)).toBe('active');
  });
});
