import { useCallback, useEffect, useState } from 'react';
import type { BaitConfig, BaitId, PlayerGearState, TackleConfig, TackleId } from '@fish-social/shared';
import { BAITS, TACKLES } from '@fish-social/shared';
import { shopApiClient } from './shopApi';

export function useShop(playerId: string) {
  const [gear, setGear] = useState<PlayerGearState | null>(null);
  const [coins, setCoins] = useState(0);
  const [baits, setBaits] = useState<BaitConfig[]>([]);
  const [tackles, setTackles] = useState<TackleConfig[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [catalogOffline, setCatalogOffline] = useState(false);

  const refresh = useCallback(async () => {
    if (!playerId) return;
    setLoading(true);
    setError('');
    try {
      const [catalogBaits, catalogTackle, player] = await Promise.all([
        shopApiClient.listBaits(),
        shopApiClient.listTackle(),
        shopApiClient.getGear(playerId),
      ]);
      setBaits(catalogBaits.baits);
      setTackles(catalogTackle.tackles);
      setGear(player.gear);
      setCoins(player.coins);
      setCatalogOffline(false);
    } catch (e) {
      const message = e instanceof Error ? e.message : '加载失败';
      setError(message);
      setBaits(BAITS);
      setTackles(TACKLES);
      setCatalogOffline(true);
    } finally {
      setLoading(false);
    }
  }, [playerId]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const buyBait = useCallback(
    async (baitId: BaitId, quantity: number, idempotencyKey?: string) => {
      const res = await shopApiClient.buyBait(playerId, baitId, quantity, idempotencyKey);
      setGear(res.gear);
      setCoins(res.coins);
      return res;
    },
    [playerId],
  );

  const buyTackle = useCallback(
    async (tackleId: TackleId, idempotencyKey?: string) => {
      const res = await shopApiClient.buyTackle(playerId, tackleId, idempotencyKey);
      setGear(res.gear);
      setCoins(res.coins);
      return res;
    },
    [playerId],
  );

  const equipBait = useCallback(async (baitId: BaitId) => {
    const res = await shopApiClient.equipBait(playerId, baitId);
    setGear(res.gear);
    return res;
  }, [playerId]);

  const equipTackle = useCallback(async (tackleId: TackleId) => {
    const res = await shopApiClient.equipTackle(playerId, tackleId);
    setGear(res.gear);
    return res;
  }, [playerId]);

  return {
    gear,
    coins,
    baits,
    tackles,
    loading,
    error,
    catalogOffline,
    refresh,
    buyBait,
    buyTackle,
    equipBait,
    equipTackle,
    setGear,
    setCoins,
  };
}
