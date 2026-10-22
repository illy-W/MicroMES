import { describe, expect, it } from 'vitest';
import { isWorkVisible } from './timeline-visibility';

describe('isWorkVisible', () => {
  it('hides ended work when not shown', () => {
    expect(isWorkVisible({ id: 1, status: 'Ended' }, { showEnded: false })).toBe(false);
  });
  it('shows ended work when requested', () => {
    expect(isWorkVisible({ id: 1, status: 'Ended' }, { showEnded: true })).toBe(true);
  });
  it('keeps selected or focused ended work', () => {
    expect(isWorkVisible({ id: 1, status: 'Ended' }, { showEnded: false, keepIds: [null, 1] })).toBe(true);
    expect(isWorkVisible({ id: 2, status: 'Ended' }, { showEnded: false, keepIds: [1, null] })).toBe(false);
  });
  it('never hides other statuses', () => {
    for (const status of ['Waiting', 'Ready', 'Running', 'Interrupted', 'Aborted'] as const) {
      expect(isWorkVisible({ id: 1, status }, { showEnded: false })).toBe(true);
    }
  });
});
