import { useMemo, useState, useCallback } from 'react';
import { View, Text, StyleSheet, Pressable } from 'react-native';
import { useRouter } from 'expo-router';
import { AppScreen } from '../components/AppScreen';
import { BackpackButton } from '../components/BackpackButton';
import { ShopButton } from '../components/ShopButton';
import { CodexButton } from '../components/CodexButton';
import { SocialButton } from '../components/SocialButton';
import { LeaderboardButton } from '../components/LeaderboardButton';
import { BackpackModal } from '../components/BackpackModal';
import { ShopModal } from '../components/ShopModal';
import { CodexModal } from '../components/CodexModal';
import { ProfileAvatar } from '../components/ProfileAvatar';
import { WorldMapView } from '../components/WorldMapView';
import { useWorldMap } from '../lib/useWorldMap';
import { getNickname } from '../lib/config';
import { getPlayerId } from '../lib/playerId';
import { useInventory } from '../lib/useInventory';
import { useProfile } from '../lib/useProfile';
import { useShop } from '../lib/useShop';
import { socialApi } from '../lib/socialApi';
import { AdminDebugButton } from '../components/AdminDebugButton';
import { useResponsive } from '../lib/responsive';
import { useRequireAuth } from '../lib/useRequireAuth';
import { colors, spacing, radius } from '../lib/theme';

export default function WorldMapScreen() {
  const router = useRouter();
  const { ready, authenticated, session } = useRequireAuth();
  const { data, loading, error, demoMode } = useWorldMap();
  const nickname = useMemo(() => getNickname(), []);
  const playerId = useMemo(() => getPlayerId(), []);
  const { isMobile, isCompact, contentPadding, titleSize } = useResponsive();
  const [backpackOpen, setBackpackOpen] = useState(false);
  const [shopOpen, setShopOpen] = useState(false);
  const [codexOpen, setCodexOpen] = useState(false);
  const { items, loading: invLoading, setItems } = useInventory(playerId);
  const { profile, setProfile } = useProfile(playerId, nickname);
  const shop = useShop(playerId);

  const openShop = useCallback(async () => {
    await shop.refresh();
    setShopOpen(true);
  }, [shop]);

  const pondByRegion = useMemo(() => {
    const map: Record<string, { id: string; name: string }> = {};
    data?.ponds.forEach((p) => {
      map[p.regionId] = { id: p.id, name: p.name };
    });
    return map;
  }, [data]);

  if (!ready || !authenticated) return null;

  return (
    <AppScreen>
      {/* L0 顶栏：永远最上层 */}
      <View style={[styles.header, { paddingHorizontal: contentPadding }]}>
        <View style={styles.headerLeft}>
          <Text style={[styles.title, { fontSize: titleSize }]}>Fish Social</Text>
          {!isCompact && (
            <Text style={styles.subtitle} numberOfLines={1}>
              钓鱼世界 · 你好，{nickname}
            </Text>
          )}
        </View>
        <View style={styles.headerRight}>
          {demoMode && (
            <View style={styles.demoBadge}>
              <Text style={styles.demoText}>{isMobile ? '演示' : '演示模式'}</Text>
            </View>
          )}
          <Pressable
            onPress={() => router.push('/profile')}
            style={styles.avatarBtn}
            hitSlop={6}
          >
            <ProfileAvatar
              nickname={profile?.nickname ?? nickname}
              avatarUrl={profile?.avatarUrl ?? session?.avatarUrl}
              size={isMobile ? 34 : 36}
            />
          </Pressable>
          <BackpackButton onPress={() => setBackpackOpen(true)} />
          <ShopButton onPress={openShop} />
          <CodexButton onPress={() => setCodexOpen(true)} />
          <LeaderboardButton onPress={() => router.push('/leaderboard')} />
          <SocialButton onPress={() => router.push('/social')} />
          <AdminDebugButton compact={isMobile} />
        </View>
      </View>

      {/* MapStage：顶栏以下全屏 */}
      <View style={styles.mapStage}>
        <WorldMapView
          regions={data?.regions ?? []}
          occupancy={data?.occupancy ?? {}}
          pondByRegion={pondByRegion}
          loading={loading}
          bannerHint={error && demoMode ? error : null}
          onEnterPond={(pondId, pondName) => {
            router.push({ pathname: '/pond/[id]', params: { id: pondId, name: pondName } });
          }}
        />
      </View>

      <BackpackModal
        visible={backpackOpen}
        items={items}
        coins={profile?.coins}
        loading={invLoading}
        onClose={() => setBackpackOpen(false)}
        onSell={async (fishId) => {
          try {
            const res = await socialApi.sellFish(playerId, fishId);
            setItems(res.items);
            setProfile((p) => (p ? { ...p, coins: res.totalCoins } : p));
          } catch (e) {
            window.alert(e instanceof Error ? e.message : '出售失败');
          }
        }}
        onShare={async (fishId) => {
          try {
            await socialApi.shareFish(playerId, nickname, fishId);
            window.alert('已分享到动态！');
          } catch (e) {
            window.alert(e instanceof Error ? e.message : '分享失败');
          }
        }}
      />
      <ShopModal
        visible={shopOpen}
        gear={shop.gear}
        coins={shop.coins || profile?.coins || 0}
        baits={shop.baits}
        tackles={shop.tackles}
        loading={shop.loading}
        error={shop.error}
        catalogOffline={shop.catalogOffline}
        onRetry={shop.refresh}
        onClose={() => setShopOpen(false)}
        onBuyBait={async (baitId, qty) => {
          try {
            const res = await shop.buyBait(baitId, qty);
            setProfile((p) => (p ? { ...p, coins: res.coins } : p));
          } catch (e) {
            window.alert(e instanceof Error ? e.message : '购买失败');
          }
        }}
        onBuyTackle={async (tackleId) => {
          try {
            const res = await shop.buyTackle(tackleId);
            setProfile((p) => (p ? { ...p, coins: res.coins } : p));
          } catch (e) {
            window.alert(e instanceof Error ? e.message : '购买失败');
          }
        }}
        onEquipBait={async (baitId) => {
          try {
            await shop.equipBait(baitId);
          } catch (e) {
            window.alert(e instanceof Error ? e.message : '装备失败');
          }
        }}
        onEquipTackle={async (tackleId) => {
          try {
            await shop.equipTackle(tackleId);
          } catch (e) {
            window.alert(e instanceof Error ? e.message : '装备失败');
          }
        }}
      />
      <CodexModal visible={codexOpen} onClose={() => setCodexOpen(false)} />
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  header: {
    zIndex: 100,
    elevation: 8,
    paddingTop: spacing.sm,
    paddingBottom: spacing.sm,
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
    backgroundColor: colors.bg,
  },
  headerLeft: {
    flex: 1,
    minWidth: 0,
  },
  headerRight: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    flexShrink: 0,
  },
  avatarBtn: {
    cursor: 'pointer',
  },
  title: {
    fontWeight: '800',
    color: colors.primaryDark,
  },
  subtitle: {
    fontSize: 13,
    color: colors.textMuted,
    marginTop: 2,
  },
  demoBadge: {
    backgroundColor: colors.warning,
    borderRadius: radius.sm,
    paddingHorizontal: 8,
    paddingVertical: 4,
  },
  demoText: {
    fontSize: 11,
    fontWeight: '600',
    color: '#5D4037',
  },
  mapStage: {
    flex: 1,
    minHeight: 0,
    zIndex: 1,
    position: 'relative',
  },
});
