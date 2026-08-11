import { useMemo, useState, useCallback, useEffect, useRef } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Pressable,
  ActivityIndicator,
} from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import {
  getPondById,
  isAnnounceQuality,
  getSpecies,
  getBait,
  isFishingActive,
} from '@fish-social/shared';
import {
  deriveTodayFishingBaseline,
  remainingDailyFishingMs,
  sessionAnchor,
} from '../../lib/fishingDuration';
import { BackpackButton } from '../../components/BackpackButton';
import { BackpackModal } from '../../components/BackpackModal';
import { CatchFishModal } from '../../components/CatchFishModal';
import { AppNoticeModal } from '../../components/AppNoticeModal';
import { PondSocialPanel } from '../../components/PondSocialPanel';
import { CodexModal } from '../../components/CodexModal';
import { ShopModal } from '../../components/ShopModal';
import { ShopButton } from '../../components/ShopButton';
import { CodexButton } from '../../components/CodexButton';
import { AdminDebugButton } from '../../components/AdminDebugButton';
import { ProfileAvatar } from '../../components/ProfileAvatar';
import { PondScene } from '../../components/PondScene';
import { AppScreen } from '../../components/AppScreen';
import { AppHeader } from '../../components/AppHeader';
import { usePondSocket } from '../../lib/usePondSocket';
import { formatDuration, formatFishingDuration, getNickname } from '../../lib/config';
import { getPlayerId } from '../../lib/playerId';
import { getStoredAdminKey } from '../../lib/adminApi';
import { useInventory } from '../../lib/useInventory';
import { useResponsive } from '../../lib/responsive';
import { colors, spacing, radius } from '../../lib/theme';
import { SocialButton } from '../../components/SocialButton';
import { useProfile } from '../../lib/useProfile';
import { socialApi } from '../../lib/socialApi';
import { useRequireAuth } from '../../lib/useRequireAuth';
import { useProfileModal } from '../../lib/useProfileModal';
import { useShop } from '../../lib/useShop';

