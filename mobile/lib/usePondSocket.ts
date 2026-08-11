import { useEffect, useRef, useState, useCallback } from 'react';
import { AppState, Platform } from 'react-native';
import { io, Socket } from 'socket.io-client';
import {
  FISH_BITE_CHANCE,
  FISH_BITE_CHECK_MS,
  MAX_DAILY_FISHING_MS,
  getQualityInfo,
  getSpecies,
  isAnnounceQuality,
  isFishingActive,
  rollFishCatch,
  FISH_CATCH_SUCCESS_RATE,
  type ChatMessage,
  type ClientToServerEvents,
  type FishInventoryItem,
  type FishingMiss,
  type FishingPrompt,
  type FishingFloatTextPayload,
  type LeavePondReason,
  type PendingFishCatch,
  type PlayerGearState,
  type BaitId,
  type FishSpeciesId,
  type PondEcologySummary,
  type PondSnapshot,
  type PondUser,
  type ServerToClientEvents,
  type SessionTimerTickPayload,
} from '@fish-social/shared';
import { SOCKET_URL } from './config';
import { getValidAccessToken } from './apiClient';
import { DEMO_MESSAGES, DEMO_USERS } from './demoData';
import { formatFishingFloatText } from './i18n/fishing';
import { logPondApp, logPondSocket } from './pondLifecycleLog';

const EXPLICIT_DEMO_MODE =
  typeof process !== 'undefined' &&
  process.env.EXPO_PUBLIC_DEMO_MODE === '1';

type JoinQuotaSeed = {
  userId: string;
  todayFishingBaseMs: number;
  todayRemainingMs: number;
  quotaDateKey?: string;
};

function sessionAnchorOf(user: PondUser): number | null {
  return user.sessionStartedAt ?? user.fishingStartedAt ?? null;
}

/** 进塘 ack / 快照：把上海日额度写到本人用户上（未选钓点也要显示正确剩余） */
function applyQuotaSeedToUsers(
  prev: PondUser[],
  seed: JoinQuotaSeed,
  nickname: string,
): PondUser[] {
  const idx = prev.findIndex((u) => u.id === seed.userId);
  if (idx >= 0) {
    return prev.map((u) =>
      u.id === seed.userId
        ? {
            ...u,
            todayFishingBaseMs: seed.todayFishingBaseMs,
            todayFishingMs: Math.max(u.todayFishingMs ?? 0, seed.todayFishingBaseMs),
            todayRemainingMs: seed.todayRemainingMs,
          }
        : u,
    );
  }
  // 快照尚未到达时先种一颗本人节点，底栏可立即显示额度
  const stub: PondUser = {
    id: seed.userId,
    nickname,
    color: '#64B5F6',
    spotId: null,
    status: 'idle',
    fishingStartedAt: null,
    sessionStartedAt: null,
    todayFishingMs: seed.todayFishingBaseMs,
    todayFishingBaseMs: seed.todayFishingBaseMs,
    todayRemainingMs: seed.todayRemainingMs,
    fishingPhase: 'idle',
    phaseEndsAt: null,
  };
  return [...prev, stub];
}

function mergeSnapshotWithQuotaSeed(
  users: PondUser[],
  seed: JoinQuotaSeed | null,
  nickname = '钓友',
): PondUser[] {
  if (!seed) return users;
  let found = false;
  const merged = users.map((u) => {
    if (u.id !== seed.userId) return u;
    found = true;
    const snapBase =
      typeof u.todayFishingBaseMs === 'number'
        ? u.todayFishingBaseMs
        : typeof u.todayFishingMs === 'number'
          ? u.todayFishingMs
          : 0;
    // 若快照缺字段或为 0，而 join ack 已带回当日已用，保留 ack（同日）
    const base = Math.max(snapBase, seed.todayFishingBaseMs);
    return {
      ...u,
      todayFishingBaseMs: base,
      todayFishingMs: Math.max(u.todayFishingMs ?? 0, base),
      todayRemainingMs:
        typeof u.todayRemainingMs === 'number' && snapBase >= seed.todayFishingBaseMs
          ? u.todayRemainingMs
          : seed.todayRemainingMs,
    };
  });
  return found ? merged : applyQuotaSeedToUsers(merged, seed, nickname);
}

