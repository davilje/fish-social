import type { Socket } from 'socket.io';
import type { ClientToServerEvents, ServerToClientEvents } from '@fish-social/shared';
import { logStructuredEvent } from './fishingObservability.js';

const KNOWN_CLIENT_EVENTS = new Set<keyof ClientToServerEvents>([
  'join_pond',
  'leave_pond',
  'start_fishing',
  'groundbait_start',
  'stop_fishing',
  'send_chat',
  'accept_catch',
  'register_player',
]);

const KNOWN_SERVER_EVENTS = new Set<keyof ServerToClientEvents>([
  'pond_snapshot',
  'pond_ecology_updated',
  'pond_user_joined',
  'pond_user_left',
  'pond_user_updated',
  'session_timer_tick',
  'chat_message',
  'fish_bite',
  'fish_miss',
  'fishing_float_text',
  'bait_depleted',
  'gear_updated',
  'codex_unlocked',
  'inventory_updated',
  'dm_message',
  'friend_request',
  'error',
]);

const SAMPLE_RATE = Number(process.env.SOCKET_TAP_SAMPLE_RATE ?? 0.01);
const PAYLOAD_MAX_LEN = Number(process.env.SOCKET_TAP_PAYLOAD_MAX ?? 512);
const SENSITIVE_KEYS = new Set(['token', 'password', 'authorization', 'nickname', 'text']);

/** OBS-LOG-1: default ignore high-frequency server events in tap */
function getTapIgnoreEvents(): Set<string> {
  const raw =
    process.env.SOCKET_TAP_IGNORE_EVENTS ?? 'session_timer_tick,pond_ecology_updated';
  const set = new Set(
    raw
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean),
  );
  // Allow debug of session timer without clearing the whole ignore list
  if (process.env.SOCKET_TAP_INCLUDE_TIMER === '1' || process.env.SOCKET_TAP_INCLUDE_TIMER === 'true') {
    set.delete('session_timer_tick');
  }
  return set;
}

const TAP_IGNORE_EVENTS = getTapIgnoreEvents();

function shouldIgnoreTapEvent(eventName: string): boolean {
  return TAP_IGNORE_EVENTS.has(eventName);
}

function summarizePayload(payload: unknown): string {
  if (payload === undefined) return '';
  try {
    const masked = maskPayload(payload);
    const raw = typeof masked === 'string' ? masked : JSON.stringify(masked);
    return raw.length > PAYLOAD_MAX_LEN ? raw.slice(0, PAYLOAD_MAX_LEN) + '…' : raw;
  } catch {
    return '[unserializable]';
  }
}

function maskPayload(value: unknown): unknown {
  if (value === null || value === undefined) return value;
  if (Array.isArray(value)) return value.map(maskPayload);
  if (typeof value === 'object') {
    const out: Record<string, unknown> = {};
    for (const [key, val] of Object.entries(value as Record<string, unknown>)) {
      if (SENSITIVE_KEYS.has(key.toLowerCase())) {
        out[key] = '***';
      } else {
        out[key] = maskPayload(val);
      }
    }
    return out;
  }
  return value;
}

async function shouldTapSocket(playerId?: string): Promise<boolean> {
  if (playerId) {
    const { isDebugSampled } = await import('./debugSampler.js');
    if (isDebugSampled(playerId)) return true;
  }
  return Math.random() < SAMPLE_RATE;
}

function resolvePlayerId(
  socket: Socket<ClientToServerEvents, ServerToClientEvents>,
): string | undefined {
  return socket.data.authPlayerId as string | undefined;
}

export function registerSocketEventTap(
  socket: Socket<ClientToServerEvents, ServerToClientEvents>,
  correlationId: string,
): void {
  socket.onAny((eventName, ...args) => {
    void (async () => {
      if (shouldIgnoreTapEvent(eventName)) return;
      const playerId = resolvePlayerId(socket);
      if (!(await shouldTapSocket(playerId))) return;
      const payloadSummary = summarizePayload(args[0]);
      logStructuredEvent('socket_tap', 'socket_tap_in', {
        direction: 'in',
        eventName,
        socketId: socket.id,
        playerId,
        correlationId,
        payloadSummary,
      });
      if (!KNOWN_CLIENT_EVENTS.has(eventName as keyof ClientToServerEvents)) {
        logStructuredEvent('socket_tap', 'socket_tap_unknown', {
          direction: 'in',
          eventName,
          socketId: socket.id,
          playerId,
          correlationId,
        });
      }
    })();
  });

  socket.onAnyOutgoing((eventName, ...args) => {
    void (async () => {
      if (shouldIgnoreTapEvent(eventName)) return;
      const playerId = resolvePlayerId(socket);
      if (!(await shouldTapSocket(playerId))) return;
      const payloadSummary = summarizePayload(args[0]);
      logStructuredEvent('socket_tap', 'socket_tap_out', {
        direction: 'out',
        eventName,
        socketId: socket.id,
        playerId,
        correlationId,
        payloadSummary,
      });
      if (!KNOWN_SERVER_EVENTS.has(eventName as keyof ServerToClientEvents)) {
        logStructuredEvent('socket_tap', 'socket_tap_unknown', {
          direction: 'out',
          eventName,
          socketId: socket.id,
          playerId,
          correlationId,
        });
      }
    })();
  });
}
