import { useCallback, useEffect, useState } from 'react';
import {
  View,
  Text,
  Pressable,
  StyleSheet,
  ActivityIndicator,
  FlatList,
  ScrollView,
} from 'react-native';
import {
  PONDS,
  formatFishSize,
  getSpecies,
  type FishSpeciesId,
  type LeaderboardBoardType,
  type LeaderboardEntry,
  type LeaderboardMyRank,
} from '@fish-social/shared';
import { ProfileAvatar } from './ProfileAvatar';
import { socialApi } from '../lib/socialApi';
import { useResponsive } from '../lib/responsive';
import { colors, radius, spacing } from '../lib/theme';

type SubTab = 'daily' | 'weekly' | 'pond' | 'rare';

interface Props {
  viewerPlayerId: string;
  onPressUser: (user: { playerId: string; nickname: string; avatarUrl?: string }) => void;
}

const MEDALS = ['🥇', '🥈', '🥉'] as const;

function boardTypeOf(sub: SubTab): LeaderboardBoardType {
  if (sub === 'daily') return 'daily_biggest';
  if (sub === 'weekly') return 'weekly_king';
  if (sub === 'pond') return 'pond';
  return 'rare';
}

function formatValue(sub: SubTab, entry: LeaderboardEntry): string {
  if (sub === 'daily') {
    const speciesId = entry.extra?.speciesId as FishSpeciesId | undefined;
    const sp = speciesId ? getSpecies(speciesId) : null;
    const size = entry.extra?.sizeM ?? entry.value;
    return `${sp?.icon ?? '🐟'} ${formatFishSize(size)}`;
  }
  if (sub === 'weekly') {
    const count = entry.extra?.catchCount;
    return `💰 ${Math.round(entry.value)}${count != null ? ` · ${count} 条` : ''}`;
  }
  if (sub === 'pond') {
    const max = entry.extra?.sizeM;
    return `${Math.round(entry.value)} 条${max != null ? ` · 最大 ${formatFishSize(max)}` : ''}`;
  }
  const max = entry.extra?.sizeM;
  return `史诗+ ${Math.round(entry.value)} 条${max != null ? ` · ${formatFishSize(max)}` : ''}`;
}

function formatExtraLine(sub: SubTab, entry: LeaderboardEntry): string | null {
  if (sub === 'daily') {
    const pond = entry.extra?.pondId
      ? PONDS.find((p) => p.id === entry.extra!.pondId)?.name
      : null;
    const when = entry.extra?.caughtAt
      ? new Date(entry.extra.caughtAt).toLocaleTimeString()
      : null;
    return [pond, when].filter(Boolean).join(' · ') || null;
  }
  return null;
}