/** BUG-13：与 badge「垂钓中」对齐，不依赖 status==='fishing' */
function interpolateSessionFishingMs(user: PondUser, now: number): number | undefined {
  const startedAt = sessionAnchorOf(user);
  if (isFishingActive(user.fishingPhase) && user.fishingPhase !== 'stopping' && startedAt != null) {
    return Math.max(0, now - startedAt);
  }
  return user.sessionFishingMs ?? 0;
}

/**
 * BUG-13/19：在钓相位合并时保留秒表锚点。
 * finalize/stopping 清空锚点后不得复活；额度字段以服务端 base/remaining 为准。
 */
function mergePondUserUpdated(prev: PondUser, incoming: PondUser): PondUser {
  const nextPhase = incoming.fishingPhase ?? prev.fishingPhase;
  const stillFishing = isFishingActive(nextPhase) && nextPhase !== 'stopping';
  const merged: PondUser = { ...prev, ...incoming };

  // 额度字段：始终信任服务端
  if (typeof incoming.todayFishingBaseMs === 'number') {
    merged.todayFishingBaseMs = incoming.todayFishingBaseMs;
  }
  if (typeof incoming.todayRemainingMs === 'number') {
    merged.todayRemainingMs = incoming.todayRemainingMs;
  }
  if (typeof incoming.todayFishingMs === 'number') {
    merged.todayFishingMs = incoming.todayFishingMs;
  }

  if (!stillFishing) {
    if (nextPhase === 'stopping' || !isFishingActive(nextPhase)) {
      if (incoming.sessionStartedAt == null) merged.sessionStartedAt = null;
      if (incoming.fishingStartedAt == null) merged.fishingStartedAt = null;
    }
    return merged;
  }

  const prevAnchor = sessionAnchorOf(prev);
  const incomingAnchor = sessionAnchorOf(incoming);

  const newSession =
    incomingAnchor != null &&
    prevAnchor != null &&
    incomingAnchor > prevAnchor + 500;

  // 仅 legacy waiting 缺锚点时可保留；stopping/finalize 的 null 必须生效
  if (
    nextPhase === 'waiting' &&
    incomingAnchor == null &&
    prevAnchor != null
  ) {
    merged.sessionStartedAt = prev.sessionStartedAt ?? prev.fishingStartedAt;
    merged.fishingStartedAt = merged.sessionStartedAt;
  } else if (incomingAnchor != null) {
    merged.sessionStartedAt = incoming.sessionStartedAt ?? incoming.fishingStartedAt;
    merged.fishingStartedAt = merged.sessionStartedAt;
  }

  if (newSession) {
    merged.sessionFishingMs = incoming.sessionFishingMs ?? 0;
    return merged;
  }

  const incomingMs = incoming.sessionFishingMs;
  const anchor = sessionAnchorOf(merged);
  if (
    (incomingMs == null || incomingMs === 0) &&
    (prev.sessionFishingMs ?? 0) > 0 &&
    anchor != null
  ) {
    merged.sessionFishingMs = Math.max(prev.sessionFishingMs ?? 0, Date.now() - anchor);
  }

  return merged;
}

export interface FishingFloatDisplay {
  text: string;
  color: string;
  token: number;
}

export interface PondSocketCallbacks {
  onGearUpdate?: (gear: PlayerGearState) => void;
  onBaitDepleted?: (previousBaitId: BaitId) => void;
}

export interface PondConnectionProbe {
  connected: boolean;
  fishingPhase: string | null;
  spotId: string | null;
  lastDisconnectReason: string | null;
  lastDisconnectAt: number | null;
}

