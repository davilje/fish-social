import { apiFetch } from './apiClient';

const ADMIN_KEY_STORAGE = 'fish-social-admin-key';

export function getStoredAdminKey(): string {
  if (typeof localStorage === 'undefined') return '';
  return localStorage.getItem(ADMIN_KEY_STORAGE) ?? '';
}

export function setStoredAdminKey(key: string): void {
  if (typeof localStorage === 'undefined') return;
  localStorage.setItem(ADMIN_KEY_STORAGE, key);
}

async function adminApi<T>(path: string, init?: RequestInit): Promise<T> {
  const key = getStoredAdminKey();
  return apiFetch<T>(path, {
    ...init,
    headers: {
      'X-Admin-Key': key,
      ...(init?.headers ?? {}),
    },
  });
}
export const adminApiClient = {
  status() {
    return adminApi<{ ok: boolean; hint: string }>('/api/admin/status');
  },
  listPonds() {
    return adminApi<{ ponds: { pondId: string; summary: import('@fish-social/shared').PondEcologySummary }[] }>(
      '/api/admin/ponds',
    );
  },
  listPondFish(pondId: string) {
    return adminApi<{ pondId: string; fish: import('@fish-social/shared').PondFishEntity[]; count: number }>(
      `/api/admin/ponds/${pondId}/fish`,
    );
  },
  getFishingDebug(
    pondId: string,
    options?: { refresh?: boolean; baitId?: string; tackleId?: string; playerId?: string },
  ) {
    const params = new URLSearchParams();
    if (options?.refresh) params.set('refresh', '1');
    if (options?.baitId) params.set('baitId', options.baitId);
    if (options?.tackleId) params.set('tackleId', options.tackleId);
    if (options?.playerId) params.set('playerId', options.playerId);
    const q = params.toString();
    return adminApi<import('@fish-social/shared').PondFishingDebugResponse>(
      `/api/admin/ponds/${pondId}/fishing-debug${q ? `?${q}` : ''}`,
    );
  },
  resetEcology() {
    return adminApi<{ ok: boolean; message: string }>('/api/admin/ecology/reset', { method: 'POST' });
  },
  clearUsers() {
    return adminApi<{ ok: boolean; message: string }>('/api/admin/users/clear', { method: 'POST' });
  },
  getLogs(limit = 100) {
    return adminApi<{ logs: { id: string; message: string; stack?: string; context?: string; createdAt: number }[] }>(
      `/api/admin/logs?limit=${limit}`,
    );
  },
  clearLogs() {
    return adminApi<{ ok: boolean }>('/api/admin/logs/clear', { method: 'POST' });
  },
  getConfig() {
    return adminApi<{ keys: string[]; entries: import('@fish-social/shared').GameConfigEntryView[] }>(
      '/api/admin/config',
    );
  },
  getConfigPending() {
    return adminApi<{
      requests: Array<{ id: string; configKey: string; proposedValue: string; submittedBy: string }>;
    }>('/api/admin/config/pending');
  },
  submitConfigChange(key: string, value: string, submittedBy: string) {
    return adminApi('/api/admin/config/submit', {
      method: 'POST',
      body: JSON.stringify({ key, value, submittedBy }),
    });
  },
  approveConfigChange(requestId: string, approver: string) {
    return adminApi('/api/admin/config/approve', {
      method: 'POST',
      body: JSON.stringify({ requestId, approver }),
    });
  },
  getFishingMetrics(hours = 168) {
    return adminApi<import('@fish-social/shared').FishingMetricsSummary>(
      `/api/admin/metrics/fishing?hours=${hours}`,
    );
  },
  getBusinessHealth(days = 7) {
    return adminApi<{
      days: number;
      fromDate: string;
      toDate: string;
      daily: Array<{
        dateKey: string;
        totalCatch: number;
        totalDisconnect: number;
        biteTickHit: number;
        biteTickMiss: number;
        biteHitRate: number;
        disconnectRate: number;
        activePlayers: number;
        ponds: Array<{
          pondId: string;
          catchCount: number;
          biteTickHit: number;
          biteTickMiss: number;
          disconnectCount: number;
          avgPopulation: number | null;
          biteHitRate: number;
          disconnectRate: number;
        }>;
      }>;
      totals: {
        catchCount: number;
        disconnectCount: number;
        biteTickHit: number;
        biteTickMiss: number;
        activePlayers: number;
      };
    }>(`/api/admin/metrics/business-health?days=${days}`);
  },
  getTraces(correlationId: string) {
    return adminApi<{
      correlationId: string;
      traceId: string;
      otelEnabled: boolean;
      spans: Array<{
        name: string;
        correlationId: string;
        traceId: string;
        durationMs: number;
        status: string;
      }>;
      count: number;
    }>(`/api/admin/traces?correlationId=${encodeURIComponent(correlationId)}`);
  },
  getPlayerFishingTimeline(playerId: string, hours = 24, limit = 500) {
    return adminApi<import('@fish-social/shared').PlayerFishingTimeline>(
      `/api/admin/metrics/fishing/player/${encodeURIComponent(playerId)}?hours=${hours}&limit=${limit}`,
    );
  },
};
