import { describe, expect, it } from 'vitest';
import { allowsDashedBorder, layoutBar, placementFor, visualWidth } from './bar-label';

const base = { left: 200, realW: 10, nextStart: Infinity, maxRight: 5000 };

describe('visualWidth', () => {
  it('extends short bars to the minimum when there is room', () => {
    expect(visualWidth(base)).toBe(72);
  });
  it('keeps long bars at their real width', () => {
    expect(visualWidth({ ...base, realW: 300 })).toBe(300);
  });
  it('shrinks to the room left before the next bar', () => {
    expect(visualWidth({ ...base, nextStart: 250 })).toBe(48);
  });
  it('never goes below the real width', () => {
    expect(visualWidth({ ...base, realW: 30, nextStart: 220 })).toBe(30);
  });
  it('never overlaps the next bar', () => {
    for (const next of [215, 240, 260, 280, 400]) {
      expect(200 + visualWidth({ ...base, nextStart: next })).toBeLessThanOrEqual(Math.max(next, 210));
    }
  });
  it('does not overflow the right edge of the axis', () => {
    expect(visualWidth({ ...base, left: 4980, maxRight: 5000 })).toBe(18);
  });
});

describe('placementFor', () => {
  it('degrades with the width', () => {
    expect(placementFor(100)).toBe('inside');
    expect(placementFor(72)).toBe('inside');
    expect(placementFor(50)).toBe('truncated');
    expect(placementFor(28)).toBe('code');
    expect(placementFor(8)).toBe('none');
  });
  it('layoutBar combines both', () => {
    expect(layoutBar({ ...base, nextStart: 250 })).toEqual({ visualWidth: 48, placement: 'truncated' });
  });
});

describe('allowsDashedBorder', () => {
  it('only on bars of at least 96px', () => {
    expect(allowsDashedBorder(95)).toBe(false);
    expect(allowsDashedBorder(96)).toBe(true);
  });
});
