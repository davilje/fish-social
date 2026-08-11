import { API_BASE } from './config';
import {
  getStoredTokenRecord,
  setStoredToken,
  shouldRefreshToken,
} from './jwtToken';
import { getAuthSession } from './auth';

let refreshPromise: Promise<string | null> | null = null;

export async function fetchDevToken(playerId: string): Promise<string> {
  const res = await fetch(`${API_BASE}/api/auth/dev-token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ playerId }),
  });
  const data = (await res.json()) as { token?: string; error?: string };
  if (!res.ok || !data.token) {
    throw new Error(data.error ?? '获取 token 失败');
  }
  await setStoredToken(data.token, playerId);
  return data.token;
}

export async function getValidAccessToken(playerId?: string): Promise<string | null> {
  const resolvedPlayerId = playerId ?? getAuthSession()?.playerId;
  if (!resolvedPlayerId) return null;

  const record = await getStoredTokenRecord();
  if (!record || record.playerId !== resolvedPlayerId) {
    try {
      return await fetchDevToken(resolvedPlayerId);
    } catch {
      return null;
    }
  }

  if (!shouldRefreshToken(record.expSec)) {
    return record.token;
  }

  if (!refreshPromise) {
    refreshPromise = fetchDevToken(resolvedPlayerId)
      .catch(() => record.token)
      .finally(() => {
        refreshPromise = null;
      });
  }
  return refreshPromise;
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const session = getAuthSession();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(init?.headers as Record<string, string> | undefined),
  };
  if (session?.playerId) {
    const token = await getValidAccessToken(session.playerId);
    if (token) headers.Authorization = `Bearer ${token}`;
  }

  const res = await fetch(`${API_BASE}${path}`, { ...init, headers });
  const data = await res.json();
  if (!res.ok) {
    const err = new Error((data as { error?: string }).error ?? '请求失败') as Error & {
      code?: string;
    };
    if (typeof (data as { code?: string }).code === 'string') {
      err.code = (data as { code: string }).code;
    }
    throw err;
  }
  return data as T;
}

export async function apiFetchRaw(path: string, init?: RequestInit): Promise<Response> {
  const session = getAuthSession();
  const headers: Record<string, string> = {
    ...(init?.headers as Record<string, string> | undefined),
  };
  if (session?.playerId) {
    const token = await getValidAccessToken(session.playerId);
    if (token) headers.Authorization = `Bearer ${token}`;
  }
  return fetch(`${API_BASE}${path}`, { ...init, headers });
}
