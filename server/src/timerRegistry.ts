import { randomUUID } from 'crypto';

export type TimerKind = 'disconnect_grace' | 'hook_legacy' | 'pending_expire';

export interface TimerRegistration {
  id: string;
  kind: string;
  userId?: string;
  socketId?: string;
  firesAt: number;
}

interface TimerEntry extends TimerRegistration {
  timer: NodeJS.Timeout;
  onFire: () => void;
}

const timersById = new Map<string, TimerEntry>();
const idsByUser = new Map<string, Set<string>>();
const idsBySocket = new Map<string, Set<string>>();

function indexId(map: Map<string, Set<string>>, key: string | undefined, id: string): void {
  if (!key) return;
  if (!map.has(key)) map.set(key, new Set());
  map.get(key)!.add(id);
}

function unindexId(map: Map<string, Set<string>>, key: string | undefined, id: string): void {
  if (!key) return;
  const set = map.get(key);
  if (!set) return;
  set.delete(id);
  if (set.size === 0) map.delete(key);
}

function unregister(id: string): void {
  const entry = timersById.get(id);
  if (!entry) return;
  clearTimeout(entry.timer);
  unindexId(idsByUser, entry.userId, id);
  unindexId(idsBySocket, entry.socketId, id);
  timersById.delete(id);
}

export function register(opts: {
  userId?: string;
  socketId?: string;
  kind: string;
  ms: number;
  onFire: () => void;
}): string {
  const id = randomUUID();
  const firesAt = Date.now() + opts.ms;
  const timer = setTimeout(() => {
    unregister(id);
    opts.onFire();
  }, opts.ms);
  const entry: TimerEntry = {
    id,
    kind: opts.kind,
    userId: opts.userId,
    socketId: opts.socketId,
    firesAt,
    timer,
    onFire: opts.onFire,
  };
  timersById.set(id, entry);
  indexId(idsByUser, opts.userId, id);
  indexId(idsBySocket, opts.socketId, id);
  return id;
}

export function cancelByUser(userId: string): void {
  const ids = idsByUser.get(userId);
  if (!ids) return;
  for (const id of [...ids]) unregister(id);
}

export function cancelBySocket(socketId: string): void {
  const ids = idsBySocket.get(socketId);
  if (!ids) return;
  for (const id of [...ids]) unregister(id);
}

export function cancelByKind(userId: string, kind: string): void {
  const ids = idsByUser.get(userId);
  if (!ids) return;
  for (const id of [...ids]) {
    const entry = timersById.get(id);
    if (entry?.kind === kind) unregister(id);
  }
}

export function cancelAll(): void {
  for (const id of [...timersById.keys()]) unregister(id);
}

export function listActive(): TimerRegistration[] {
  return [...timersById.values()].map(({ id, kind, userId, socketId, firesAt }) => ({
    id,
    kind,
    userId,
    socketId,
    firesAt,
  }));
}

export function hasActiveTimer(userId: string, kind: string): boolean {
  const ids = idsByUser.get(userId);
  if (!ids) return false;
  for (const id of ids) {
    const entry = timersById.get(id);
    if (entry?.kind === kind) return true;
  }
  return false;
}
