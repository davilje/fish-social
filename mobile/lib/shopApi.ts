import type { BaitId, PlayerGearState, TackleId } from '@fish-social/shared';
import { BAITS, TACKLES } from '@fish-social/shared';
import { apiFetch } from './apiClient';

async function shopApi<T>(
  path: string,
  init?: RequestInit & { idempotencyKey?: string },
): Promise<T> {
  const headers: Record<string, string> = {
    ...(init?.headers as Record<string, string> | undefined),
  };
  if (init?.idempotencyKey) {
    headers['X-Idempotency-Key'] = init.idempotencyKey;
  }
  const { idempotencyKey: _k, ...rest } = init ?? {};
  return apiFetch<T>(path, { ...rest, headers });
}
export const shopApiClient = {
  listBaits() {
    return shopApi<{ baits: typeof BAITS }>('/api/shop/baits');
  },
  listTackle() {
    return shopApi<{ tackles: typeof TACKLES }>('/api/shop/tackle');
  },
  getGear(playerId: string) {
    return shopApi<{ gear: PlayerGearState; coins: number }>(
      `/api/player/gear?playerId=${encodeURIComponent(playerId)}`,
    );
  },
  buyBait(playerId: string, baitId: BaitId, quantity: number, idempotencyKey?: string) {
    return shopApi<{ ok: boolean; gear: PlayerGearState; coins: number }>(
      '/api/shop/baits/buy',
      {
        method: 'POST',
        body: JSON.stringify({ playerId, baitId, quantity }),
        idempotencyKey,
      },
    );
  },
  buyTackle(playerId: string, tackleId: TackleId, idempotencyKey?: string) {
    return shopApi<{ ok: boolean; gear: PlayerGearState; coins: number }>(
      '/api/shop/tackle/buy',
      {
        method: 'POST',
        body: JSON.stringify({ playerId, tackleId }),
        idempotencyKey,
      },
    );
  },
  equipBait(playerId: string, baitId: BaitId) {
    return shopApi<{ ok: boolean; gear: PlayerGearState }>('/api/player/equip/bait', {
      method: 'POST',
      body: JSON.stringify({ playerId, baitId }),
    });
  },
  equipTackle(playerId: string, tackleId: TackleId) {
    return shopApi<{ ok: boolean; gear: PlayerGearState }>('/api/player/equip/tackle', {
      method: 'POST',
      body: JSON.stringify({ playerId, tackleId }),
    });
  },
};
