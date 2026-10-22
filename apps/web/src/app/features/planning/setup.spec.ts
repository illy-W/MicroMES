import { describe, expect, it } from 'vitest';
import { setupDelta, setupLabel, setupSegmentWidth } from './setup';

const toX = (ms: number) => ms / 60_000; // 1 px per minuto

describe('setupSegmentWidth', () => {
  const base = { startMs: 0, endMs: 120 * 60_000, barWidth: 120, toX };
  it('converte i minuti sull’asse', () => expect(setupSegmentWidth({ ...base, setupMinutes: 30 })).toBe(30));
  it('nessun segmento con zero minuti o assente', () => {
    expect(setupSegmentWidth({ ...base, setupMinutes: 0 })).toBeNull();
    expect(setupSegmentWidth({ ...base, setupMinutes: null })).toBeNull();
  });
  it('barra troppo stretta: nessun segmento', () => expect(setupSegmentWidth({ ...base, barWidth: 10, setupMinutes: 5 })).toBeNull());
  it('taglia alla barra', () => expect(setupSegmentWidth({ ...base, setupMinutes: 500 })).toBe(120));
  it('rispetta la compressione dell’asse', () => {
    const compressed = (ms: number) => (ms <= 60 * 60_000 ? ms / 60_000 : 60 + 5); // da 60 min in poi: chiuso, 5 px di separatore
    expect(setupSegmentWidth({ startMs: 0, endMs: 600 * 60_000, barWidth: 200, setupMinutes: 120, toX: compressed })).toBe(65);
  });
});

describe('setupDelta', () => {
  it('peggioramento', () => expect(setupDelta(10, 25)).toMatchObject({ direction: 'up', text: 'Attrezzaggi: 10 → 25 min (+15)' }));
  it('miglioramento', () => expect(setupDelta(25, 10)).toMatchObject({ direction: 'down', text: 'Attrezzaggi: 25 → 10 min (-15)' }));
  it('invariato', () => expect(setupDelta(5, 5)?.direction).toBe('same'));
  it('assenti', () => expect(setupDelta(undefined, undefined)).toBeNull());
});

describe('setupLabel', () => {
  it('formatta', () => {
    expect(setupLabel(12, true)).toBe('attrezzaggio 12 min (completo)');
    expect(setupLabel(0)).toBe('');
  });
});
