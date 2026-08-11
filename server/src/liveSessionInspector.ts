import type { Response } from 'express';
import { getPlayerFishingTimeline } from './fishingMetrics.js';
import { getPlayerLiveState, type PlayerLiveState } from './playerLiveState.js';
import { listUsersInPond } from './pondUserManager.js';

const MAX_CONNECTIONS = 5;
const activeConnections = new Map<string, Response>();
let connectionCount = 0;

const RECENT_EVENT_TYPES = new Set([
  'disconnect',
  'socket_disconnect',
  'reconnect',
  'disconnect_timeout',
  'leave_pond',
  'join_pond_attempt',
  'join_pond_success',
  'join_pond_fail',
  'server_start',
  'server_stop',
  'fishing_start',
  'fishing_stop',
]);

export interface LiveSessionTick {
  type: 'tick';
  live: PlayerLiveState;
  recentEvents: Array<{
    eventType: string;
    createdAt: number;
    pondId: string | null;
    payloadSummary: Record<string, unknown>;
  }>;
  pondHumans: number;
  pondBots: number;
  timestamp: number;
}

/** @deprecated 旧 payload 形状；保留类型以免外部引用断裂 */
export interface LiveSessionData {
  playerId: string;
  phase: string | null;
  fishingMs: number;
  biteCount: number;
  recentEvents: number;
  pondUsers: number;
  timestamp: number;
}

export function pushLiveSession(playerId: string, data: LiveSessionTick | LiveSessionData): void {
  for (const [pid, res] of activeConnections) {
    if (pid === playerId) {
      try {
        res.write('data: ' + JSON.stringify(data) + '\n\n');
      } catch {
        activeConnections.delete(pid);
        connectionCount = Math.max(0, connectionCount - 1);
      }
    }
  }
}

function buildTick(playerId: string): LiveSessionTick {
  const live = getPlayerLiveState(playerId);
  const timeline = getPlayerFishingTimeline(playerId, 6, 80);
  const recentEvents = timeline.events
    .filter((ev) => {
      if (RECENT_EVENT_TYPES.has(ev.eventType)) return true;
      const joinKind = ev.payload?.joinKind;
      return joinKind === 'checkpoint_restore';
    })
    .slice(-10)
    .reverse()
    .map((ev) => ({
      eventType: ev.eventType,
      createdAt: ev.createdAt,
      pondId: ev.pondId,
      payloadSummary: {
        ...(ev.payload.joinKind != null ? { joinKind: ev.payload.joinKind } : {}),
        ...(ev.payload.reason != null ? { reason: ev.payload.reason } : {}),
        ...(ev.payload.fishingPhase != null ? { fishingPhase: ev.payload.fishingPhase } : {}),
        ...(ev.payload.fromPhase != null ? { fromPhase: ev.payload.fromPhase } : {}),
        ...(ev.payload.toPhase != null ? { toPhase: ev.payload.toPhase } : {}),
      },
    }));

  let pondHumans = 0;
  let pondBots = 0;
  if (live.pondId) {
    for (const u of listUsersInPond(live.pondId)) {
      if (u.isBot) pondBots += 1;
      else pondHumans += 1;
    }
  }

  return {
    type: 'tick',
    live,
    recentEvents,
    pondHumans,
    pondBots,
    timestamp: Date.now(),
  };
}

export function registerLiveSession(playerId: string, res: Response): boolean {
  if (connectionCount >= MAX_CONNECTIONS) {
    return false;
  }

  const existing = activeConnections.get(playerId);
  if (existing) {
    try {
      existing.end();
    } catch {
      /* ignore */
    }
  }

  activeConnections.set(playerId, res);
  connectionCount++;

  res.writeHead(200, {
    'Content-Type': 'text/event-stream',
    'Cache-Control': 'no-cache',
    Connection: 'keep-alive',
    'X-Accel-Buffering': 'no',
  });

  res.write('data: ' + JSON.stringify({ type: 'connected', playerId }) + '\n\n');

  const interval = setInterval(() => {
    try {
      const tick = buildTick(playerId);
      res.write('data: ' + JSON.stringify(tick) + '\n\n');
    } catch {
      try {
        res.write(': heartbeat\n\n');
      } catch {
        clearInterval(interval);
        activeConnections.delete(playerId);
        connectionCount = Math.max(0, connectionCount - 1);
      }
    }
  }, 1000);

  res.on('close', () => {
    clearInterval(interval);
    activeConnections.delete(playerId);
    connectionCount = Math.max(0, connectionCount - 1);
  });

  return true;
}

export function getActiveConnectionCount(): number {
  return connectionCount;
}
