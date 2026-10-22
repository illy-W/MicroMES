import { describe, expect, it } from 'vitest';
import { formatElapsed, loadLevel, loadText, loadWidth, stateLabel } from './equipment-overview';

describe('equipment-overview', () => {
  it('formatta il tempo trascorso', () => {
    expect(formatElapsed(45)).toBe('da 45 min');
    expect(formatElapsed(80)).toBe('da 1 h 20 min');
    expect(formatElapsed(120)).toBe('da 2 h');
    expect(formatElapsed(1620)).toBe('da 1 g 3 h');
  });
  it('etichette di stato', () => {
    expect(stateLabel('Free')).toBe('Libera');
    expect(stateLabel('Working')).toBe('In lavorazione');
    expect(stateLabel('ClosedNow', 'Pausa')).toBe('Chiusa: Pausa');
    expect(stateLabel('ClosedNow')).toBe('Chiusa');
    expect(stateLabel('Inactive')).toBe('Disattivata');
  });
  it('livello di carico', () => {
    expect(loadLevel(null)).toBe('unknown');
    expect(loadLevel(89.9)).toBe('normal');
    expect(loadLevel(90)).toBe('high');
    expect(loadText(null)).toBe('n/d');
    expect(loadText(92.4)).toBe('92%');
    expect(loadWidth(150)).toBe(100);
  });
});
