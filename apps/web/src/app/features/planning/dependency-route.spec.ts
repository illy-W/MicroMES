import { describe, expect, it } from 'vitest';
import { buildGraph, chainOf } from './dependency-graph';
import { isOffRange, routeConnector, stubRoute } from './dependency-route';

const box = (x: number, y: number, w = 100, h = 44) => ({ x, y, w, h });

describe('routeConnector', () => {
  it('goes straight when on the same row centerline with room', () => {
    const r = routeConnector(box(0, 0), box(150, 0));
    expect(r.points).toEqual([{ x: 100, y: 22 }, { x: 150, y: 22 }]);
    expect(r.backwards).toBe(false);
  });
  it('uses an elbow 8px after the end across rows', () => {
    const r = routeConnector(box(0, 0), box(150, 100));
    expect(r.points).toEqual([{ x: 100, y: 22 }, { x: 108, y: 22 }, { x: 108, y: 122 }, { x: 150, y: 122 }]);
    expect(r.d).toBe('M100 22 L108 22 L108 122 L150 122');
  });
  it('detours through the gap below when the successor is lower and too close', () => {
    const r = routeConnector(box(0, 0), box(104, 100));
    expect(r.points[2]).toEqual({ x: 108, y: 46 });
    expect(r.points[3]).toEqual({ x: 96, y: 46 });
    expect(r.points.at(-1)).toEqual({ x: 104, y: 122 });
  });
  it('detours above and flags stale (backwards) plans', () => {
    const r = routeConnector(box(200, 100), box(50, 0));
    expect(r.backwards).toBe(true);
    expect(r.points[2].y).toBe(98);
    expect(r.points.at(-1)).toEqual({ x: 50, y: 22 });
  });
  it('same row, successor before predecessor end, goes under the bar', () => {
    const r = routeConnector(box(0, 0), box(60, 0));
    expect(r.backwards).toBe(true);
    expect(r.points[2].y).toBe(46);
  });
});

describe('stub and range', () => {
  it('draws a short stub into the left edge', () => {
    expect(stubRoute(box(40, 10)).points).toEqual([{ x: 22, y: 32 }, { x: 40, y: 32 }]);
  });
  it('flags missing or fully left predecessors as off range', () => {
    expect(isOffRange(undefined)).toBe(true);
    expect(isOffRange(box(-200, 0))).toBe(true);
    expect(isOffRange(box(-50, 0))).toBe(false);
  });
});

describe('chainOf', () => {
  const g = buildGraph([{ id: 1, predecessorIds: [] }, { id: 2, predecessorIds: [1] }, { id: 3, predecessorIds: [2] }, { id: 4, predecessorIds: [1] }]);
  it('collects upstream and downstream transitively', () => {
    const c = chainOf(g, 2);
    expect([...c.up]).toEqual([1]);
    expect([...c.down]).toEqual([3]);
    expect([...chainOf(g, 1).down].sort()).toEqual([2, 3, 4]);
  });
  it('survives cycles', () => {
    const cy = buildGraph([{ id: 1, predecessorIds: [2] }, { id: 2, predecessorIds: [1] }]);
    expect([...chainOf(cy, 1).up]).toEqual([2]);
  });
});
