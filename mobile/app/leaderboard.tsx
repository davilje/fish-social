import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  View,
  Text,
  Pressable,
  StyleSheet,
  ActivityIndicator,
  FlatList,
} from 'react-native';
import { useRouter } from 'expo-router';
import {
  formatFishSize,
  getSpecies,
  type FishSpeciesId,
  type LeaderboardEntry,
  type LeaderboardMyRank,
} from '@fish-social/shared';
import { ProfileAvatar } from '../components/ProfileAvatar';
import { AppScreen } from '../components/AppScreen';
import { socialApi } from '../lib/socialApi';
import { useProfileModal } from '../lib/useProfileModal';
import { useResponsive } from '../lib/responsive';
import { getNickname } from '../lib/config';
import { getPlayerId } from '../lib/playerId';
import { colors, radius, spacing } from '../lib/theme';

type BoardTab = 'daily' | 'weekly';

const MEDALS = ['🥇', '🥈', '🥉'] as const;
const LIST_MAX = 50;

type RankSlot = { rank: number; entry: LeaderboardEntry | null };

const POND_NAMES: Record<string, string> = {
  'pond-calm': '静心湖',
  'pond-mist': '云雾塘',
  'pond-sunset': '夕阳湾',
  'pond-bamboo': '竹林池',
  'pond-reed': '芦苇荡',
  'pond-crystal': '晶石潭',
  'pond-lotus': '荷香池',
  'pond-mirror': '镜面湖',
  'pond-willow': '柳荫湾',
  'pond-stone': '叠石矶',
  'pond-spring': '清泉眼',
  'pond-dusk': '暮色泊',
  'pond-pine': '松风潭',
  'pond-coral': '珊瑚浅',
  'pond-moon': '月影池',
  'pond-fern': '蕨影泽',
  'pond-ridge': '岭下塘',
  'pond-harbor': '渔港湾',
  'pond-orchid': '兰汀',
  'pond-frost': '霜华淀',
};

function getPondName(pondId: string | undefined | null): string {
  if (!pondId) return '—';
  return POND_NAMES[pondId] ?? pondId;
}

function formatEntryValue(entry: LeaderboardEntry): string {
  const speciesId = entry.extra?.speciesId as FishSpeciesId | undefined;
  const sp = speciesId ? getSpecies(speciesId) : null;
  const size = entry.extra?.sizeM ?? entry.value;
  return (sp?.icon ?? '🐟') + ' ' + formatFishSize(size);
}

function formatEntryPond(entry: LeaderboardEntry): string {
  return getPondName(entry.extra?.pondId);
}

function buildSlots(entries: LeaderboardEntry[]): { podium: (LeaderboardEntry | null)[]; rest: RankSlot[] } {
  const byRank = new Map(entries.map((e) => [e.rank, e]));
  const podium: (LeaderboardEntry | null)[] = [
    byRank.get(2) ?? null,
    byRank.get(1) ?? null,
    byRank.get(3) ?? null,
  ];
  const rest: RankSlot[] = [];
  for (let rank = 4; rank <= LIST_MAX; rank++) {
    rest.push({ rank, entry: byRank.get(rank) ?? null });
  }
  return { podium, rest };
}

