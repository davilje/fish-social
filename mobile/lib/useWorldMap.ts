import { useEffect, useState } from 'react';
import type { WorldPondRegion } from '@fish-social/shared';
import { API_BASE } from './config';
import { DEMO_WORLD } from './demoData';

interface WorldData {
  regions: WorldPondRegion[];
  ponds: { id: string; name: string; regionId: string }[];
  occupancy: Record<string, number>;
}

export function useWorldMap() {
  const [data, setData] = useState<WorldData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [demoMode, setDemoMode] = useState(false);

  const refresh = async () => {
    try {
      const res = await fetch(`${API_BASE}/api/world`);
      if (!res.ok) throw new Error('加载地图失败');
      setData(await res.json());
      setError(null);
      setDemoMode(false);
    } catch (e) {
      setData(DEMO_WORLD);
      setDemoMode(true);
      setError(e instanceof Error ? e.message : '未知错误');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    refresh();
    const timer = setInterval(refresh, 5000);
    return () => clearInterval(timer);
  }, []);

  return { data, loading, error, demoMode, refresh };
}
