import { getAuthSession } from './auth';

const STORAGE_KEY = 'fish_social_player_id';

export function getPlayerId(): string {
  const auth = getAuthSession();
  if (auth?.playerId) return auth.playerId;

  if (typeof localStorage !== 'undefined') {
    return localStorage.getItem(STORAGE_KEY) ?? '';
  }
  const g = globalThis as unknown as { __fishPlayerId?: string };
  return g.__fishPlayerId ?? '';
}
