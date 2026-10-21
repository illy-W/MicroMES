import { describe, expect, it } from 'vitest';
import { fitLeftChip, placeLabel } from './bar-label';

const base = { left: 200, width: 40, prevEnd: 0, nextStart: Infinity, labelW: 120, codeW: 40, maxRight: 2000 };

describe('placeLabel', () => {
  it('keeps the label inside wide bars', () => {
    expect(placeLabel({ ...base, width: 160 }).placement).toBe('inside');
  });
  it('puts the label on the right when there is room', () => {
    expect(placeLabel(base)).toEqual({ placement: 'right', side: 'right' });
  });
  it('goes left when the next bar is too close', () => {
    expect(placeLabel({ ...base, nextStart: 260 })).toEqual({ placement: 'left', side: 'left' });
  });
  it('falls back to the phase code on the right, then on the left', () => {
    expect(placeLabel({ ...base, nextStart: 300, prevEnd: 150 })).toEqual({ placement: 'code', side: 'right' });
    expect(placeLabel({ ...base, nextStart: 250, prevEnd: 80 })).toEqual({ placement: 'code', side: 'left' });
  });
  it('shows nothing when no space is available', () => {
    expect(placeLabel({ ...base, nextStart: 250, prevEnd: 190 }).placement).toBe('none');
  });
  it('does not overflow the right edge or the machine column', () => {
    expect(placeLabel({ ...base, left: 1950, maxRight: 2000, prevEnd: 1920 }).placement).toBe('none');
    expect(placeLabel({ ...base, left: 10, nextStart: 80 }).placement).toBe('none');
  });
});

describe('fitLeftChip', () => {
  it('keeps the full chip when it is fully visible', () => {
    expect(fitLeftChip(300, 120, 40, 100)).toBe('full');
  });
  it('falls back to the code when the full chip would sit under the sticky column', () => {
    expect(fitLeftChip(300, 120, 40, 250)).toBe('code');
  });
  it('hides the chip when not even the code fits', () => {
    expect(fitLeftChip(300, 120, 40, 290)).toBe('none');
  });
});