export default function LeaderboardScreen() {
  const router = useRouter();
  const playerId = useMemo(() => getPlayerId(), []);
  const nickname = useMemo(() => getNickname(), []);
  const { isMobile, contentPadding } = useResponsive();
  const [tab, setTab] = useState<BoardTab>('daily');
  const [entries, setEntries] = useState<LeaderboardEntry[]>([]);
  const [myRank, setMyRank] = useState<LeaderboardMyRank | null>(null);
  const [periodKey, setPeriodKey] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [friendIds, setFriendIds] = useState<string[]>([]);
  const [pendingOutgoingIds, setPendingOutgoingIds] = useState<string[]>([]);

  const { openProfile, profileModal } = useProfileModal({
    viewerPlayerId: playerId,
    viewerNickname: nickname,
    friendIds,
    pendingOutgoingIds,
    setFriendIds,
    setPendingOutgoingIds,
    refreshFriends: () => {},
    onEditProfile: () => router.push('/profile'),
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      let list: LeaderboardEntry[] = [];
      let pk = '';
      if (tab === 'daily') {
        const res = await socialApi.getLeaderboardDailyBiggest({ limit: LIST_MAX });
        list = res.entries;
        pk = res.periodKey;
      } else {
        const res = await socialApi.getLeaderboardWeeklyKing({ limit: LIST_MAX });
        list = res.entries;
        pk = res.periodKey;
      }
      setEntries(list);
      setPeriodKey(pk);
      try {
        const mine = await socialApi.getMyRank(tab === 'daily' ? 'daily_biggest' : 'weekly_king');
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
  }, [tab]);

  useEffect(() => {
    load();
  }, [load]);

  const { podium, rest } = useMemo(() => buildSlots(entries), [entries]);

  const renderPodiumEntry = (entry: LeaderboardEntry | null, rank: 1 | 2 | 3, height: number) => {
    if (!entry) {
      return (
        <View style={[styles.podiumSlot, styles.podiumEmpty, { height }]}>
          <Text style={styles.podiumMedal}>{MEDALS[rank - 1]}</Text>
          <View style={styles.emptyAvatar} />
          <Text style={styles.emptyLabel}>虚位以待</Text>
        </View>
      );
    }
    const isBot = entry.playerId.startsWith('bot-');
    return (
      <Pressable
        style={[styles.podiumSlot, { height }]}
        onPress={() =>
          openProfile({
            playerId: entry.playerId,
            nickname: entry.nickname,
            avatarUrl: entry.avatarUrl,
          })
        }
      >
        <Text style={styles.podiumMedal}>{MEDALS[rank - 1]}</Text>
        <ProfileAvatar
          nickname={entry.nickname}
          avatarUrl={entry.avatarUrl}
          size={rank === 1 ? 48 : 40}
        />
        <Text style={styles.podiumNick} numberOfLines={1}>
          {entry.nickname}
          {isBot ? ' ·机' : ''}
        </Text>
        <Text style={styles.podiumFish} numberOfLines={1}>
          {formatEntryValue(entry)}
        </Text>
        <Text style={styles.podiumPond} numberOfLines={1}>
          {formatEntryPond(entry)}
        </Text>
      </Pressable>
    );
  };

  const renderRow = ({ item }: { item: RankSlot }) => {
    if (!item.entry) {
      return (
        <View style={[styles.row, styles.rowEmpty]}>
          <Text style={styles.rowRank}>{item.rank}</Text>
          <View style={styles.emptyAvatarSm} />
          <Text style={styles.emptyRowText}>虚位以待</Text>
        </View>
      );
    }
    const entry = item.entry;
    const isMe = entry.playerId === playerId;
    const isBot = entry.playerId.startsWith('bot-');
    return (
      <Pressable
        style={[styles.row, isMe && styles.rowMe]}
        onPress={() =>
          openProfile({
            playerId: entry.playerId,
            nickname: entry.nickname,
            avatarUrl: entry.avatarUrl,
          })
        }
      >
        <Text style={styles.rowRank}>{entry.rank}</Text>
        <ProfileAvatar nickname={entry.nickname} avatarUrl={entry.avatarUrl} size={36} />
        <View style={styles.rowMain}>
          <Text style={[styles.rowNick, isMe && styles.rowNickMe]} numberOfLines={1}>
            {entry.nickname}
            {isMe ? '（我）' : ''}
            {isBot ? ' ·机' : ''}
          </Text>
          <View style={styles.rowDetails}>
            <Text style={styles.rowFish}>{formatEntryValue(entry)}</Text>
            <Text style={styles.rowPond}>{formatEntryPond(entry)}</Text>
          </View>
        </View>
      </Pressable>
    );
  };

  return (
    <AppScreen>
      <View style={styles.container}>
        <View style={[styles.topBar, { paddingHorizontal: contentPadding }]}>
          <Pressable onPress={() => router.back()} hitSlop={8}>
            <Text style={styles.back}>{isMobile ? '←' : '← 返回'}</Text>
          </Pressable>
          <Text style={[styles.title, isMobile && styles.titleMobile]}>排行榜</Text>
          <View style={styles.topBarRight} />
        </View>

        <View style={[styles.segmentRow, { paddingHorizontal: contentPadding }]}>
          <Pressable
            style={[styles.segment, tab === 'daily' && styles.segmentActive]}
            onPress={() => setTab('daily')}
          >
            <Text style={[styles.segmentText, tab === 'daily' && styles.segmentTextActive]}>
              每日排行
            </Text>
          </Pressable>
          <Pressable
            style={[styles.segment, tab === 'weekly' && styles.segmentActive]}
            onPress={() => setTab('weekly')}
          >
            <Text style={[styles.segmentText, tab === 'weekly' && styles.segmentTextActive]}>
              每周排行
            </Text>
          </Pressable>
        </View>

        {periodKey ? (
          <Text style={[styles.period, { paddingHorizontal: contentPadding }]}>
            周期 {periodKey}
          </Text>
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
            data={rest}
            keyExtractor={(e) => `rank-${e.rank}`}
            contentContainerStyle={[styles.listContent, { paddingHorizontal: contentPadding }]}
            ListHeaderComponent={
              <View style={styles.podiumWrap}>
                <View style={styles.podiumRow}>
                  <View style={styles.podiumSide}>{renderPodiumEntry(podium[0], 2, 120)}</View>
                  <View style={styles.podiumCenter}>{renderPodiumEntry(podium[1], 1, 150)}</View>
                  <View style={styles.podiumSide}>{renderPodiumEntry(podium[2], 3, 100)}</View>
                </View>
              </View>
            }
            renderItem={renderRow}
            ListFooterComponent={
              myRank ? (
                <View style={styles.myRankBox}>
                  <Text style={styles.myRankTitle}>我的排名</Text>
                  <Text style={styles.myRankLine}>
                    {myRank.rank != null ? `第 ${myRank.rank} 名` : '未进入 Top 榜'}
                    {' · '}
                    {myRank.entry?.extra?.sizeM != null
                      ? formatFishSize(myRank.entry.extra.sizeM)
                      : formatFishSize(myRank.value)}
                  </Text>
                </View>
              ) : null
            }
          />
        )}
      </View>

      {profileModal}
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, minHeight: 0 },
  topBar: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingTop: spacing.sm,
    paddingBottom: spacing.sm,
    gap: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
    backgroundColor: colors.bg,
  },
  back: { color: colors.primary, fontWeight: '600', fontSize: 15 },
  title: { flex: 1, fontSize: 20, fontWeight: '800', color: colors.primaryDark },
  titleMobile: { fontSize: 18 },
  topBarRight: { width: 40 },
  segmentRow: {
    flexDirection: 'row',
    gap: spacing.sm,
    paddingVertical: spacing.sm,
    backgroundColor: colors.bg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  segment: {
    flex: 1,
    paddingVertical: 10,
    borderRadius: radius.md,
    backgroundColor: '#e8e8e8',
    alignItems: 'center',
    cursor: 'pointer',
  },
  segmentActive: { backgroundColor: colors.primary },
  segmentText: { fontSize: 14, fontWeight: '600', color: colors.textSecondary },
  segmentTextActive: { color: '#fff' },
  period: { fontSize: 11, color: colors.textMuted, paddingVertical: 4 },
  loader: { marginTop: 60 },
  list: { flex: 1 },
  listContent: { paddingBottom: 40, paddingTop: 8 },
  emptyWrap: { alignItems: 'center', marginTop: 60, gap: 12 },
  error: { color: '#c62828', textAlign: 'center' },
  retry: {
    backgroundColor: colors.primary,
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: radius.md,
    cursor: 'pointer',
  },
  retryText: { color: '#fff', fontWeight: '600' },
  podiumWrap: {
    paddingVertical: spacing.lg,
    alignItems: 'center',
  },
  podiumRow: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'flex-end',
    gap: 8,
  },
  podiumCenter: { alignItems: 'center', width: 100 },
  podiumSide: { alignItems: 'center', width: 90 },
  podiumSlot: {
    alignItems: 'center',
    justifyContent: 'flex-end',
    paddingBottom: 8,
    gap: 4,
    cursor: 'pointer',
  },
  podiumEmpty: {
    borderWidth: 1,
    borderStyle: 'dashed',
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingHorizontal: 6,
    backgroundColor: colors.surfaceMuted,
  },
  emptyAvatar: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: '#ddd',
  },
  emptyAvatarSm: {
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: '#ddd',
  },
  emptyLabel: { fontSize: 11, color: colors.textMuted },
  emptyRowText: { fontSize: 13, color: colors.textMuted },
  podiumMedal: { fontSize: 28 },
  podiumNick: { fontSize: 12, fontWeight: '700', color: colors.primaryDark, maxWidth: 80 },
  podiumFish: { fontSize: 11, color: colors.text, maxWidth: 80 },
  podiumPond: { fontSize: 10, color: colors.textMuted, maxWidth: 80 },
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
  rowEmpty: {
    borderStyle: 'dashed',
    backgroundColor: colors.surfaceMuted,
  },
  rowMe: {
    backgroundColor: '#E8F4F8',
    borderColor: colors.primary,
  },
  rowRank: {
    width: 28,
    textAlign: 'center',
    fontSize: 14,
    fontWeight: '700',
    color: colors.textSecondary,
  },
  rowMain: { flex: 1, minWidth: 0 },
  rowNick: { fontWeight: '700', color: colors.primaryDark, fontSize: 14 },
  rowNickMe: { color: colors.primary },
  rowDetails: { flexDirection: 'row', gap: 8, marginTop: 2 },
  rowFish: { fontSize: 13, color: colors.text },
  rowPond: { fontSize: 12, color: colors.textMuted },
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
