import { useCallback, useEffect, useState } from 'react';
import type { PlayerProfile } from '@fish-social/shared';
import { socialApi } from './socialApi';
import { updateAuthSession } from './auth';

export function useProfile(playerId: string, nickname: string) {
  const [profile, setProfile] = useState<PlayerProfile | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    if (!playerId) {
      setProfile(null);
      setLoading(false);
      return;
    }
    try {
      const { profile: p } = await socialApi.register(playerId, nickname);
      setProfile(p);
      updateAuthSession({
        nickname: p.nickname,
        avatarUrl: p.avatarUrl,
      });
    } catch {
      setProfile(null);
    } finally {
      setLoading(false);
    }
  }, [playerId, nickname]);
  useEffect(() => {
    refresh();
  }, [refresh]);

  return { profile, loading, refresh, setProfile };
}
