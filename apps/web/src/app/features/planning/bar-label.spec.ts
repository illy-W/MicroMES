import { describe, expect, it } from 'vitest';
import { barHitRect, fitLeftChip, placeLabel } from './bar-label';

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

describe('barHitRect', () => {
  it('is the block alone without chip', () => {
    expect(barHitRect(200, 40, 0, null)).toEqual({ left: 200, width: 40 });
  });
  it('extends to the right by the chip width', () => {
    expect(barHitRect(200, 40, 120, 'right')).toEqual({ left: 200, width: 160 });
  });
  it('extends to the left by the chip width', () => {
    expect(barHitRect(200, 40, 120, 'left')).toEqual({ left: 80, width: 160 });
  });
  it('never overlaps the next bar when the chip is placed on the right', () => {
    const input = { ...base, nextStart: 400 };
    const res = placeLabel(input);
    const hit = barHitRect(input.left, input.width, input.labelW, res.side);
    expect(hit.left + hit.width).toBeLessThanOrEqual(input.nextStart);
  });
  it('never overlaps the previous bar when the chip is placed on the left', () => {
    const input = { ...base, nextStart: 260, prevEnd: 50 };
    const res = placeLabel(input);
    expect(res.side).toBe('left');
    expect(barHitRect(input.left, input.width, input.labelW, res.side).left).toBeGreaterThanOrEqual(input.prevEnd);
  });
});
