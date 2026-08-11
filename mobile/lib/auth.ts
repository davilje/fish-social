import { clearStoredToken } from './jwtToken';

const AUTH_KEY = 'fish_social_auth';

export interface AuthSession {
  playerId: string;
  nickname: string;
  avatarUrl?: string;
  loggedIn: boolean;
}

function readStorage(): AuthSession | null {
  if (typeof localStorage === 'undefined') return null;
  const raw = localStorage.getItem(AUTH_KEY);
  if (!raw) return null;
  try {
    const data = JSON.parse(raw) as AuthSession;
    return data.loggedIn ? data : null;
  } catch {
    return null;
  }
}

function writeStorage(session: AuthSession | null) {
  if (typeof localStorage === 'undefined') return;
  if (!session) {
    localStorage.removeItem(AUTH_KEY);
    return;
  }
  localStorage.setItem(AUTH_KEY, JSON.stringify(session));
}

const memoryFallback: { session: AuthSession | null } = { session: null };

export function getAuthSession(): AuthSession | null {
  return readStorage() ?? memoryFallback.session;
}

export function isLoggedIn(): boolean {
  return !!getAuthSession()?.loggedIn;
}

export function setAuthSession(session: AuthSession) {
  memoryFallback.session = session;
  writeStorage(session);
}

export function updateAuthSession(patch: Partial<AuthSession>) {
  const current = getAuthSession();
  if (!current) return;
  const next = { ...current, ...patch, loggedIn: true };
  setAuthSession(next);
}

export function clearAuthSession() {
  memoryFallback.session = null;
  writeStorage(null);
  void clearStoredToken();
}

export function createPlayerId(): string {
  return `p_${Date.now()}_${Math.random().toString(36).slice(2, 10)}`;
}