export function usePondSocket(
  pondId: string | null,
  nickname: string,
  playerId: string,
  onInventoryChange?: (items: FishInventoryItem[]) => void,
  callbacks?: PondSocketCallbacks,
) {
  const socketRef = useRef<Socket<ServerToClientEvents, ClientToServerEvents> | null>(null);
  const connectionGenerationRef = useRef(0);
  const leftPondRef = useRef(false);
  /** BUG-17：显式返回地图时，在卸载 cleanup 里再发 leave_pond（先导航后离塘） */
  const leaveOnUnmountRef = useRef<LeavePondReason | null>(null);
  const rejoinInFlightRef = useRef(false);
  const lastRejoinAtRef = useRef(0);
  const pondIdRef = useRef(pondId);
  pondIdRef.current = pondId;
  const nicknameRef = useRef(nickname);
  nicknameRef.current = nickname;
  const [connected, setConnected] = useState(false);
  const [users, setUsers] = useState<PondUser[]>([]);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [myUserId, setMyUserId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [demoMode, setDemoMode] = useState(false);
  const [snapshotReady, setSnapshotReady] = useState(false);
  const [fishingPrompt, setFishingPrompt] = useState<FishingPrompt | null>(null);
  const [ecology, setEcology] = useState<PondEcologySummary | null>(null);
  const [floatTexts, setFloatTexts] = useState<Record<string, FishingFloatDisplay>>({});
  const [accepting, setAccepting] = useState(false);
  const [lastDisconnectReason, setLastDisconnectReason] = useState<string | null>(null);
  const [lastDisconnectAt, setLastDisconnectAt] = useState<number | null>(null);
  const demoInventoryRef = useRef<FishInventoryItem[]>([]);
  const demoCaughtSpeciesRef = useRef<Set<FishSpeciesId>>(new Set());
  const promptRef = useRef<FishingPrompt | null>(null);
  const joinQuotaSeedRef = useRef<JoinQuotaSeed | null>(null);
  const onInventoryRef = useRef(onInventoryChange);
  onInventoryRef.current = onInventoryChange;
  const callbacksRef = useRef(callbacks);
  callbacksRef.current = callbacks;

  useEffect(() => {
    promptRef.current = fishingPrompt;
  }, [fishingPrompt]);

  const emitLeavePond = useCallback((reason: LeavePondReason, targetPondId?: string) => {
    const pid = targetPondId ?? pondIdRef.current;
    if (!pid || leftPondRef.current || !socketRef.current) return;
    leftPondRef.current = true;
    logPondSocket('leave_pond', { playerId, pondId: pid, reason });
    socketRef.current.emit('leave_pond', { pondId: pid, reason });
  }, [playerId]);

  /** 立即离塘（鉴权失效等） */
  const leavePondWithReason = useCallback(
    (reason: LeavePondReason) => {
      leaveOnUnmountRef.current = null;
      emitLeavePond(reason);
    },
    [emitLeavePond],
  );

  /** BUG-17：先导航；离塘延迟到本 hook 的 socket cleanup */
  const requestLeaveOnUnmount = useCallback((reason: LeavePondReason) => {
    leaveOnUnmountRef.current = reason;
  }, []);

  /** BUG-17：闩锁复位并重新 join（死态自愈） */
  const rejoinPond = useCallback(() => {
    return new Promise<{ ok: boolean; userId?: string; error?: string }>((resolve) => {
      if (demoMode) return resolve({ ok: true, userId: myUserId ?? 'demo-me' });
      const sock = socketRef.current;
      const pid = pondIdRef.current;
      if (!sock?.connected || !pid) {
        return resolve({ ok: false, error: '未连接' });
      }
      const now = Date.now();
      if (rejoinInFlightRef.current || now - lastRejoinAtRef.current < 2000) {
        return resolve({ ok: false, error: 'rejoin_throttled' });
      }
      rejoinInFlightRef.current = true;
      lastRejoinAtRef.current = now;
      leftPondRef.current = false;
      leaveOnUnmountRef.current = null;
      sock.emit(
        'join_pond',
        { pondId: pid, nickname: nicknameRef.current, playerId },
        (res) => {
          rejoinInFlightRef.current = false;
          if (res?.ok && res.userId) {
            setMyUserId(res.userId);
            setError(null);
            if (typeof res.todayFishingBaseMs === 'number') {
              const seed: JoinQuotaSeed = {
                userId: res.userId,
                todayFishingBaseMs: res.todayFishingBaseMs,
                todayRemainingMs:
                  typeof res.todayRemainingMs === 'number'
                    ? res.todayRemainingMs
                    : Math.max(0, MAX_DAILY_FISHING_MS - res.todayFishingBaseMs),
                quotaDateKey: res.quotaDateKey,
              };
              joinQuotaSeedRef.current = seed;
              setUsers((prev) => applyQuotaSeedToUsers(prev, seed, nicknameRef.current));
            }
            logPondSocket('join_pond_ok', {
              playerId,
              pondId: pid,
              userId: res.userId,
              reason: 'rejoin',
              todayFishingBaseMs: res.todayFishingBaseMs,
            });
          } else if (res?.error) {
            setError(res.error);
            logPondSocket('join_pond_fail', {
              playerId,
              pondId: pid,
              error: res.error,
              reason: 'rejoin',
            });
          }
          resolve(res ?? { ok: false, error: '失败' });
        },
      );
    });
  }, [demoMode, myUserId, playerId]);

  useEffect(() => {
    const sub = AppState.addEventListener('change', (state) => {
      if (state === 'active') logPondApp('foreground', { playerId, pondId: pondIdRef.current });
      else if (state === 'background') logPondApp('background', { playerId, pondId: pondIdRef.current });
    });
    return () => sub.remove();
  }, [playerId]);

  useEffect(() => {
    if (Platform.OS !== 'web' || typeof document === 'undefined') return;
    const onVisibility = () => {
      logPondApp(document.visibilityState === 'visible' ? 'visible' : 'hidden', {
        playerId,
        pondId: pondIdRef.current,
      });
    };
    document.addEventListener('visibilitychange', onVisibility);
    return () => document.removeEventListener('visibilitychange', onVisibility);
  }, [playerId]);

  useEffect(() => {
    if (!pondId) return;

    const generation = ++connectionGenerationRef.current;
    leftPondRef.current = false;
    let socket: Socket<ServerToClientEvents, ClientToServerEvents> | null = null;
    let cancelled = false;
    const activePondId = pondId;
    const isCurrent = () =>
      !cancelled &&
      generation === connectionGenerationRef.current &&
      pondIdRef.current === activePondId;

    setUsers([]);
    setMyUserId(null);
    setMessages([]);
    setEcology(null);
    setFloatTexts({});
    setFishingPrompt(null);
    setDemoMode(false);
    setSnapshotReady(false);
    setConnected(false);
    setError(null);
    joinQuotaSeedRef.current = null;

    void (async () => {
      const token = await getValidAccessToken(playerId);
      if (!isCurrent()) return;

      socket = io(SOCKET_URL, {
        transports: ['websocket'],
        timeout: 4000,
        reconnection: true,
        reconnectionAttempts: 6,
        reconnectionDelay: 5000,
        reconnectionDelayMax: 5000,
        auth: token ? { token } : {},
      });
      socketRef.current = socket;

      socket.on('connect', () => {
      if (!isCurrent()) return;
      const currentSocket = socketRef.current;
      if (!currentSocket) return;
      // A reconnect is a new snapshot boundary. Do not render the previous
      // pond/user quota while the server is rebuilding the current snapshot.
      setUsers([]);
      setMyUserId(null);
      setMessages([]);
      setEcology(null);
      setSnapshotReady(false);
      joinQuotaSeedRef.current = null;
      setConnected(true);
      setDemoMode(EXPLICIT_DEMO_MODE);
      setError(null);
      logPondSocket('connect', {
        playerId,
        pondId: activePondId,
        socketId: currentSocket.id,
        generation,
      });
      if (EXPLICIT_DEMO_MODE) {
        setUsers(DEMO_USERS.map((u) => ({ ...u, nickname: u.id === 'demo-me' ? nickname : u.nickname })));
        setMessages(DEMO_MESSAGES);
        setMyUserId('demo-me');
        setSnapshotReady(true);
        return;
      }
      currentSocket.emit('register_player', playerId);
      currentSocket.emit('join_pond', { pondId: activePondId, nickname, playerId }, (res) => {
        if (!isCurrent()) return;
        if (res?.ok && res.userId) {
          setMyUserId(res.userId);
          if (typeof res.todayFishingBaseMs === 'number') {
            const seed: JoinQuotaSeed = {
              userId: res.userId,
              todayFishingBaseMs: res.todayFishingBaseMs,
              todayRemainingMs:
                typeof res.todayRemainingMs === 'number'
                  ? res.todayRemainingMs
                  : Math.max(0, MAX_DAILY_FISHING_MS - res.todayFishingBaseMs),
              quotaDateKey: res.quotaDateKey,
            };
            joinQuotaSeedRef.current = seed;
            setUsers((prev) => applyQuotaSeedToUsers(prev, seed, nickname));
          }
          logPondSocket('join_pond_ok', {
            playerId,
            pondId: activePondId,
            userId: res.userId,
            generation,
            todayFishingBaseMs: res.todayFishingBaseMs,
          });
        } else if (res?.error) {
          setError(res.error);
          logPondSocket('join_pond_fail', { playerId, pondId: activePondId, error: res.error });
        }
      });
    });

    socket.on('disconnect', (reason) => {
      if (!isCurrent()) return;
      setConnected(false);
      setSnapshotReady(false);
      setUsers([]);
      setMyUserId(null);
      setMessages([]);
      setEcology(null);
      joinQuotaSeedRef.current = null;
      setLastDisconnectReason(reason);
      setLastDisconnectAt(Date.now());
      logPondSocket('disconnect', { playerId, pondId: activePondId, reason });
    });

    socket.io.on('reconnect_attempt', (attempt) => {
      logPondSocket('reconnect_attempt', { playerId, pondId: activePondId, attempt });
    });

    socket.on('pond_snapshot', (snapshot: PondSnapshot) => {
      if (!isCurrent() || snapshot.pond.id !== activePondId) return;
      setUsers(
        mergeSnapshotWithQuotaSeed(snapshot.users, joinQuotaSeedRef.current, nickname),
      );
      setMessages(snapshot.messages);
      if (snapshot.ecology) setEcology(snapshot.ecology);
      setSnapshotReady(true);
      if (snapshot.inventory) {
        onInventoryRef.current?.(snapshot.inventory);
      }
    });

    socket.on('pond_ecology_updated', (summary: PondEcologySummary) => {
      if (!isCurrent()) return;
      setEcology(summary);
    });

    socket.on('pond_user_joined', (user: PondUser) => {
      if (!isCurrent()) return;
      setUsers((prev) => {
        const existing = prev.find((u) => u.id === user.id);
        if (!existing) return [...prev, user];
        // 已有 join-ack 种子时合并，避免丢掉 todayFishingBaseMs
        return prev.map((u) => (u.id === user.id ? mergePondUserUpdated(u, user) : u));
      });
    });

    socket.on('pond_user_left', (userId: string) => {
      if (!isCurrent()) return;
      setUsers((prev) => prev.filter((u) => u.id !== userId));
    });

    socket.on('pond_user_updated', (user: PondUser) => {
      if (!isCurrent()) return;
      setUsers((prev) => {
        const existing = prev.find((u) => u.id === user.id);
        if (!existing) return [...prev, user];
        return prev.map((u) => (u.id === user.id ? mergePondUserUpdated(u, user) : u));
      });
    });

    // PERF-03b: merge duration only — phase/startedAt 走 pond_user_updated
    socket.on('session_timer_tick', (payload: SessionTimerTickPayload) => {
      if (!isCurrent()) return;
      setUsers((prev) =>
        prev.map((u) => {
          if (u.id !== payload.userId) return u;

          // 有本地锚点时以锚点为准，拒绝 0/回退 tick（避免 0秒↔正确秒数闪烁）
          const anchor = u.sessionStartedAt ?? u.fishingStartedAt;
          if (
            isFishingActive(u.fishingPhase) &&
            u.fishingPhase !== 'stopping' &&
            anchor != null
          ) {
            const localMs = Math.max(0, Date.now() - anchor);
            const sessionFishingMs = Math.max(payload.sessionFishingMs, localMs);
            if (u.sessionFishingMs === sessionFishingMs) return u;
            return { ...u, sessionFishingMs };
          }

          if (u.sessionFishingMs === payload.sessionFishingMs) return u;
          const next: PondUser = { ...u, sessionFishingMs: payload.sessionFishingMs };
          // C3：仅 waiting 缺锚点时可反推；stopping/finalize 后禁止复活
          if (
            u.fishingPhase === 'waiting' &&
            (u.sessionStartedAt ?? u.fishingStartedAt) == null &&
            payload.sessionFishingMs > 0
          ) {
            const started = Date.now() - payload.sessionFishingMs;
            next.sessionStartedAt = started;
            next.fishingStartedAt = started;
          }
          return next;
        }),
      );
    });

    socket.on('chat_message', (msg: ChatMessage) => {
      if (!isCurrent() || msg.pondId !== activePondId) return;
      setMessages((prev) => [...prev, msg]);
    });

    socket.on('fish_bite', (catchData: PendingFishCatch) => {
      if (!isCurrent()) return;
      if (promptRef.current) return;
      setFishingPrompt({ kind: 'catch', data: catchData });
    });

    socket.on('fish_miss', (miss: FishingMiss) => {
      if (!isCurrent()) return;
      if (promptRef.current) return;
      setFishingPrompt({ kind: 'miss', data: miss });
    });

    socket.on('fishing_float_text', (payload: FishingFloatTextPayload) => {
      if (!isCurrent() || payload.pondId !== activePondId) return;
      const { text, color } = formatFishingFloatText(
        payload.kind,
        payload.speciesId,
        payload.quality,
      );
      setFloatTexts((prev) => ({
        ...prev,
        [payload.userId]: { text, color, token: payload.timestamp },
      }));
    });

    socket.on('inventory_updated', (items: FishInventoryItem[]) => {
      if (!isCurrent()) return;
      onInventoryRef.current?.(items);
    });

    socket.on('gear_updated', (gear: PlayerGearState) => {
      if (!isCurrent()) return;
      callbacksRef.current?.onGearUpdate?.(gear);
    });

    socket.on('bait_depleted', (payload) => {
      if (!isCurrent()) return;
      callbacksRef.current?.onBaitDepleted?.(payload.previousBaitId);
    });

    socket.on('codex_unlocked', () => {
      /* 图鉴解锁提示，打开 CodexModal 重新加载 */
    });

    socket.on('error', (msg: string) => {
      if (isCurrent()) setError(msg);
    });

    socket.on('connect_error', (err) => {
      if (!isCurrent()) return;
      logPondSocket('connect_error', {
        playerId,
        pondId: activePondId,
        message: err?.message ?? String(err),
        generation,
      });
      setError('鱼塘连接失败，请检查服务端后重试');
    });
    })();

    return () => {
      cancelled = true;
      setFloatTexts({});
      // BUG-17：先导航后离塘 — 仅在显式 requestLeaveOnUnmount 时发 leave
      const pendingLeave = leaveOnUnmountRef.current;
      if (pendingLeave && socket && !leftPondRef.current) {
        leftPondRef.current = true;
        logPondSocket('leave_pond', {
          playerId,
          pondId: activePondId,
          reason: pendingLeave,
        });
        socket.emit('leave_pond', { pondId: activePondId, reason: pendingLeave });
      }
      leaveOnUnmountRef.current = null;
      socket?.disconnect();
      socketRef.current = null;
    };
  }, [pondId, nickname, playerId, emitLeavePond]);

  useEffect(() => {
    if (!demoMode) return;
    const timer = setInterval(() => {
      const t = Date.now();
      setUsers((prev) =>
        prev.map((u) => {
          if (u.status !== 'fishing' || u.fishingStartedAt === null) {
            if ((u.sessionFishingMs ?? 0) === 0) return u;
            return { ...u, sessionFishingMs: 0 };
          }
          const sessionFishingMs = t - u.fishingStartedAt;
          if (u.sessionFishingMs === sessionFishingMs) return u;
          return { ...u, sessionFishingMs };
        }),
      );
    }, 1000);
    return () => clearInterval(timer);
  }, [demoMode]);

  // BUG-08: live mode client-side interpolation so Modal open does not freeze session timer display
  useEffect(() => {
    if (demoMode) return;
    const timer = setInterval(() => {
      const now = Date.now();
      setUsers((prev) => {
        let changed = false;
        const next = prev.map((u) => {
          const sessionFishingMs = interpolateSessionFishingMs(u, now);
          if (sessionFishingMs === undefined || u.sessionFishingMs === sessionFishingMs) return u;
          changed = true;
          return { ...u, sessionFishingMs };
        });
        return changed ? next : prev;
      });
    }, 1000);
    return () => clearInterval(timer);
  }, [demoMode]);

  useEffect(() => {
    if (!demoMode) return;
    const timer = setInterval(() => {
      const me = users.find((u) => u.id === 'demo-me');
      if (!me || me.status !== 'fishing' || promptRef.current) return;
      if (Math.random() > FISH_BITE_CHANCE) return;
      if (Math.random() < FISH_CATCH_SUCCESS_RATE) {
        const rolled = rollFishCatch();
        const isCodexNew = !demoCaughtSpeciesRef.current.has(rolled.speciesId);
        setFishingPrompt({
          kind: 'catch',
          data: {
            catchId: `demo-${Date.now()}`,
            pondFishId: `demo-fish-${Date.now()}`,
            ...rolled,
            ...(isCodexNew ? { isCodexNew: true } : {}),
          },
        });
      } else {
        setFishingPrompt({
          kind: 'miss',
          data: {
            resultId: `demo-miss-${Date.now()}`,
            reason: Math.random() < 0.5 ? 'empty' : 'escaped',
          },
        });
      }
    }, FISH_BITE_CHECK_MS);
    return () => clearInterval(timer);
  }, [demoMode, users]);

  const startFishing = useCallback(
    () =>
      new Promise<{ ok: boolean; error?: string }>((resolve) => {
        if (demoMode) {
          const startedAt = Date.now();
          setUsers((prev) =>
            prev.map((u) =>
              u.id === 'demo-me' && u.spotId
                ? {
                    ...u,
                    status: 'fishing' as const,
                    fishingStartedAt: startedAt,
                    sessionStartedAt: startedAt,
                    sessionFishingMs: 0,
                    fishingPhase: 'waiting',
                    phaseEndsAt: null,
                  }
                : u,
            ),
          );
          return resolve({ ok: true });
        }
        if (!pondId || !socketRef.current) return resolve({ ok: false, error: '未连接' });
        socketRef.current.emit('start_fishing', { pondId }, (res) => {
          resolve(res ?? { ok: false, error: '失败' });
        });
      }),
    [pondId, demoMode],
  );

  const takeSpot = useCallback(
    (spotId: string) =>
      new Promise<{ ok: boolean; error?: string }>((resolve) => {
        if (demoMode) {
          setUsers((prev) => {
            const target = prev.find((u) => u.spotId === spotId && u.id !== 'demo-me');
            const next = prev
              .filter((u) => u.id !== target?.id)
              .map((u) =>
                u.id === 'demo-me'
                  ? {
                      ...u,
                      status: 'idle' as const,
                      spotId,
                      fishingPhase: 'seated' as const,
                      fishingStartedAt: null,
                      sessionFishingMs: 0,
                      phaseEndsAt: null,
                    }
                  : u,
              );
            return next;
          });
          return resolve({ ok: true });
        }
        if (!pondId || !socketRef.current) return resolve({ ok: false, error: '未连接' });
        socketRef.current.emit('take_spot', { pondId, spotId }, (res) => {
          resolve(res ?? { ok: false, error: '失败' });
        });
      }),
    [pondId, demoMode],
  );

  const stopFishing = useCallback(
    () =>
      new Promise<{
        ok: boolean;
        error?: string;
        todayFishingMs?: number;
        todayFishingBaseMs?: number;
        todayRemainingMs?: number;
        quotaDateKey?: string;
      }>((resolve) => {
        if (demoMode) {
          setUsers((prev) =>
            prev.map((u) => {
              if (u.id !== 'demo-me') return u;
              const started = u.sessionStartedAt ?? u.fishingStartedAt;
              const elapsed = started != null ? Date.now() - started : 0;
              const base = (u.todayFishingBaseMs ?? u.todayFishingMs) + elapsed;
              return {
                ...u,
                status: 'idle' as const,
                fishingStartedAt: null,
                sessionStartedAt: null,
                sessionFishingMs: 0,
                fishingPhase: 'seated',
                phaseEndsAt: null,
                todayFishingBaseMs: base,
                todayFishingMs: base,
                todayRemainingMs: Math.max(0, MAX_DAILY_FISHING_MS - base),
              };
            }),
          );
          return resolve({
            ok: true,
            todayFishingMs: undefined,
            todayRemainingMs: undefined,
          });
        }
        if (!pondId || !socketRef.current) return resolve({ ok: false, error: '未连接' });
        socketRef.current.emit('stop_fishing', pondId, (res) => {
          if (res?.ok) {
            const base =
              typeof res.todayFishingBaseMs === 'number'
                ? res.todayFishingBaseMs
                : res.todayFishingMs;
            setUsers((prev) =>
              prev.map((u) =>
                u.id === myUserId
                  ? {
                      ...u,
                      todayFishingMs: base ?? u.todayFishingMs,
                      todayFishingBaseMs: base ?? u.todayFishingBaseMs,
                      todayRemainingMs:
                        typeof res.todayRemainingMs === 'number'
                          ? res.todayRemainingMs
                          : u.todayRemainingMs,
                      fishingStartedAt: null,
                      sessionStartedAt: null,
                      sessionFishingMs: 0,
                    }
                  : u,
              ),
            );
          }
          resolve(res ?? { ok: false, error: '失败' });
        });
      }),
    [pondId, demoMode, myUserId],
  );

  const sendChat = useCallback(
    (text: string) =>
      new Promise<{ ok: boolean; error?: string }>((resolve) => {
        if (demoMode) {
          const msg: ChatMessage = {
            id: `local-${Date.now()}`,
            pondId: pondId!,
            userId: 'demo-me',
            nickname,
            text: text.trim(),
            createdAt: Date.now(),
          };
          setMessages((prev) => [...prev, msg]);
          return resolve({ ok: true });
        }
        if (!pondId || !socketRef.current) return resolve({ ok: false, error: '未连接' });
        socketRef.current.emit('send_chat', { pondId, text }, resolve);
      }),
    [pondId, demoMode, nickname],
  );

  const dismissFishingPrompt = useCallback(() => {
    setFishingPrompt(null);
  }, []);

  const acceptCatch = useCallback(async () => {
    if (!fishingPrompt || fishingPrompt.kind !== 'catch') {
      return { ok: false as const, error: '没有待领取的鱼获' };
    }
    const pendingCatch = fishingPrompt.data;
    setAccepting(true);
    try {
      if (demoMode) {
        const item: FishInventoryItem = {
          id: `fish-${Date.now()}`,
          speciesId: pendingCatch.speciesId,
          quality: pendingCatch.quality,
          sizeM: pendingCatch.sizeM,
          caughtAt: Date.now(),
        };
        demoInventoryRef.current = [...demoInventoryRef.current, item];
        demoCaughtSpeciesRef.current.add(pendingCatch.speciesId);
        onInventoryRef.current?.(demoInventoryRef.current);
        if (isAnnounceQuality(item.quality)) {
          const species = getSpecies(item.speciesId);
          const q = getQualityInfo(item.quality);
          const msg: ChatMessage = {
            id: `ann-${Date.now()}`,
            pondId: pondId!,
            userId: 'system',
            nickname: '系统',
            text: `${nickname} 钓到了 ${q.name} 品质的 ${species.name}！`,
            createdAt: Date.now(),
            type: 'announcement',
          };
          setMessages((prev) => [...prev, msg]);
        }
        setFishingPrompt(null);
        return { ok: true as const, item };
      }
      return await new Promise<{ ok: boolean; error?: string; item?: FishInventoryItem }>((resolve) => {
        socketRef.current?.emit('accept_catch', pendingCatch.catchId, (res) => {
          if (res?.ok) setFishingPrompt(null);
          resolve(res ?? { ok: false, error: '失败' });
        });
      });
    } finally {
      setAccepting(false);
    }
  }, [fishingPrompt, demoMode, pondId, nickname]);

  const confirmFishingPrompt = useCallback(async () => {
    if (!fishingPrompt || accepting) return { ok: true as const };
    if (fishingPrompt.kind === 'miss') {
      setFishingPrompt(null);
      return { ok: true as const };
    }
    return acceptCatch();
  }, [fishingPrompt, accepting, acceptCatch]);

  const me = users.find((u) => u.id === myUserId);
  const connectionProbe: PondConnectionProbe = {
    connected: connected,
    fishingPhase: me?.fishingPhase ?? me?.status ?? null,
    spotId: me?.spotId ?? null,
    lastDisconnectReason,
    lastDisconnectAt,
  };

  return {
    connected: connected,
    users,
    messages,
    myUserId,
    error,
    demoMode,
    snapshotReady,
    ecology,
    floatTexts,
    fishingPrompt,
    accepting,
    connectionProbe,
    leavePondWithReason,
    requestLeaveOnUnmount,
    rejoinPond,
    takeSpot,
    startFishing,
    stopFishing,
    sendChat,
    acceptCatch,
    confirmFishingPrompt,
    dismissFishingPrompt,
  };
}
