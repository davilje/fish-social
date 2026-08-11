import type { FishCodexEntry } from '@fish-social/shared';
import { apiFetch } from './apiClient';

export async function getCodex(playerId: string): Promise<FishCodexEntry[]> {
  const data = await apiFetch<{ entries?: FishCodexEntry[] }>(
    `/api/player/codex?playerId=${encodeURIComponent(playerId)}`,
  );
  return data.entries ?? [];
}
