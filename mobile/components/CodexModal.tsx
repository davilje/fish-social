import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Modal,
  View,
  Text,
  StyleSheet,
  Pressable,
  ScrollView,
  ActivityIndicator,
} from 'react-native';
import {
  FISH_SPECIES,
  FISH_BITE_CHECK_MS,
  FISH_DIET_LABELS,
  getSpeciesDiet,
  BAITS,
  baitBiteBonus,
  formatBiteRatePct,
  formatFishSize,
  type FishCodexEntry,
  type FishSpeciesId,
} from '@fish-social/shared';
import { getPlayerId } from '../lib/playerId';
import { getCodex } from '../lib/codexApi';
import { useResponsive } from '../lib/responsive';
import { colors, spacing, radius } from '../lib/theme';

interface Props {
  visible: boolean;
  onClose: () => void;
}

const GRID_COLS = 4;
const TOTAL_SPECIES = FISH_SPECIES.length;

function topRecommendedBaits(speciesId: FishSpeciesId) {
  return [...BAITS]
    .sort((a, b) => baitBiteBonus(b.id, speciesId) - baitBiteBonus(a.id, speciesId))
    .slice(0, 3);
}

export function CodexModal({ visible, onClose }: Props) {
  const { isDesktop } = useResponsive();
  const [loading, setLoading] = useState(false);
  const [entries, setEntries] = useState<FishCodexEntry[]>([]);
  const [selectedId, setSelectedId] = useState<FishSpeciesId | null>(null);

  const load = useCallback(async () => {
    const playerId = getPlayerId();
    if (!playerId) return;
    setLoading(true);
    try {
      setEntries(await getCodex(playerId));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (visible) load();
    else setSelectedId(null);
  }, [visible, load]);

  const unlockedCount = useMemo(
    () => entries.filter((e) => e.totalCaught > 0).length,
    [entries],
  );

  const selectedSpecies = selectedId ? FISH_SPECIES.find((s) => s.id === selectedId) ?? null : null;
  const selectedEntry = selectedId ? entries.find((e) => e.speciesId === selectedId) : null;
  const caught = (selectedEntry?.totalCaught ?? 0) > 0;

  return (
    <Modal visible={visible} animationType="fade" transparent onRequestClose={onClose}>
      <View style={styles.overlay}>
        <View style={[styles.panel, isDesktop && styles.panelDesktop]}>
          <View style={styles.header}>
            <View>
              <Text style={styles.title}>📖 钓鱼图鉴</Text>
              <Text style={styles.subtitle}>
                已解锁 {unlockedCount}/{TOTAL_SPECIES}
              </Text>
            </View>
            <Pressable onPress={onClose} style={styles.closeBtn}>
              <Text style={styles.closeText}>✕</Text>
            </Pressable>
          </View>

          {loading ? (
            <ActivityIndicator style={styles.loader} color={colors.primary} />
          ) : (
            <View style={[styles.body, isDesktop && styles.bodyDesktop]}>
              <View style={[styles.displayPane, isDesktop && styles.displayPaneDesktop]}>
                <Text style={styles.paneLabel}>详情</Text>
                {selectedSpecies ? (
                  caught && selectedEntry ? (
                    <SpeciesDisplay species={selectedSpecies} entry={selectedEntry} />
                  ) : (
                    <LockedDisplay species={selectedSpecies} />
                  )
                ) : (
                  <View style={styles.displayEmpty}>
                    <Text style={styles.displayEmptyIcon}>🐟</Text>
                    <Text style={styles.empty}>选择右侧鱼种查看详情</Text>
                  </View>
                )}
              </View>

              <View style={[styles.gridPane, isDesktop && styles.gridPaneDesktop]}>
                <Text style={styles.paneLabel}>鱼种 · {TOTAL_SPECIES} 种</Text>
                <ScrollView style={styles.gridScroll} contentContainerStyle={styles.gridContent}>
                  <View style={styles.grid}>
                    {FISH_SPECIES.map((s) => {
                      const e = entries.find((x) => x.speciesId === s.id);
                      const isUnlocked = (e?.totalCaught ?? 0) > 0;
                      const active = selectedId === s.id;
                      return (
                        <Pressable
                          key={s.id}
                          style={[styles.cell, active && styles.cellActive]}
                          onPress={() => setSelectedId(s.id)}
                        >
                          <Text style={[styles.cellIcon, !isUnlocked && styles.cellIconLocked]}>
                            {isUnlocked ? s.icon : '🔒'}
                          </Text>
                          <Text
                            style={[styles.cellName, !isUnlocked && styles.cellNameLocked]}
                            numberOfLines={1}
                          >
                            {isUnlocked ? s.name : '???'}
                          </Text>
                        </Pressable>
                      );
                    })}
                    {Array.from({
                      length: Math.max(0, GRID_COLS - (FISH_SPECIES.length % GRID_COLS || GRID_COLS)),
                    }).map((_, i) => (
                      <View key={`empty-${i}`} style={styles.cellEmpty} />
                    ))}
                  </View>
                </ScrollView>
              </View>
            </View>
          )}
        </View>
      </View>
    </Modal>
  );
}

function LockedDisplay({ species }: { species: (typeof FISH_SPECIES)[number] }) {
  return (
    <View style={styles.displayCard}>
      <View style={[styles.displayIconWrap, styles.displayIconLocked]}>
        <Text style={[styles.displayIcon, styles.iconSilhouette]}>{species.icon}</Text>
      </View>
      <Text style={styles.displayNameLocked}>???</Text>
      <Text style={styles.displayMeta}>尚未钓到</Text>
    </View>
  );
}

function SpeciesDisplay({
  species,
  entry,
}: {
  species: (typeof FISH_SPECIES)[number];
  entry: FishCodexEntry;
}) {
  const recommended = topRecommendedBaits(species.id);

  return (
    <View style={styles.displayCard}>
      <View style={styles.displayIconWrap}>
        <Text style={styles.displayIcon}>{species.icon}</Text>
      </View>
      <Text style={styles.displayName}>{species.name}</Text>
      <Text style={styles.displayMeta}>
        食性：{FISH_DIET_LABELS[getSpeciesDiet(species.id)]}
      </Text>
      <Text style={styles.displayMeta}>
        每 {Math.round(FISH_BITE_CHECK_MS / 1000)} 秒 {formatBiteRatePct(species.biteRatePerTick ?? species.biteWeight * 0.2)} 基础咬钩
      </Text>
      <Text style={styles.displayMeta}>
        上钩后脱钩 {(species.baseEscapeRate * 100).toFixed(0)}%（未计品质/渔具）
      </Text>
      <Text style={styles.displayMeta}>
        记录：共 {entry.totalCaught} 条 · 最大 {formatFishSize(entry.maxSizeM)}
      </Text>
      {entry.firstCaughtAt && (
        <Text style={styles.displayMeta}>
          首次：{new Date(entry.firstCaughtAt).toLocaleString()}
        </Text>
      )}
      <Text style={[styles.displayMeta, styles.recommendTitle]}>推荐鱼饵 Top3</Text>
      {recommended.map((b) => (
        <Text key={b.id} style={styles.recommendLine}>
          {b.icon} {b.name} (+{formatBiteRatePct(baitBiteBonus(b.id, species.id))})
        </Text>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: spacing.md,
  },
  panel: {
    width: '100%',
    maxWidth: 720,
    maxHeight: '90%',
    alignSelf: 'center',
    backgroundColor: '#fff',
    borderRadius: radius.lg,
    overflow: 'hidden',
  },
  panelDesktop: { maxWidth: 860 },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    padding: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  title: { fontSize: 18, fontWeight: '800', color: colors.primaryDark },
  subtitle: { fontSize: 13, color: colors.textSecondary, marginTop: 4, fontWeight: '600' },
  closeBtn: { padding: 8, cursor: 'pointer' as const },
  closeText: { fontSize: 18, color: '#999' },
  loader: { marginVertical: 40 },
  body: { flexDirection: 'column' },
  bodyDesktop: { flexDirection: 'row', minHeight: 360 },
  displayPane: {
    padding: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
    minHeight: 220,
  },
  displayPaneDesktop: {
    flex: 1,
    borderBottomWidth: 0,
    borderRightWidth: 1,
    borderRightColor: '#eee',
  },
  gridPane: { padding: spacing.md, maxHeight: 280 },
  gridPaneDesktop: { flex: 1, maxHeight: undefined },
  paneLabel: {
    fontSize: 12,
    fontWeight: '700',
    color: '#888',
    marginBottom: 12,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  displayCard: {
    alignItems: 'center',
    padding: spacing.md,
    borderRadius: 14,
    borderWidth: 2,
    borderColor: colors.border,
    backgroundColor: colors.surfaceMuted,
  },
  displayIconWrap: {
    width: 88,
    height: 88,
    borderRadius: 44,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.primaryLight,
    marginBottom: 12,
  },
  displayIconLocked: { backgroundColor: '#eee' },
  displayIcon: { fontSize: 44 },
  iconSilhouette: { opacity: 0.25 },
  displayName: { fontSize: 20, fontWeight: '800', color: colors.text },
  displayNameLocked: { fontSize: 20, fontWeight: '800', color: '#999' },
  displayMeta: { fontSize: 13, color: colors.textSecondary, marginTop: 4, textAlign: 'center' },
  recommendTitle: { marginTop: 10, fontWeight: '700', color: colors.text },
  recommendLine: { fontSize: 12, color: colors.textSecondary, marginTop: 2 },
  displayEmpty: { alignItems: 'center', justifyContent: 'center', paddingVertical: 32 },
  displayEmptyIcon: { fontSize: 48, opacity: 0.3 },
  empty: { textAlign: 'center', color: '#aaa', marginTop: 8 },
  gridScroll: { flex: 1 },
  gridContent: { paddingBottom: 8 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  cell: {
    width: '23%',
    minWidth: 68,
    aspectRatio: 1,
    borderWidth: 2,
    borderColor: colors.border,
    borderRadius: 10,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.surfaceMuted,
    padding: 4,
    cursor: 'pointer' as const,
  },
  cellActive: {
    backgroundColor: colors.primaryLight,
    borderColor: colors.primary,
    shadowColor: colors.primary,
    shadowOpacity: 0.25,
    shadowRadius: 4,
  },
  cellIcon: { fontSize: 24 },
  cellIconLocked: { fontSize: 20 },
  cellName: { fontSize: 9, fontWeight: '700', marginTop: 2, color: colors.text },
  cellNameLocked: { color: '#999' },
  cellEmpty: {
    width: '23%',
    minWidth: 68,
    aspectRatio: 1,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: '#f0f0f0',
    borderStyle: 'dashed',
  },
});