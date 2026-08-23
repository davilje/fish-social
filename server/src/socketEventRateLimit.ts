/**
 * STAB-04: per-socket sliding-window rate limit for high-frequency client events.
 * Env: SOCKET_EVENT_RATE_PER_SEC (default 20).
 */

const RATE_PER_SEC = Math.max(1, Number(process.env.SOCKET_EVENT_RATE_PER_SEC ?? 20));
const WINDOW_MS = 1000;

/** Events counted toward the per-socket budget */
export const RATE_LIMITED_SOCKET_EVENTS = new Set([
  'join_pond',
  'send_chat',
  'start_fishing',
  'groundbait_start',
  'take_spot',
  'leave_spot',
  'stop_fishing',
  'accept_catch',
]);

const timestampsBySocket = new Map<string, number[]>();

export function getSocketEventRatePerSec(): number {
  return RATE_PER_SEC;
}

/** Returns true if the event is allowed; false if rate-limited. */
export function allowSocketEvent(socketId: string, event: string): boolean {
  if (!RATE_LIMITED_SOCKET_EVENTS.has(event)) return true;
  const now = Date.now();
  let stamps = timestampsBySocket.get(socketId);
  if (!stamps) {
    stamps = [];
    timestampsBySocket.set(socketId, stamps);
  }
  const cutoff = now - WINDOW_MS;
  while (stamps.length > 0 && stamps[0]! < cutoff) stamps.shift();
  if (stamps.length >= RATE_PER_SEC) return false;
  stamps.push(now);
  return true;
}

export function clearSocketEventRate(socketId: string): void {
  timestampsBySocket.delete(socketId);
}

/** Test helper: reset all windows */
export function resetSocketEventRateLimitForTests(): void {
  timestampsBySocket.clear();
}
