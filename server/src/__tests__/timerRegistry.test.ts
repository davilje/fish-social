import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import {
  register,
  cancelByUser,
  cancelBySocket,
  cancelByKind,
  cancelAll,
  listActive,
  hasActiveTimer,
} from '../timerRegistry.js';

describe('timerRegistry', () => {
  beforeEach(() => {
    cancelAll();
    vi.useFakeTimers();
  });

  afterEach(() => {
    cancelAll();
    vi.useRealTimers();
  });

  it('fires callback after delay', () => {
    const onFire = vi.fn();
    register({ userId: 'u1', kind: 'disconnect_grace', ms: 1000, onFire });
    expect(onFire).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1000);
    expect(onFire).toHaveBeenCalledOnce();
    expect(listActive()).toHaveLength(0);
  });

  it('cancelByUser removes pending timers', () => {
    const onFire = vi.fn();
    register({ userId: 'u1', kind: 'hook_legacy', ms: 5000, onFire });
    cancelByUser('u1');
    vi.advanceTimersByTime(5000);
    expect(onFire).not.toHaveBeenCalled();
  });

  it('cancelByKind only removes matching kind', () => {
    const a = vi.fn();
    const b = vi.fn();
    register({ userId: 'u1', kind: 'pending_expire', ms: 3000, onFire: a });
    register({ userId: 'u1', kind: 'disconnect_grace', ms: 3000, onFire: b });
    cancelByKind('u1', 'pending_expire');
    vi.advanceTimersByTime(3000);
    expect(a).not.toHaveBeenCalled();
    expect(b).toHaveBeenCalledOnce();
  });

  it('hasActiveTimer reflects registration', () => {
    register({ userId: 'u2', socketId: 's1', kind: 'disconnect_grace', ms: 2000, onFire: () => {} });
    expect(hasActiveTimer('u2', 'disconnect_grace')).toBe(true);
    cancelBySocket('s1');
    expect(hasActiveTimer('u2', 'disconnect_grace')).toBe(false);
  });
});
