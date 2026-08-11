import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
  bindPlayer,
  resetSessionRegistryForTests,
} from '../sessionRegistry.js';
import { shouldRejectHumanJoin, getMaxHumanSockets } from '../humanCapacity.js';

describe('humanCapacity', () => {
  const prevLimit = process.env.MAX_HUMAN_SOCKETS;

  beforeEach(() => {
    process.env.MAX_HUMAN_SOCKETS = '3';
    resetSessionRegistryForTests();
  });

  afterEach(() => {
    resetSessionRegistryForTests();
    if (prevLimit === undefined) delete process.env.MAX_HUMAN_SOCKETS;
    else process.env.MAX_HUMAN_SOCKETS = prevLimit;
  });

  it('reads limit from env', () => {
    expect(getMaxHumanSockets()).toBe(3);
  });

  it('rejects new player when at limit', () => {
    bindPlayer('a', 's1');
    bindPlayer('b', 's2');
    bindPlayer('c', 's3');
    const r = shouldRejectHumanJoin('newbie');
    expect(r.reject).toBe(true);
    expect(r.current).toBe(3);
    expect(r.limit).toBe(3);
  });

  it('allows already-bound player when at limit (reconnect)', () => {
    bindPlayer('a', 's1');
    bindPlayer('b', 's2');
    bindPlayer('c', 's3');
    const r = shouldRejectHumanJoin('a');
    expect(r.reject).toBe(false);
    expect(r.current).toBe(3);
  });

  it('allows join when under limit', () => {
    bindPlayer('a', 's1');
    expect(shouldRejectHumanJoin('b').reject).toBe(false);
  });
});
