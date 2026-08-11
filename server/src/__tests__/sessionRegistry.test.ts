import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
  bindPlayer,
  bindPondUser,
  unbindSocket,
  resolveBySocket,
  resolveSocketByPlayer,
  getBoundHumanSocketCount,
  isPlayerSocketBound,
  resetSessionRegistryForTests,
} from '../sessionRegistry.js';

describe('sessionRegistry', () => {
  beforeEach(() => {
    resetSessionRegistryForTests();
  });

  afterEach(() => {
    resetSessionRegistryForTests();
  });

  it('bindPlayer / unbindSocket updates counts', () => {
    expect(getBoundHumanSocketCount()).toBe(0);
    bindPlayer('p1', 's1', 'c1');
    expect(getBoundHumanSocketCount()).toBe(1);
    expect(isPlayerSocketBound('p1')).toBe(true);
    expect(resolveSocketByPlayer('p1')).toBe('s1');
    expect(resolveBySocket('s1')).toEqual({ playerId: 'p1', userId: undefined });

    unbindSocket('s1');
    expect(getBoundHumanSocketCount()).toBe(0);
    expect(isPlayerSocketBound('p1')).toBe(false);
  });

  it('rebind same player replaces previous socket', () => {
    bindPlayer('p1', 's1');
    bindPlayer('p1', 's2');
    expect(getBoundHumanSocketCount()).toBe(1);
    expect(resolveSocketByPlayer('p1')).toBe('s2');
    expect(resolveBySocket('s1').playerId).toBeUndefined();
  });

  it('bindPondUser links userId ↔ socket', () => {
    bindPondUser('u1', 's9', 'pond-calm');
    expect(resolveBySocket('s9')).toEqual({ playerId: undefined, userId: 'u1' });
    unbindSocket('s9');
    expect(resolveBySocket('s9')).toEqual({ playerId: undefined, userId: undefined });
  });
});
