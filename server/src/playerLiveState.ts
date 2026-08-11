import { PONDS, type PondUser } from '@fish-social/shared';
import { enrichPondUser, listUsersInPond } from './pondUserManager.js';
import { loadPlayerPondSession, type SessionRow } from './playerPondSession.js';
import { resolveSocketByPlayer, resolveSocketByUser } from './sessionRegistry.js';
import { getServerLifecycleInfo } from './serverLifecycle.js';
import { db } from './db.js';

export interface LiveStateDiagnostic {
  id: string;
  level: 'error' | 'warn' | 'info';
  message: string;
}

export interface PlayerLiveState {
  playerId: string;
  found: boolean;
  pondId: string | null;
  user: null | {
    userId: string;
    nickname: string;
    spotId: string | null;
    status: string;
    fishingPhase: string | null;
    fishingStartedAt: number | null;
    sessionFishingMs: number;
    todayFishingMs: number;
    disconnectedAt: number | null;
    phaseEndsAt: number | null;
    isBot: boolean;
  };
  checkpoint: null | {
    exists: boolean;
    fishingPhase: string | null;
    spotId: string | null;
    disconnectedAt: number | null;
    updatedAt: number;
  };
  socketBound: boolean;
  diagnostics: LiveStateDiagnostic[];
  server: { startedAt: number; uptimeSec: number; pid: number };
}

function findLiveUser(playerId: string): { pondId: string; user: PondUser } | null {
  let botMatch: { pondId: string; user: PondUser } | null = null;
  for (const pond of PONDS) {
    for (const user of listUsersInPond(pond.id)) {
      if (user.playerId !== playerId) continue;
      if (!user.isBot) return { pondId: pond.id, user };
      botMatch ??= { pondId: pond.id, user };
    }
  }
  return botMatch;
}

function loadAnyCheckpoint(playerId: string): SessionRow | null {
  try {
    const row = db
      .prepare(
        `SELECT * FROM player_pond_session WHERE player_id = ? ORDER BY updated_at DESC LIMIT 1`,
      )
      .get(playerId) as SessionRow | undefined;
    return row ?? null;
  } catch {
    return null;
  }
}

function buildDiagnostics(
  found: boolean,
  user: PondUser | null,
  server: { uptimeSec: number },
): LiveStateDiagnostic[] {
  const diagnostics: LiveStateDiagnostic[] = [];
  if (!found) {
    diagnostics.push({
      id: 'player_not_in_pond',
      level: 'info',
      message: '玩家当前不在任何鱼塘内存中',
    });
  }
  if (user && user.status === 'fishing' && user.fishingStartedAt == null) {
    diagnostics.push({
      id: 'missing_fishing_started_at',
      level: 'error',
      message: 'status=fishing 但 fishingStartedAt=null，会话计时会卡死',
    });
  }
  if (server.uptimeSec < 300) {
    diagnostics.push({
      id: 'recent_server_start',
      level: 'warn',
      message: `服务启动仅 ${server.uptimeSec}s，留意 checkpoint_restore`,
    });
  }
  return diagnostics;
}

export function getPlayerLiveState(playerId: string): PlayerLiveState {
  const server = getServerLifecycleInfo();
  const match = findLiveUser(playerId);
  const enriched = match ? enrichPondUser(match.user) : null;

  let checkpoint: PlayerLiveState['checkpoint'] = null;
  if (match) {
    const row = loadPlayerPondSession(playerId, match.pondId);
    if (row) {
      checkpoint = {
        exists: true,
        fishingPhase: row.fishing_phase,
        spotId: row.spot_id,
        disconnectedAt: row.disconnected_at,
        updatedAt: row.updated_at,
      };
    } else {
      checkpoint = {
        exists: false,
        fishingPhase: null,
        spotId: null,
        disconnectedAt: null,
        updatedAt: 0,
      };
    }
  } else {
    const row = loadAnyCheckpoint(playerId);
    if (row) {
      checkpoint = {
        exists: true,
        fishingPhase: row.fishing_phase,
        spotId: row.spot_id,
        disconnectedAt: row.disconnected_at,
        updatedAt: row.updated_at,
      };
    }
  }

  let socketBound = false;
  if (match) {
    socketBound = !!(resolveSocketByUser(match.user.id) || (match.user.playerId && resolveSocketByPlayer(match.user.playerId)));
  } else {
    socketBound = !!resolveSocketByPlayer(playerId);
  }

  return {
    playerId,
    found: !!match,
    pondId: match?.pondId ?? null,
    user: enriched
      ? {
          userId: enriched.id,
          nickname: enriched.nickname,
          spotId: enriched.spotId,
          status: enriched.status,
          fishingPhase: enriched.fishingPhase ?? null,
          fishingStartedAt: enriched.fishingStartedAt,
          sessionFishingMs: enriched.sessionFishingMs ?? 0,
          todayFishingMs: enriched.todayFishingMs ?? 0,
          disconnectedAt: enriched.disconnectedAt ?? null,
          phaseEndsAt: enriched.phaseEndsAt ?? null,
          isBot: !!enriched.isBot,
        }
      : null,
    checkpoint,
    socketBound,
    diagnostics: buildDiagnostics(!!match, match?.user ?? null, server),
    server,
  };
}
