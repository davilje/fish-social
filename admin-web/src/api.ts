const STORAGE_KEY = 'fish-admin-key';
const API_BASE = import.meta.env.VITE_API_BASE ?? '';

export function getAdminKey(): string {
  return localStorage.getItem(STORAGE_KEY) ?? '';
}

export function setAdminKey(key: string): void {
  localStorage.setItem(STORAGE_KEY, key.trim());
}

async function adminFetch<T>(path: string, init?: RequestInit & { adminKey?: string }): Promise<T> {
  const { adminKey, ...rest } = init ?? {};
  const key = (adminKey ?? getAdminKey()).trim();
  const res = await fetch(`${API_BASE}${path}`, {
    ...rest,
    headers: {
      'Content-Type': 'application/json',
      'X-Admin-Key': key,
      ...(rest.headers ?? {}),
    },
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error((body as { error?: string }).error ?? `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export const api = {
  status: (adminKey?: string) => adminFetch<{ ok: boolean }>('/api/admin/status', { adminKey }),
  ponds: () => adminFetch<{ ponds: PondOverview[] }>('/api/admin/ponds'),
  playersOverview: (opts?: {
    hours?: number;
    humansOnly?: boolean;
    pondId?: string;
    phase?: string;
    q?: string;
  }) => {
    const q = new URLSearchParams();
    q.set('hours', String(opts?.hours ?? 24));
    q.set('humansOnly', opts?.humansOnly === false ? '0' : '1');
    if (opts?.pondId) q.set('pondId', opts.pondId);
    if (opts?.phase) q.set('phase', opts.phase);
    if (opts?.q) q.set('q', opts.q);
    return adminFetch<PlayersOverview>(`/api/admin/players/overview?${q}`);
  },
  playerTimeline: (playerId: string, hours = 24) =>
    adminFetch<PlayerTimeline>(`/api/admin/metrics/fishing/player/${encodeURIComponent(playerId)}?hours=${hours}`),
  fishingDebug: (pondId: string, playerId?: string, opts?: { refresh?: boolean }) => {
    const q = new URLSearchParams();
    if (opts?.refresh) q.set('refresh', '1');
    if (playerId) q.set('playerId', playerId);
    const qs = q.toString();
    return adminFetch<FishingDebugReport>(
      `/api/admin/ponds/${encodeURIComponent(pondId)}/fishing-debug${qs ? `?${qs}` : ''}`,
    );
  },
  pondFish: (pondId: string) =>
    adminFetch<{ pondId: string; fish: unknown[]; count: number }>(
      `/api/admin/ponds/${encodeURIComponent(pondId)}/fish`,
    ),
  businessHealth: (days = 7) =>
    adminFetch<BusinessHealthTrend>(`/api/admin/metrics/business-health?days=${days}`),
  liveState: (playerId: string) =>
    adminFetch<PlayerLiveState>(`/api/admin/players/${encodeURIComponent(playerId)}/live-state`),
  config: () => adminFetch<{ keys: string[]; entries: ConfigEntry[] }>('/api/admin/config'),
};

export interface PlayerOverviewRow {
  playerId: string;
  nickname: string;
  pondId: string | null;
  spotId?: string | null;
  pondName?: string | null;
  spotName?: string | null;
  fishingPhase: string | null;
  sessionFishingMs: number | null;
  catchCount: number;
  disconnectCount: number;
  biteHookCount: number;
  isBot: boolean;
  online: boolean;
  lastEventAt: number | null;
}

export interface PlayersOverview {
  hours: number;
  humansOnly: boolean;
  pondId: string | null;
  phase: string | null;
  q: string | null;
  rows: PlayerOverviewRow[];
}

export interface PondOverview {
  pondId: string;
  summary: {
    fishCount: number;
    maxPopulation: number;
  };
  humanCount: number;
  botCount: number;
  botRatio: number;
}

export interface PlayerTimeline {
  playerId: string;
  hours: number;
  events: Array<{
    id: string;
    eventType: string;
    pondId: string | null;
    createdAt: number;
    payload: Record<string, unknown>;
  }>;
  summary: Record<string, number | null> & {
    catchAcceptCount?: number;
    biteHookCount?: number;
    disconnectCount?: number;
    joinPondFailCount?: number;
    fishingStartCount?: number;
    phaseTransitionCount?: number;
  };
}

export interface FishingDebugReport {
  pondId: string;
  updatedAt: number;
  spots: Array<{
    spotId: string;
    tickBiteChance: number;
    fishAtSpotCount: number;
    spotMultiplier?: number;
  }>;
  activeFishers: Array<{
    userId: string;
    playerId?: string;
    nickname?: string;
    isBot: boolean;
    spotId: string;
    fishingPhase: string;
    phaseEndsAt?: number | null;
    fishingStartedAt: number | null;
    sessionFishingMs: number;
    disconnectedAt?: number | null;
    equippedBaitId: string;
    equippedTackleId: string;
  }>;
  summary: {
    totalFish: number;
    avgTickBiteChance: number;
  };
}

export interface BusinessHealthTrend {
  days: number;
  fromDate?: string;
  toDate?: string;
  catchSource?: 'inventory';
  catchNote?: string;
  daily: Array<{
    dateKey: string;
    totalCatch: number;
    disconnectRate: number;
    avgPopulation?: number | null;
    ponds: Array<{ pondId: string; catchCount: number; avgPopulation: number | null }>;
  }>;
  totals: Record<string, number>;
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
  diagnostics: Array<{ id: string; level: 'error' | 'warn' | 'info'; message: string }>;
  server: { startedAt: number; uptimeSec: number; pid: number };
}

export interface ConfigEntry {
  key: string;
  value: string;
  source: string;
}
