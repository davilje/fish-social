import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Pressable,
  ActivityIndicator,
  ScrollView,
  Platform,
  Alert,
  TextInput,
} from 'react-native';
import {
  BAITS,
  FISH_QUALITIES,
  formatBiteRatePct,
  getBait,
  getQualityInfo,
  getSpecies,
  isNearMaxSize,
  PONDS,
  type BaitId,
  type FishQuality,
  type PondFishEntity,
  type PondFishingDebugFishContribution,
  type PondFishingDebugResponse,
} from '@fish-social/shared';
import { adminApiClient } from '../lib/adminApi';
import { colors, spacing, radius } from '../lib/theme';

interface Props {
  pondId: string;
  fish: PondFishEntity[];
}

type TabKey = 'pond' | number;

const NEAR_MAX_TOOLTIP =
  '此鱼体长已接近该品质允许的最大值；咬钩率仍按品质×尺寸，脱钩/收杆仅按体长';

function formatProb(rate: number): string {
  return formatBiteRatePct(rate);
}

function generationLabel(m: PondFishEntity): string {
  if (m.generation <= 0) return '初始鱼';
  return `第 ${m.generation} 代`;
}

async function copyFishId(id: string): Promise<void> {
  if (Platform.OS === 'web' && typeof navigator !== 'undefined' && navigator.clipboard) {
    await navigator.clipboard.writeText(id);
    return;
  }
  Alert.alert('鱼 ID', id, [{ text: '确定' }]);
}

function PondEcologyCell({ fish }: { fish: PondFishEntity }) {
  const species = getSpecies(fish.speciesId);
  const quality = getQualityInfo(fish.quality);
  const nearMax = isNearMaxSize(fish);
  const bred = fish.generation >= 1;

  return (
    <View style={[styles.cell, { borderColor: quality.color }]}>
      {nearMax ? (
        <Pressable
          style={styles.badgeNearMax}
          onLongPress={() => Alert.alert('体型上限', NEAR_MAX_TOOLTIP)}
        >
          <Text style={styles.badgeText}>体型上限</Text>
        </Pressable>
      ) : null}
      {bred ? (
        <View style={styles.badgeBred}>
          <Text style={styles.badgeText}>繁殖</Text>
        </View>
      ) : null}
      <Text style={styles.cellIcon}>{species.icon}</Text>
      <Text style={styles.cellSpecies} numberOfLines={1}>
        {species.name}
      </Text>
      <Text style={styles.cellSize}>{fish.sizeM.toFixed(2)}m</Text>
      <Text style={[styles.cellQuality, { color: quality.color }]}>{quality.name}</Text>
      <Text style={styles.cellAux}>钓点 {fish.spotId}</Text>
      <Text style={styles.cellAux}>{generationLabel(fish)}</Text>
      <Pressable onPress={() => copyFishId(fish.id)} onLongPress={() => copyFishId(fish.id)}>
        <Text style={styles.cellId}>{fish.id.slice(0, 8)}…</Text>
      </Pressable>
    </View>
  );
}

function SpotFishCell({
  row,
  spotMultiplier,
  checkSec,
}: {
  row: PondFishingDebugFishContribution;
  spotMultiplier: number;
  checkSec: number;
}) {
  const species = getSpecies(row.speciesId);
  const quality = getQualityInfo(row.quality);

  return (
    <View style={[styles.cell, { borderColor: quality.color }]}>
      {row.isNearMaxSize ? (
        <Pressable
          style={styles.badgeNearMaxSmall}
          onLongPress={() => Alert.alert('体型上限', NEAR_MAX_TOOLTIP)}
        >
          <Text style={styles.badgeTextSmall}>上限</Text>
        </Pressable>
      ) : null}
      <Text style={styles.cellIcon}>{species.icon}</Text>
      <Text style={styles.cellSpecies} numberOfLines={1}>
        {species.name}
      </Text>
      <Text style={styles.cellSize}>{row.sizeM.toFixed(2)}m</Text>
      <Text style={[styles.cellQuality, { color: quality.color }]}>{quality.name}</Text>
      <Pressable onPress={() => copyFishId(row.fishId)} onLongPress={() => copyFishId(row.fishId)}>
        <Text style={styles.cellId}>{row.fishId.slice(0, 8)}…</Text>
      </Pressable>
      <ScrollView style={styles.probScroll} nestedScrollEnabled showsVerticalScrollIndicator={false}>
        <Text style={styles.probLine}>基础咬钩 {formatProb(row.fishBiteRate)}/{checkSec}s</Text>
        <Text style={styles.probLine}>鱼饵加成 +{formatProb(row.baitBonus)}</Text>
        <Text style={styles.probLine}>运气 ×{spotMultiplier.toFixed(1)}</Text>
        <Text style={styles.probLine}>单次咬钩 {formatProb(row.spotBiteRate)}/{checkSec}s</Text>
        <Text style={styles.probLine}>脱钩 {formatProb(row.escapeRate)}</Text>
        <Text style={styles.probLineSmall}>品质抽中 {formatProb(row.pickShare)}</Text>
      </ScrollView>
    </View>
  );
}