export default function PondScreen() {
  const router = useRouter();
  const { ready, authenticated } = useRequireAuth();
  const { id, name } = useLocalSearchParams<{ id: string; name?: string }>();
  const pondId = id ?? '';
  const pond = getPondById(pondId);
  const nickname = useMemo(() => getNickname(), []);
  const playerId = useMemo(() => getPlayerId(), []);
  const [actionLoading, setActionLoading] = useState(false);
  const [backpackOpen, setBackpackOpen] = useState(false);
  const [shopOpen, setShopOpen] = useState(false);
  const [codexOpen, setCodexOpen] = useState(false);
  const [friendIds, setFriendIds] = useState<string[]>([]);
  const [pendingOutgoingIds, setPendingOutgoingIds] = useState<string[]>([]);
  const [notice, setNotice] = useState<{ title: string; message: string } | null>(null);
  const { pondSideBySide, isDesktop, isMobile, contentPadding } = useResponsive();
  const { items: inventory, setItems: setInventory, loading: invLoading } = useInventory(playerId);
  const { profile, setProfile } = useProfile(playerId, nickname);
  const shop = useShop(playerId);

  const showNotice = useCallback((title: string, message: string) => {
    setNotice({ title, message });
  }, []);

  useEffect(() => {
    socialApi
      .getFriends(playerId)
      .then(({ friends }) => setFriendIds(friends.map((f) => f.playerId)))
      .catch(() => setFriendIds([]));
    socialApi
      .getRequests(playerId)
      .then(({ outgoing }) => setPendingOutgoingIds(outgoing.map((r) => r.toPlayerId)))
      .catch(() => setPendingOutgoingIds([]));
  }, [playerId]);

  const refreshFriendState = useCallback(async () => {
    try {
      const [{ friends }, { outgoing }] = await Promise.all([
        socialApi.getFriends(playerId),
        socialApi.getRequests(playerId),
      ]);
      setFriendIds(friends.map((f) => f.playerId));
      setPendingOutgoingIds(outgoing.map((r) => r.toPlayerId));
    } catch {
      /* offline */
    }
  }, [playerId]);

  const handleInventoryChange = useCallback(
    (items: import('@fish-social/shared').FishInventoryItem[]) => {
      setInventory(items);
    },
    [setInventory],
  );

  const pondSocketCallbacks = useMemo(
    () => ({
      onGearUpdate: (gear: import('@fish-social/shared').PlayerGearState) => {
        shop.setGear(gear);
      },
      onBaitDepleted: (previousBaitId: import('@fish-social/shared').BaitId) => {
        const name = getBait(previousBaitId)?.name ?? previousBaitId;
        showNotice('鱼饵耗尽', `${name} 已用完，已自动切换为蚯蚓`);
      },
    }),
    [shop.setGear, showNotice],
  );

  const {
    connected,
    users,
    messages,
    myUserId,
    error,
    demoMode,
    snapshotReady,
    fishingPrompt,
    accepting,
    connectionProbe,
    leavePondWithReason,
    requestLeaveOnUnmount,
    rejoinPond,
    takeSpot,
    startFishing,
    stopFishing,
    sendChat,
    confirmFishingPrompt,
    ecology,
    floatTexts,
  } = usePondSocket(pondId, nickname, playerId, handleInventoryChange, pondSocketCallbacks);

  const { openProfile, profileModal } = useProfileModal({
    viewerPlayerId: playerId,
    viewerNickname: nickname,
    friendIds,
    pendingOutgoingIds,
    setFriendIds,
    setPendingOutgoingIds,
    refreshFriends: refreshFriendState,
    onEditProfile: () => {
      router.push('/profile');
    },
  });

  const handlePressPondUser = useCallback(
    (user: { playerId?: string; nickname: string; avatarUrl?: string }) => {
      if (!user.playerId) {
        showNotice('提示', '无法获取该用户信息');
        return;
      }
      openProfile({
        playerId: user.playerId,
        nickname: user.nickname,
        avatarUrl: user.avatarUrl,
      });
    },
    [openProfile],
  );

  const openShop = useCallback(async () => {
    await shop.refresh();
    setShopOpen(true);
  }, [shop]);

  const showConnectionProbe =
    (typeof __DEV__ !== 'undefined' && __DEV__) || Boolean(getStoredAdminKey());

  const leavingToMapRef = useRef(false);
  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  /** BUG-17：先导航后离塘；无历史时 replace 首页 */
  const handleLeaveToMap = useCallback(() => {
    leavingToMapRef.current = true;
    requestLeaveOnUnmount('navigation_back');
    const canBack =
      typeof (router as { canGoBack?: () => boolean }).canGoBack === 'function' &&
      (router as { canGoBack: () => boolean }).canGoBack();
    if (canBack) {
      router.back();
    } else {
      router.replace('/');
    }
    // back 在无历史时可能 no-op：短延迟后仍挂载则强制回地图
    setTimeout(() => {
      if (!mountedRef.current) return;
      router.replace('/');
    }, 80);
  }, [requestLeaveOnUnmount, router]);

  const handleLeaveToSocial = useCallback(() => {
    // BUG-06：切页不离塘
    router.push('/social');
  }, [router]);

  useEffect(() => {
    if (!ready || authenticated) return;
    leavePondWithReason('auth_redirect');
  }, [ready, authenticated, leavePondWithReason]);

  const handleConfirmFishingPrompt = useCallback(async () => {
    const res = await confirmFishingPrompt();
    if (!res.ok) {
      showNotice('领取失败', res.error ?? '未知错误');
      return;
    }
    if ('item' in res && res.item && isAnnounceQuality(res.item.quality)) {
      showNotice('史诗鱼获', '已自动生成钓鱼纪念照并发布到动态墙，快去社交中心看看吧！');
    }
  }, [confirmFishingPrompt]);

  const me = users.find((u) => u.id === myUserId);
  const phase = me?.fishingPhase;

  /** BUG-17：开始/收杆中间态，避免 seated↔baiting / stopping 闪烁 */
  const [startPending, setStartPending] = useState(false);
  const [stopPending, setStopPending] = useState(false);

  useEffect(() => {
    if (startPending && isFishingActive(phase) && phase !== 'stopping') {
      setStartPending(false);
    }
  }, [phase, startPending]);

  useEffect(() => {
    if (!startPending) return;
    const t = setTimeout(() => setStartPending(false), 3000);
    return () => clearTimeout(t);
  }, [startPending]);

  useEffect(() => {
    if (stopPending && !isFishingActive(phase)) {
      setStopPending(false);
    }
  }, [phase, stopPending]);

  useEffect(() => {
    if (!stopPending) return;
    const t = setTimeout(() => setStopPending(false), 3000);
    return () => clearTimeout(t);
  }, [stopPending]);

  /** BUG-17：死态自愈 — me 丢失且仍在鱼塘页时重新 join */
  useEffect(() => {
    if (demoMode || !connected || !pondId || leavingToMapRef.current) return;
    if (!myUserId) return;
    if (me) return;
    let cancelled = false;
    const timer = setTimeout(() => {
      if (cancelled || leavingToMapRef.current) return;
      void rejoinPond().then((res) => {
        if (cancelled || !res.ok || res.error === 'rejoin_throttled') return;
        showNotice('已重新加入鱼塘', '连接已恢复，可以继续钓鱼');
      });
    }, 700);
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [demoMode, connected, pondId, myUserId, me, rejoinPond, showNotice]);

  /**
   * BUG-14/19：开钓时冻结 todayFishingBaseMs，剩余 = 8h - (基线 + 本局墙钟)。
   * checkpoint 会抬高 DB base，本地基线不得随之上调（否则双计）；日切基线下降时跟齐。
   */
  const sessionStartRef = useRef<number | null>(null);
  const [todayBaseline, setTodayBaseline] = useState<number | null>(null);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!me) {
      sessionStartRef.current = null;
      setTodayBaseline(null);
      return;
    }
    const startedAt = sessionAnchor(me);
    const fishing =
      isFishingActive(me.fishingPhase) &&
      me.fishingPhase !== 'stopping' &&
      startedAt != null;
    if (!fishing) {
      sessionStartRef.current = null;
      setTodayBaseline(null);
      return;
    }
    const derived = deriveTodayFishingBaseline(me);
    if (sessionStartRef.current !== startedAt) {
      sessionStartRef.current = startedAt;
      setTodayBaseline(derived);
      setNow(Date.now());
      return;
    }
    // 仅允许基线下降（日切），禁止因 checkpoint 上调
    setTodayBaseline((prev) =>
      prev != null && derived + 1500 < prev ? derived : prev,
    );
  }, [
    me?.id,
    me?.sessionStartedAt,
    me?.fishingStartedAt,
    me?.fishingPhase,
    me?.todayFishingBaseMs,
  ]);

  useEffect(() => {
    if (!isFishingActive(me?.fishingPhase) || me?.fishingPhase === 'stopping') return;
    setNow(Date.now());
    const tick = setInterval(() => setNow(Date.now()), 250);
    return () => clearInterval(tick);
  }, [me?.fishingPhase, me?.id]);

  const remainingMs = remainingDailyFishingMs(me, now, todayBaseline);
  // join ack / enrich 已带回额度时，未选钓点也可先显示（不必等完整 snapshot）
  const quotaReady =
    !!me &&
    (snapshotReady ||
      typeof me.todayFishingBaseMs === 'number' ||
      typeof me.todayRemainingMs === 'number');

  const handleTakeSpot = async (spotId: string) => {
    if (!me) {
      showNotice('提示', '正在加入鱼塘，请稍候');
      return;
    }
    if (isFishingActive(phase) || startPending || stopPending) {
      showNotice('提示', '请先收起鱼竿');
      return;
    }
    const occupant = users.find((u) => u.spotId === spotId && u.id !== me.id);
    if (occupant && !occupant.isBot) {
      showNotice('钓点已占用', '请选择其他空闲钓点');
      return;
    }
    const res = await takeSpot(spotId);
    if (!res.ok) showNotice('无法占用钓点', res.error ?? '该钓点暂不可用');
  };

  const handleStartFishing = async () => {
    if (!pond) return;
    if (!me) {
      const r = await rejoinPond();
      if (r.ok) {
        showNotice('已重新加入鱼塘', '请再点一次开始钓鱼');
      } else {
        showNotice('无法开始钓鱼', '请先加入鱼塘');
      }
      return;
    }
    if (!me.spotId) {
      showNotice('请先选择钓点', '点击鱼塘中的空闲钓点落座后再开始钓鱼');
      return;
    }
    setStartPending(true);
    setActionLoading(true);
    const res = await startFishing();
    setActionLoading(false);
    if (!res.ok) {
      setStartPending(false);
      const needRejoin = (res.error ?? '').includes('请先加入鱼塘');
      if (needRejoin) {
        const r = await rejoinPond();
        if (r.ok) {
          showNotice('已重新加入鱼塘', '请再点一次开始钓鱼');
        } else {
          showNotice('无法开始钓鱼', res.error ?? '未知错误');
        }
      } else {
        showNotice('无法开始钓鱼', res.error ?? '未知错误');
      }
    }
  };

  const handleStopFishing = async () => {
    setStopPending(true);
    setActionLoading(true);
    const res = await stopFishing();
    setActionLoading(false);
    if (!res.ok) {
      setStopPending(false);
      showNotice('操作失败', res.error ?? '未知错误');
    }
  };

  const fishingBtnBusy = actionLoading || startPending || stopPending || phase === 'stopping';
  // 开钓 pending 且尚未进入活跃相位：显示开钓中（不回落「开始钓鱼」）
  const showStarting = startPending && !isFishingActive(phase);
  const showStopping = stopPending || phase === 'stopping';

  if (!ready || !authenticated) return null;

  if (!pond) {
    return (
      <AppScreen backgroundColor={colors.bgCool}>
        <AppHeader title="鱼塘" onBack={handleLeaveToMap} backLabel="← 地图" />
        <View style={styles.notFound}>
          <Text style={styles.error}>鱼塘不存在</Text>
          <Pressable onPress={handleLeaveToMap}>
            <Text style={styles.backLink}>返回地图</Text>
          </Pressable>
        </View>
      </AppScreen>
    );
  }

  const mainContent = (
    <>
      <View style={pondSideBySide ? styles.pondColumn : styles.pondColumnMobile}>
        <PondScene
          pondId={pond.id}
          users={users}
          spots={pond.spots}
          myUserId={myUserId}
          onPressSpot={handleTakeSpot}
          floatTexts={floatTexts}
          onPressUser={(user) => handlePressPondUser(user)}
        />
        <View style={[styles.actionBar, isDesktop && styles.actionBarDesktop, isMobile && styles.actionBarMobile]}>
          <View style={styles.actionMeta}>
            <Text style={styles.remaining} numberOfLines={1}>
              今日剩余：{quotaReady ? formatFishingDuration(remainingMs) : '加载中...'}
            </Text>
            {ecology && (
              <Text style={styles.ecology} numberOfLines={2}>
                {ecology.depleted
                  ? `鱼塘恢复中${ecology.depletedUntil ? ` · ${formatDuration(Math.max(0, ecology.depletedUntil - Date.now()))}` : ''}`
                  : `鱼群 ${ecology.fishCount}/${ecology.maxPopulation} · 常见${ecology.commonSpecies.map((id) => getSpecies(id).name).join('、')}`}
              </Text>
            )}
          </View>
          {!snapshotReady ? (
            <Pressable style={[styles.actionBtn, styles.startBtn]} disabled>
              <Text style={styles.actionBtnText}>加载中…</Text>
            </Pressable>
          ) : showStopping ? (
            <Pressable style={[styles.actionBtn, styles.stopBtn]} disabled>
              <Text style={styles.actionBtnText}>收杆中…</Text>
            </Pressable>
          ) : showStarting ? (
            <Pressable style={[styles.actionBtn, styles.startBtn]} disabled>
              <Text style={styles.actionBtnText}>开钓中…</Text>
            </Pressable>
          ) : isFishingActive(phase) ? (
            <Pressable
              style={[styles.actionBtn, styles.stopBtn]}
              onPress={handleStopFishing}
              disabled={fishingBtnBusy}
            >
              <Text style={styles.actionBtnText}>收起鱼竿</Text>
            </Pressable>
          ) : (
            <Pressable
              style={[styles.actionBtn, styles.startBtn]}
              onPress={handleStartFishing}
              disabled={fishingBtnBusy || !quotaReady || remainingMs <= 0 || !me?.spotId}
            >
              <Text style={styles.actionBtnText}>
                {!quotaReady
                  ? '加载中…'
                  : remainingMs <= 0
                    ? '今日已满'
                    : !me?.spotId
                      ? '请先选钓点'
                      : '开始钓鱼'}
              </Text>
            </Pressable>
          )}
        </View>
      </View>
      <PondSocialPanel
        users={users}
        myUserId={myUserId}
        messages={messages}
        onSend={sendChat}
        expanded={pondSideBySide}
        onPressUser={handlePressPondUser}
      />
    </>
  );

  return (
    <AppScreen backgroundColor={colors.bgCool}>
      <AppHeader
        title={name ?? pond.name}
        subtitle={`${users.length} 人在线 · ${connected || demoMode ? (demoMode ? '演示' : '已连接') : '连接中...'}`}
        onBack={handleLeaveToMap}
        backLabel="← 地图"
        right={
          <>
            {demoMode && (
              <View style={styles.demoBadge}>
                <Text style={styles.demoText}>演示</Text>
              </View>
            )}
            <Pressable
              onPress={() =>
                openProfile({
                  playerId,
                  nickname: profile?.nickname ?? nickname,
                  avatarUrl: profile?.avatarUrl,
                })
              }
              hitSlop={6}
              style={styles.headerAvatarBtn}
            >
              <ProfileAvatar
                nickname={profile?.nickname ?? nickname}
                avatarUrl={profile?.avatarUrl}
                size={32}
              />
            </Pressable>
            <BackpackButton onPress={() => setBackpackOpen(true)} />
            <ShopButton onPress={openShop} />
            <CodexButton onPress={() => setCodexOpen(true)} />
            <AdminDebugButton compact={isMobile} />
            <SocialButton onPress={handleLeaveToSocial} />
          </>
        }
      />

      {showConnectionProbe && (
        <Text style={[styles.connectionProbe, { paddingHorizontal: contentPadding }]}>
          连接: {demoMode ? '演示模式' : (connectionProbe.connected ? '已连接' : '断开')}
          {' | '}阶段: {connectionProbe.fishingPhase ?? '-'}
          {' | '}spot: {connectionProbe.spotId ?? '-'}
          {connectionProbe.lastDisconnectAt
            ? ` | 最近断开: ${Math.round((Date.now() - connectionProbe.lastDisconnectAt) / 1000)}s前(${connectionProbe.lastDisconnectReason ?? '?'})`
            : ''}
        </Text>
      )}

      {(error || !connected) && !demoMode && (
        <View style={styles.banner}>
          {!connected ? <ActivityIndicator color="#fff" size="small" /> : null}
          <Text style={styles.bannerText}>{error ?? '正在连接鱼塘...'}</Text>
        </View>
      )}

      {demoMode && (
        <Text style={[styles.demoHint, { paddingHorizontal: contentPadding }]}>
          {error ?? '演示模式：服务端未连接，部分功能为本地模拟数据'}
        </Text>
      )}

      <View style={pondSideBySide ? styles.layoutDesktop : styles.layoutMobile}>
        {mainContent}
      </View>

        <BackpackModal
          visible={backpackOpen}
          items={inventory}
          coins={profile?.coins ?? shop.coins}
          loading={invLoading}
          onClose={() => setBackpackOpen(false)}
          onSell={async (fishId) => {
            try {
              const res = await socialApi.sellFish(playerId, fishId);
              setInventory(res.items);
              setProfile((p) => (p ? { ...p, coins: res.totalCoins } : p));
            } catch (e) {
              showNotice('出售失败', e instanceof Error ? e.message : '');
            }
          }}
          onShare={async (fishId) => {
            try {
              await socialApi.shareFish(playerId, nickname, fishId);
              showNotice('分享成功', '已发布到动态');
            } catch (e) {
              showNotice('分享失败', e instanceof Error ? e.message : '');
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
              showNotice('购买失败', e instanceof Error ? e.message : '');
            }
          }}
          onBuyTackle={async (tackleId) => {
            try {
              const res = await shop.buyTackle(tackleId);
              setProfile((p) => (p ? { ...p, coins: res.coins } : p));
            } catch (e) {
              showNotice('购买失败', e instanceof Error ? e.message : '');
            }
          }}
          onEquipBait={async (baitId) => {
            try {
              await shop.equipBait(baitId);
            } catch (e) {
              showNotice('装备失败', e instanceof Error ? e.message : '');
            }
          }}
          onEquipTackle={async (tackleId) => {
            try {
              await shop.equipTackle(tackleId);
            } catch (e) {
              showNotice('装备失败', e instanceof Error ? e.message : '');
            }
          }}
        />
        <CodexModal visible={codexOpen} onClose={() => setCodexOpen(false)} />
        <CatchFishModal
          prompt={fishingPrompt}
          loading={accepting}
          onConfirm={handleConfirmFishingPrompt}
        />
        <AppNoticeModal
          visible={!!notice}
          title={notice?.title ?? ''}
          message={notice?.message ?? ''}
          onConfirm={() => setNotice(null)}
        />
        {profileModal}
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#E8F4F8',
    minHeight: '100vh' as unknown as number,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  headerDesktop: {
    paddingHorizontal: 24,
    paddingTop: 16,
  },
  backBtn: {
    padding: 8,
    cursor: 'pointer',
  },
  backText: {
    color: '#4A90A4',
    fontSize: 15,
    fontWeight: '600',
  },
  headerCenter: {
    flex: 1,
    alignItems: 'center',
  },
  pondName: {
    fontSize: 18,
    fontWeight: '700',
    color: '#2C5F6F',
  },
  meta: {
    fontSize: 12,
    color: '#888',
  },
  demoBadge: {
    backgroundColor: '#FFB74D',
    borderRadius: 8,
    paddingHorizontal: 8,
    paddingVertical: 4,
  },
  demoText: {
    fontSize: 11,
    fontWeight: '600',
    color: '#5D4037',
  },
  demoHint: {
    textAlign: 'center',
    color: '#888',
    fontSize: 12,
    paddingBottom: 4,
  },
  connectionProbe: {
    fontSize: 10,
    color: '#666',
    paddingVertical: 4,
    textAlign: 'center',
  },
  banner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    backgroundColor: '#E57373',
    paddingVertical: 6,
  },
  bannerText: {
    color: '#fff',
    fontSize: 12,
  },
  layoutMobile: {
    flex: 1,
    minHeight: 0,
  },
  layoutDesktop: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'stretch',
    paddingHorizontal: 12,
    paddingBottom: 12,
    minHeight: 520,
  },
  pondColumn: {
    flex: 1.4,
    minWidth: 0,
  },
  pondColumnMobile: {
    flexGrow: 1,
    flexShrink: 1,
    minHeight: 280,
  },
  actionBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 10,
    backgroundColor: '#fff',
    marginHorizontal: 8,
    borderRadius: 12,
    marginBottom: 4,
  },
  actionBarDesktop: {
    marginHorizontal: 12,
    marginTop: 8,
  },
  actionBarMobile: {
    flexWrap: 'wrap',
    gap: spacing.sm,
    marginHorizontal: spacing.sm,
    paddingHorizontal: spacing.md,
  },
  notFound: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: spacing.lg,
  },
  remaining: {
    fontSize: 12,
    color: '#666',
  },
  ecology: {
    fontSize: 11,
    color: '#4A90A4',
    marginTop: 2,
    maxWidth: 220,
  },
  actionMeta: {
    flex: 1,
    minWidth: 0,
    marginRight: 8,
  },
  actionBtn: {
    borderRadius: radius.pill,
    paddingHorizontal: 20,
    paddingVertical: 10,
    minHeight: 44,
    justifyContent: 'center',
    cursor: 'pointer',
  },
  startBtn: {
    backgroundColor: colors.primary,
  },
  stopBtn: {
    backgroundColor: '#888',
  },
  actionBtnText: {
    color: '#fff',
    fontWeight: '700',
    fontSize: 14,
  },
  error: {
    textAlign: 'center',
    marginTop: 40,
    color: '#c00',
  },
  backLink: {
    textAlign: 'center',
    color: '#4A90A4',
    marginTop: 12,
  },
  headerAvatarBtn: {
    cursor: 'pointer',
  },
});
