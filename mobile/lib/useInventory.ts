import { useCallback, useEffect, useState } from 'react';
import type { FishInventoryItem } from '@fish-social/shared';
import { apiFetch } from './apiClient';

const DEMO_KEY = 'fish_social_demo_inventory';

function loadDemoInventory(playerId: string): FishInventoryItem[] {
  if (typeof localStorage === 'undefined') return [];
  try {
    const raw = localStorage.getItem(`${DEMO_KEY}_${playerId}`);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

function saveDemoInventory(playerId: string, items: FishInventoryItem[]) {
  if (typeof localStorage === 'undefined') return;
  localStorage.setItem(`${DEMO_KEY}_${playerId}`, JSON.stringify(items));
}

export function useInventory(playerId: string) {
  const [items, setItems] = useState<FishInventoryItem[]>([]);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    try {
      const data = await apiFetch<{ items: FishInventoryItem[] }>(`/api/inventory/${playerId}`);
      setItems(data.items ?? []);
    } catch {
      setItems(loadDemoInventory(playerId));
    } finally {
      setLoading(false);
    }
  }, [playerId]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const setItemsLocal = useCallback(
    (next: FishInventoryItem[] | ((prev: FishInventoryItem[]) => FishInventoryItem[])) => {
      setItems((prev) => {
        const resolved = typeof next === 'function' ? next(prev) : next;
        saveDemoInventory(playerId, resolved);
        return resolved;
      });
    },
    [playerId],
  );

  return { items, setItems: setItemsLocal, loading, refresh };
}