export function AdminPondFishDebugGrid({ pondId, fish }: Props) {
  const pondName = PONDS.find((p) => p.id === pondId)?.name ?? pondId;
  const [activeTab, setActiveTab] = useState<TabKey>('pond');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [data, setData] = useState<PondFishingDebugResponse | null>(null);
  const [debugBaitId, setDebugBaitId] = useState<BaitId>('basic');
  const [spotJumpInput, setSpotJumpInput] = useState('');
  const [humansOnly, setHumansOnly] = useState(false);

  const load = useCallback(
    async (refresh = false) => {
      setLoading(true);
      setError('');
      try {
        const report = await adminApiClient.getFishingDebug(pondId, {
          refresh,
          baitId: debugBaitId,
        });
        setData(report);
      } catch (e) {
        setError(e instanceof Error ? e.message : '加载失败');
      } finally {
        setLoading(false);
      }
    },
    [pondId, debugBaitId],
  );

  useEffect(() => {
    load(false);
  }, [load]);

  const qualityLabel = (q: FishQuality) =>
    FISH_QUALITIES.find((x) => x.id === q)?.name ?? q;

  const spotCount = data?.spots.length ?? 0;
  const tabs = useMemo(() => {
    const items: { key: TabKey; label: string; sub?: string }[] = [
      { key: 'pond', label: '鱼塘' },
    ];
    data?.spots.forEach((spot, i) => {
      const mult = spot.spotMultiplier ?? spot.spotBite ?? 0;
      items.push({
        key: i,
        label: `钓位 ${i + 1}`,
        sub: `×${mult.toFixed(1)}`,
      });
    });
    return items;
  }, [data?.spots]);

  const activeSpot =
    typeof activeTab === 'number' && data ? data.spots[activeTab] : null;
  const checkSec = data ? Math.round(data.constants.checkMs / 1000) : 300;

  const goToSpot = (index: number) => {
    if (!data || index < 0 || index >= data.spots.length) return;
    setActiveTab(index);
    setSpotJumpInput(String(index + 1));
  };

  const applySpotJump = () => {
    const n = Number.parseInt(spotJumpInput, 10);
    if (!Number.isFinite(n) || n < 1 || !data || n > data.spots.length) {
      Alert.alert('跳转钓位', `请输入 1–${data?.spots.length ?? 0} 之间的数字`);
      return;
    }
    goToSpot(n - 1);
  };

  return (
    <View style={styles.wrap}>
      <Text style={styles.panelTitle}>鱼塘调试 · {pondName}</Text>

      <View style={styles.toolbar}>
        <Text style={styles.toolbarLabel}>模拟鱼饵</Text>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.baitScroll}>
          <View style={styles.baitPicker}>
            {BAITS.map((b) => (
              <Pressable
                key={b.id}
                style={[styles.baitChip, debugBaitId === b.id && styles.baitChipActive]}
                onPress={() => setDebugBaitId(b.id)}
              >
                <Text
                  style={[
                    styles.baitChipText,
                    debugBaitId === b.id && styles.baitChipTextActive,
                  ]}
                >
                  {b.icon} {b.name}
                </Text>
              </Pressable>
            ))}
          </View>
        </ScrollView>
        <Pressable
          style={[styles.refreshBtn, loading && styles.refreshBtnDisabled]}
          onPress={() => load(true)}
          disabled={loading}
        >
          <Text style={styles.refreshText}>{loading ? '加载中…' : '强制刷新'}</Text>
        </Pressable>
      </View>

      {loading && !data ? (
        <ActivityIndicator color={colors.primary} style={styles.loader} />
      ) : null}
      {error ? <Text style={styles.error}>{error}</Text> : null}

      {data ? (
        <View style={styles.summaryCard}>
          <Text style={styles.summaryLine}>
            总鱼数 {data.summary.totalFish} · 判定间隔{' '}
            {Math.round(data.constants.checkMs / 1000)} 秒 · 活跃钓鱼{' '}
            {data.constants.activeAnglers} 人 · 补充间隔{' '}
            {Math.round(data.constants.effectiveSupplementCheckMs / 60_000)} 分钟
            {data.constants.lastMigrationAt
              ? ` · 上次迁徙 ${new Date(data.constants.lastMigrationAt).toLocaleTimeString()}`
              : ''}
          </Text>
          <View style={styles.qualityRow}>
            {(Object.entries(data.summary.qualitySupplement) as [
              FishQuality,
              { actual: number; ideal: number },
            ][])
              .filter(([, v]) => v.actual > 0 || v.ideal > 0)
              .map(([q, v]) => (
                <Text key={q} style={styles.qualityChip}>
                  {qualityLabel(q)} {v.actual}/{v.ideal.toFixed(1)}
                </Text>
              ))}
          </View>
          {activeTab === 'pond' ? (
            <Text style={styles.summaryLine}>
              模拟饵 {getBait(data.queryContext.baitId)?.name ?? data.queryContext.baitId} · 塘内{' '}
              {fish.length} 条鱼
            </Text>
          ) : activeSpot ? (
            <Text style={styles.summaryLine}>
              本钓位单次 pBite {formatProb(activeSpot.pBite ?? activeSpot.tickBiteChance)} /{' '}
              {checkSec} 秒 · 抽样鱼{' '}
              {activeSpot.pickedFishId ? activeSpot.pickedFishId.slice(0, 8) + '…' : '—'} · 运气 ×
              {(activeSpot.spotMultiplier ?? 0).toFixed(1)} · 本点鱼{' '}
              {activeSpot.fishAtSpotCount ?? activeSpot.fishContributions.length} 条
            </Text>
          ) : null}
        </View>
      ) : null}

      {data?.activeFishers ? (
        <View style={styles.fisherPanel}>
          <View style={styles.fisherHeader}>
            <Text style={styles.fisherTitle}>在钓玩家（锚点）</Text>
            <Pressable
              style={[styles.baitChip, humansOnly && styles.baitChipActive]}
              onPress={() => setHumansOnly((v) => !v)}
            >
              <Text style={[styles.baitChipText, humansOnly && styles.baitChipTextActive]}>
                {humansOnly ? '仅真人 ✓' : '仅真人'}
              </Text>
            </Pressable>
          </View>
          <Text style={styles.summaryLine}>
            human {data.activeFishers.filter((f) => !f.isBot).length} · bot{' '}
            {data.activeFishers.filter((f) => f.isBot).length}
          </Text>
          {(humansOnly ? data.activeFishers.filter((f) => !f.isBot) : data.activeFishers).length === 0 ? (
            <Text style={styles.meta}>当前无人在钓</Text>
          ) : (
            (humansOnly ? data.activeFishers.filter((f) => !f.isBot) : data.activeFishers).map((f) => {
              const startedNull = f.fishingStartedAt == null;
              return (
                <View
                  key={f.userId}
                  style={[styles.fisherRow, startedNull && styles.fisherRowError]}
                >
                  <Text style={styles.fisherName} numberOfLines={1}>
                    {f.nickname ?? f.playerId ?? f.userId.slice(0, 8)}
                    {f.isBot ? ' · bot' : ' · human'}
                  </Text>
                  <Text style={styles.fisherMeta}>
                    phase={f.fishingPhase} · spot={f.spotId}
                  </Text>
                  <Text style={[styles.fisherMeta, startedNull && styles.fisherErr]}>
                    startedAt={
                      startedNull
                        ? 'null'
                        : new Date(f.fishingStartedAt!).toLocaleTimeString()
                    }{' '}
                    · sessionMs=
                    {f.sessionFishingMs == null
                      ? '—'
                      : `${Math.floor(f.sessionFishingMs / 1000)}s`}
                  </Text>
                </View>
              );
            })
          )}
        </View>
      ) : null}

      <View style={styles.tabBar}>
        <View style={styles.tabBarContent}>
          {tabs.map((t) => (
            <Pressable
              key={String(t.key)}
              style={[styles.tab, activeTab === t.key && styles.tabActive]}
              onPress={() => {
                setActiveTab(t.key);
                if (typeof t.key === 'number') setSpotJumpInput(String(t.key + 1));
              }}
            >
              <Text style={[styles.tabText, activeTab === t.key && styles.tabTextActive]}>
                {t.label}
              </Text>
              {t.sub ? <Text style={styles.tabSub}>{t.sub}</Text> : null}
            </Pressable>
          ))}
        </View>
      </View>

      {spotCount > 8 && typeof activeTab === 'number' ? (
        <View style={styles.spotNav}>
          <Pressable
            style={[styles.spotNavBtn, activeTab <= 0 && styles.spotNavBtnDisabled]}
            disabled={activeTab <= 0}
            onPress={() => goToSpot(activeTab - 1)}
          >
            <Text style={styles.spotNavBtnText}>‹ 上一钓位</Text>
          </Pressable>
          <Text style={styles.spotNavLabel}>
            钓位 {activeTab + 1} / {spotCount}
          </Text>
          <Pressable
            style={[
              styles.spotNavBtn,
              activeTab >= spotCount - 1 && styles.spotNavBtnDisabled,
            ]}
            disabled={activeTab >= spotCount - 1}
            onPress={() => goToSpot(activeTab + 1)}
          >
            <Text style={styles.spotNavBtnText}>下一钓位 ›</Text>
          </Pressable>
          <View style={styles.spotJumpRow}>
            <TextInput
              style={styles.spotJumpInput}
              value={spotJumpInput}
              onChangeText={setSpotJumpInput}
              keyboardType="number-pad"
              placeholder="号"
              placeholderTextColor="#aaa"
              onSubmitEditing={applySpotJump}
            />
            <Pressable style={styles.spotJumpBtn} onPress={applySpotJump}>
              <Text style={styles.spotJumpBtnText}>跳转</Text>
            </Pressable>
          </View>
        </View>
      ) : null}

      {activeTab === 'pond' ? (
        <View style={styles.grid}>
          {fish.map((f) => (
            <PondEcologyCell key={f.id} fish={f} />
          ))}
        </View>
      ) : activeSpot ? (
        activeSpot.fishContributions.length === 0 ? (
          <Text style={styles.emptyState}>
            此钓位当前无有效目标鱼（运气 ×{(activeSpot.spotMultiplier ?? 0).toFixed(1)}）
          </Text>
        ) : (
          <View style={styles.grid}>
            {activeSpot.fishContributions.map((row) => (
              <SpotFishCell
                key={row.fishId}
                row={row}
                spotMultiplier={activeSpot.spotMultiplier ?? 0}
                checkSec={checkSec}
              />
            ))}
          </View>
        )
      ) : null}

      <Text style={styles.legend}>
        上限 = 已达该品质最大体长{spotCount > 0 ? ` · 钓位 ${spotCount} 个` : ''}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    marginTop: spacing.md,
    backgroundColor: '#fff',
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.md,
  },
  panelTitle: {
    fontSize: 16,
    fontWeight: '800',
    color: colors.primaryDark,
    marginBottom: spacing.sm,
  },
  toolbar: { marginBottom: spacing.sm },
  toolbarLabel: { fontSize: 12, fontWeight: '700', color: '#666', marginBottom: 4 },
  baitScroll: { marginBottom: spacing.sm },
  baitPicker: { flexDirection: 'row', gap: 6 },
  baitChip: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: radius.sm,
    backgroundColor: colors.surfaceMuted,
    cursor: 'pointer',
  },
  baitChipActive: { backgroundColor: colors.primary },
  baitChipText: { fontSize: 12, color: '#666' },
  baitChipTextActive: { color: '#fff', fontWeight: '700' },
  refreshBtn: {
    alignSelf: 'flex-start',
    paddingVertical: 8,
    paddingHorizontal: 14,
    borderRadius: radius.sm,
    backgroundColor: colors.surfaceMuted,
    cursor: 'pointer',
  },
  refreshBtnDisabled: { opacity: 0.6 },
  refreshText: { fontSize: 13, fontWeight: '700', color: colors.primary },
  loader: { marginVertical: spacing.md },
  error: { color: '#c00', fontSize: 13, marginBottom: spacing.sm },
  summaryCard: {
    backgroundColor: colors.surfaceMuted,
    borderRadius: radius.sm,
    padding: spacing.sm,
    marginBottom: spacing.sm,
  },
  summaryLine: { fontSize: 13, color: '#555', marginBottom: 4 },
  qualityRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 4, marginBottom: 4 },
  qualityChip: {
    fontSize: 12,
    backgroundColor: '#fff',
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 10,
    color: '#666',
  },
  tabBar: { marginBottom: spacing.sm },
  tabBarContent: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    paddingVertical: 4,
  },
  spotNav: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    gap: 8,
    marginBottom: spacing.sm,
    paddingVertical: 6,
    paddingHorizontal: 8,
    backgroundColor: colors.surfaceMuted,
    borderRadius: radius.sm,
  },
  spotNavBtn: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: radius.sm,
    backgroundColor: '#fff',
    cursor: 'pointer',
  },
  spotNavBtnDisabled: { opacity: 0.35 },
  spotNavBtnText: { fontSize: 12, fontWeight: '700', color: colors.primary },
  spotNavLabel: { fontSize: 13, fontWeight: '700', color: '#444', minWidth: 72 },
  spotJumpRow: { flexDirection: 'row', alignItems: 'center', gap: 6, marginLeft: 'auto' },
  spotJumpInput: {
    width: 48,
    height: 32,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    backgroundColor: '#fff',
    textAlign: 'center',
    fontSize: 13,
    paddingHorizontal: 4,
  },
  spotJumpBtn: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: radius.sm,
    backgroundColor: colors.primary,
    cursor: 'pointer',
  },
  spotJumpBtnText: { fontSize: 12, fontWeight: '700', color: '#fff' },
  tab: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: radius.sm,
    backgroundColor: colors.surfaceMuted,
    alignItems: 'center',
    cursor: 'pointer',
    minWidth: 64,
  },
  tabActive: { backgroundColor: colors.primary },
  tabText: { fontSize: 13, fontWeight: '700', color: '#555' },
  tabTextActive: { color: '#fff' },
  tabSub: { fontSize: 10, color: '#999', marginTop: 2 },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
  },
  cell: {
    width: 200,
    height: 200,
    minWidth: 200,
    minHeight: 200,
    borderWidth: 2,
    borderRadius: 12,
    backgroundColor: '#F8FAFB',
    padding: 10,
    alignItems: 'center',
    position: 'relative',
    overflow: 'hidden',
  },
  cellIcon: { fontSize: 48, lineHeight: 52 },
  cellSpecies: { fontSize: 12, color: '#666', marginTop: 2, maxWidth: '100%' },
  cellSize: { fontSize: 16, fontWeight: '800', color: '#333', marginTop: 4 },
  cellQuality: { fontSize: 14, fontWeight: '700', marginTop: 2 },
  cellAux: { fontSize: 12, color: '#888', marginTop: 4 },
  cellId: {
    fontSize: 11,
    color: '#aaa',
    marginTop: 4,
    fontFamily: Platform.OS === 'web' ? 'monospace' : undefined,
  },
  probScroll: { flex: 1, width: '100%', marginTop: 6 },
  probLine: { fontSize: 13, lineHeight: 18, color: '#444' },
  probLineSmall: { fontSize: 11, lineHeight: 16, color: '#888', marginTop: 2 },
  badgeNearMax: {
    position: 'absolute',
    top: 6,
    right: 6,
    backgroundColor: '#E53935',
    borderRadius: 4,
    paddingHorizontal: 4,
    paddingVertical: 2,
    zIndex: 1,
  },
  badgeNearMaxSmall: {
    position: 'absolute',
    top: 4,
    right: 4,
    backgroundColor: '#E53935',
    borderRadius: 4,
    paddingHorizontal: 3,
    paddingVertical: 1,
    zIndex: 1,
  },
  badgeBred: {
    position: 'absolute',
    top: 6,
    left: 6,
    backgroundColor: '#7E57C2',
    borderRadius: 4,
    paddingHorizontal: 4,
    paddingVertical: 2,
    zIndex: 1,
  },
  badgeText: { color: '#fff', fontSize: 10, fontWeight: '800' },
  badgeTextSmall: { color: '#fff', fontSize: 8, fontWeight: '800' },
  emptyState: {
    fontSize: 14,
    color: colors.textSecondary,
    textAlign: 'center',
    paddingVertical: spacing.lg,
  },
  fisherPanel: {
    marginTop: spacing.sm,
    marginBottom: spacing.sm,
    padding: spacing.sm,
    backgroundColor: colors.surfaceMuted,
    borderRadius: radius.sm,
  },
  fisherHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 4,
  },
  fisherTitle: { fontSize: 14, fontWeight: '700', color: '#333' },
  fisherRow: {
    paddingVertical: 6,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border,
  },
  fisherRowError: { backgroundColor: '#FEF2F2' },
  fisherName: { fontSize: 13, fontWeight: '700', color: '#222' },
  fisherMeta: { fontSize: 11, color: '#666', marginTop: 2 },
  fisherErr: { color: '#B91C1C', fontWeight: '700' },
  meta: { fontSize: 12, color: '#888', marginTop: 4 },
  legend: {
    fontSize: 12,
    color: '#888',
    marginTop: spacing.md,
    textAlign: 'center',
  },
});