export function LeaderboardPanel({ viewerPlayerId, onPressUser }: Props) {
  const { isMobile, contentPadding } = useResponsive();
  const [sub, setSub] = useState<SubTab>('daily');
  const [pondId, setPondId] = useState(PONDS[0]?.id ?? 'pond-calm');
  const [entries, setEntries] = useState<LeaderboardEntry[]>([]);
  const [myRank, setMyRank] = useState<LeaderboardMyRank | null>(null);
  const [periodKey, setPeriodKey] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      let list: LeaderboardEntry[] = [];
      let pk = '';
      if (sub === 'daily') {
        const res = await socialApi.getLeaderboardDailyBiggest({ limit: 20 });
        list = res.entries;
        pk = res.periodKey;
      } else if (sub === 'weekly') {
        const res = await socialApi.getLeaderboardWeeklyKing({ limit: 20 });
        list = res.entries;
        pk = res.periodKey;
      } else if (sub === 'pond') {
        const res = await socialApi.getLeaderboardPond(pondId, { limit: 10 });
        list = res.entries;
        pk = res.periodKey;
      } else {
        const res = await socialApi.getLeaderboardRare({ limit: 20 });
        list = res.entries;
        pk = res.periodKey;
      }
      setEntries(list);
      setPeriodKey(pk);

      try {
        const mine = await socialApi.getMyRank(boardTypeOf(sub), {
          pondId: sub === 'pond' ? pondId : undefined,
        });
        setMyRank(mine);
      } catch {
        setMyRank(null);
      }
    } catch (e) {
      setEntries([]);
      setMyRank(null);
      setError(e instanceof Error ? e.message : '加载失败');
    } finally {
      setLoading(false);
    }
  }, [sub, pondId]);

  useEffect(() => {
    load();
  }, [load]);

  const subTabs: { id: SubTab; label: string }[] = [
    { id: 'daily', label: '今日最大鱼' },
    { id: 'weekly', label: '本周钓王' },
    { id: 'pond', label: '钓场' },
    { id: 'rare', label: '稀有' },
  ];

  const renderRow = ({ item }: { item: LeaderboardEntry }) => {
    const isMe = item.playerId === viewerPlayerId;
    const medal = item.rank <= 3 ? MEDALS[item.rank - 1] : null;
    const extra = formatExtraLine(sub, item);

    return (
      <Pressable
        style={[styles.row, isMe && styles.rowMe, item.rank <= 3 && styles.rowTop]}
        onPress={() =>
          onPressUser({
            playerId: item.playerId,
            nickname: item.nickname,
            avatarUrl: item.avatarUrl,
          })
        }
      >
        <Text style={[styles.rank, item.rank <= 3 && styles.rankTop]}>
          {medal ?? item.rank}
        </Text>
        <ProfileAvatar nickname={item.nickname} avatarUrl={item.avatarUrl} size={36} />
        <View style={styles.rowMain}>
          <Text style={[styles.nick, isMe && styles.nickMe]} numberOfLines={1}>
            {item.nickname}
            {isMe ? '（我）' : ''}
          </Text>
          <Text style={styles.value}>{formatValue(sub, item)}</Text>
          {extra ? <Text style={styles.extra}>{extra}</Text> : null}
        </View>
      </Pressable>
    );
  };

  return (
    <View style={styles.wrap}>
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        style={styles.subTabs}
        contentContainerStyle={[styles.subTabsContent, { paddingHorizontal: contentPadding }]}
      >
        {subTabs.map((t) => (
          <Pressable
            key={t.id}
            style={[styles.subTab, sub === t.id && styles.subTabActive, isMobile && styles.subTabMobile]}
            onPress={() => setSub(t.id)}
          >
            <Text style={[styles.subTabText, sub === t.id && styles.subTabTextActive]}>
              {isMobile && t.id === 'daily' ? '今日' : isMobile && t.id === 'weekly' ? '钓王' : t.label}
            </Text>
          </Pressable>
        ))}
      </ScrollView>

      {sub === 'pond' ? (
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={[styles.pondPicker, { paddingHorizontal: contentPadding }]}
        >
          {PONDS.map((p) => (
            <Pressable
              key={p.id}
              style={[styles.pondChip, pondId === p.id && styles.pondChipActive]}
              onPress={() => setPondId(p.id)}
            >
              <Text style={[styles.pondChipText, pondId === p.id && styles.pondChipTextActive]}>
                {p.name}
              </Text>
            </Pressable>
          ))}
        </ScrollView>
      ) : null}

      {periodKey ? (
        <Text style={[styles.period, { paddingHorizontal: contentPadding }]}>周期 {periodKey}</Text>
      ) : null}

      {loading ? (
        <ActivityIndicator style={styles.loader} color={colors.primary} />
      ) : error ? (
        <View style={styles.emptyWrap}>
          <Text style={styles.error}>{error}</Text>
          <Pressable style={styles.retry} onPress={load}>
            <Text style={styles.retryText}>重试</Text>
          </Pressable>
        </View>
      ) : (
        <FlatList
          style={styles.list}
          data={entries}
          keyExtractor={(e) => `${e.rank}-${e.playerId}`}
          contentContainerStyle={[styles.listContent, { paddingHorizontal: contentPadding }]}
          ListEmptyComponent={<Text style={styles.empty}>暂无上榜数据</Text>}
          renderItem={renderRow}
          ListFooterComponent={
            myRank ? (
              <View style={styles.myRankBox}>
                <Text style={styles.myRankTitle}>我的排名</Text>
                <Text style={styles.myRankLine}>
                  {myRank.rank != null
                    ? `第 ${myRank.rank} 名`
                    : '未进入 Top 榜'}
                  {' · '}
                  {sub === 'daily' || (myRank.entry?.extra?.sizeM != null && sub !== 'weekly')
                    ? formatFishSize(myRank.entry?.extra?.sizeM ?? myRank.value)
                    : sub === 'weekly'
                      ? `价值 ${Math.round(myRank.value)}`
                      : `${Math.round(myRank.value)}`}
                </Text>
              </View>
            ) : null
          }
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { flex: 1, minHeight: 0 },
  subTabs: { flexGrow: 0, marginTop: spacing.sm },
  subTabsContent: { alignItems: 'center', gap: spacing.sm, paddingBottom: spacing.sm },
  subTab: {
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: radius.pill,
    backgroundColor: '#e8e8e8',
    cursor: 'pointer',
  },
  subTabMobile: { paddingHorizontal: 10 },
  subTabActive: { backgroundColor: colors.primary },
  subTabText: { fontSize: 13, color: colors.textSecondary },
  subTabTextActive: { color: '#fff', fontWeight: '600' },
  pondPicker: { gap: 8, paddingBottom: spacing.sm },
  pondChip: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: radius.pill,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    cursor: 'pointer',
  },
  pondChipActive: { borderColor: colors.primary, backgroundColor: colors.primaryLight },
  pondChipText: { fontSize: 12, color: colors.textSecondary },
  pondChipTextActive: { color: colors.primaryDark, fontWeight: '700' },
  period: { fontSize: 11, color: colors.textMuted, marginBottom: 4 },
  loader: { marginTop: 40 },
  list: { flex: 1 },
  listContent: { paddingBottom: 40, paddingTop: 4 },
  emptyWrap: { alignItems: 'center', marginTop: 40, gap: 12 },
  empty: { textAlign: 'center', color: '#aaa', marginTop: 40 },
  error: { color: '#c62828', textAlign: 'center' },
  retry: {
    backgroundColor: colors.primary,
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: radius.md,
    cursor: 'pointer',
  },
  retryText: { color: '#fff', fontWeight: '600' },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    backgroundColor: colors.surface,
    padding: 12,
    borderRadius: radius.md,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: colors.borderLight,
    cursor: 'pointer',
  },
  rowMe: {
    backgroundColor: '#E8F4F8',
    borderColor: colors.primary,
  },
  rowTop: {
    borderColor: '#E8C547',
  },
  rank: {
    width: 28,
    textAlign: 'center',
    fontSize: 14,
    fontWeight: '700',
    color: colors.textSecondary,
  },
  rankTop: { fontSize: 18 },
  rowMain: { flex: 1, minWidth: 0 },
  nick: { fontWeight: '700', color: colors.primaryDark, fontSize: 14 },
  nickMe: { color: colors.primary },
  value: { fontSize: 13, color: colors.text, marginTop: 2 },
  extra: { fontSize: 11, color: colors.textMuted, marginTop: 2 },
  myRankBox: {
    marginTop: 12,
    padding: 14,
    borderRadius: radius.md,
    backgroundColor: colors.primaryLight,
    borderWidth: 1,
    borderColor: colors.primary,
  },
  myRankTitle: { fontWeight: '800', color: colors.primaryDark, marginBottom: 4 },
  myRankLine: { fontSize: 13, color: colors.text },
});
