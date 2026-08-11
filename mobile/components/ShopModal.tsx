import { useEffect, useState } from 'react';
import {
  View,
  Text,
  Modal,
  Pressable,
  StyleSheet,
  ActivityIndicator,
  ScrollView,
} from 'react-native';
import {
  FISH_DIET_LABELS,
  FISH_BITE_CHECK_MS,
  formatBiteRatePct,
  getBait,
  type BaitConfig,
  type BaitId,
  type PlayerGearState,
  type TackleConfig,
  type TackleId,
} from '@fish-social/shared';
import { useResponsive } from '../lib/responsive';
import { colors, radius, spacing } from '../lib/theme';

interface Props {
  visible: boolean;
  gear: PlayerGearState | null;
  coins: number;
  baits: BaitConfig[];
  tackles: TackleConfig[];
  loading?: boolean;
  error?: string;
  catalogOffline?: boolean;
  onRetry?: () => void;
  onClose: () => void;
  onBuyBait: (baitId: BaitId, qty: number) => Promise<void>;
  onBuyTackle: (tackleId: TackleId) => Promise<void>;
  onEquipBait: (baitId: BaitId) => Promise<void>;
  onEquipTackle: (tackleId: TackleId) => Promise<void>;
}

type Tab = 'bait' | 'tackle';
type SelectedItem = { kind: 'bait'; id: BaitId } | { kind: 'tackle'; id: TackleId };

const GRID_COLS = 4;

function affinityTags(bait: BaitConfig): string[] {
  if (!bait.affinityByDiet) return [];
  return Object.entries(bait.affinityByDiet).map(([diet, affinity]) => {
    const total = bait.globalBonus + affinity;
    return `${FISH_DIET_LABELS[diet as keyof typeof FISH_DIET_LABELS]} 合计 +${formatBiteRatePct(total)}`;
  });
}

const BITE_CHECK_SEC = Math.round(FISH_BITE_CHECK_MS / 1000);

export function ShopModal({
  visible,
  gear,
  coins,
  baits,
  tackles,
  loading,
  error,
  catalogOffline,
  onRetry,
  onClose,
  onBuyBait,
  onBuyTackle,
  onEquipBait,
  onEquipTackle,
}: Props) {
  const { isDesktop } = useResponsive();
  const [tab, setTab] = useState<Tab>('bait');
  const [selected, setSelected] = useState<SelectedItem | null>(null);
  const [busy, setBusy] = useState(false);

  const equippedBait = gear?.equippedBait ?? 'basic';
  const equippedTackle = gear?.equippedTackle ?? 'basic';

  useEffect(() => {
    if (!visible) {
      setSelected(null);
      return;
    }
    if (tab === 'bait') {
      const first = baits[0];
      if (first && (selected?.kind !== 'bait' || !baits.some((b) => b.id === selected.id))) {
        setSelected({ kind: 'bait', id: first.id });
      }
    } else {
      const first = tackles[0];
      if (first && (selected?.kind !== 'tackle' || !tackles.some((t) => t.id === selected.id))) {
        setSelected({ kind: 'tackle', id: first.id });
      }
    }
  }, [visible, tab, baits, tackles, selected]);

  const run = async (fn: () => Promise<void>) => {
    setBusy(true);
    try {
      await fn();
    } finally {
      setBusy(false);
    }
  };

  const selectedBait =
    selected?.kind === 'bait' ? baits.find((b) => b.id === selected.id) ?? null : null;
  const selectedTackle =
    selected?.kind === 'tackle' ? tackles.find((t) => t.id === selected.id) ?? null : null;

  const catalogItems = tab === 'bait' ? baits : tackles;
  const showInitialLoader = loading && baits.length === 0 && tackles.length === 0;

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <View style={styles.overlay}>
        <View style={[styles.panel, isDesktop && styles.panelDesktop]}>
          <View style={styles.header}>
            <View>
              <Text style={styles.title}>🛒 补给站</Text>
              <Text style={styles.coins}>💰 {coins} 金币</Text>
              <Text style={styles.equipped}>
                饵 {getBait(equippedBait)?.name ?? equippedBait} · 竿{' '}
                {tackles.find((t) => t.id === equippedTackle)?.name ?? equippedTackle}
              </Text>
            </View>
            <Pressable onPress={onClose} style={styles.closeBtn}>
              <Text style={styles.closeText}>✕</Text>
            </Pressable>
          </View>

          <View style={styles.tabs}>
            <Pressable
              style={[styles.tab, tab === 'bait' && styles.tabActive]}
              onPress={() => setTab('bait')}
            >
              <Text style={[styles.tabText, tab === 'bait' && styles.tabTextActive]}>鱼饵</Text>
            </Pressable>
            <Pressable
              style={[styles.tab, tab === 'tackle' && styles.tabActive]}
              onPress={() => setTab('tackle')}
            >
              <Text style={[styles.tabText, tab === 'tackle' && styles.tabTextActive]}>渔具</Text>
            </Pressable>
          </View>

          {error ? (
            <View style={styles.errorBar}>
              <Text style={styles.errorText} numberOfLines={2}>
                {error}
                {catalogOffline ? ' · 已显示本地商品目录（只读）' : ''}
              </Text>
              {onRetry ? (
                <Pressable style={styles.retryBtn} onPress={onRetry}>
                  <Text style={styles.retryText}>重试</Text>
                </Pressable>
              ) : null}
            </View>
          ) : null}

          {showInitialLoader ? (
            <ActivityIndicator style={styles.loader} color={colors.primary} />
          ) : (
            <View style={[styles.body, isDesktop && styles.bodyDesktop]}>
              <View style={[styles.displayPane, isDesktop && styles.displayPaneDesktop]}>
                <Text style={styles.paneLabel}>详情</Text>
                {tab === 'bait' && selectedBait ? (
                  <BaitDisplay
                    bait={selectedBait}
                    gear={gear}
                    equippedBait={equippedBait}
                    busy={busy}
                    offline={catalogOffline}
                    onBuy={() => run(() => onBuyBait(selectedBait.id, 1))}
                    onEquip={() => run(() => onEquipBait(selectedBait.id))}
                  />
                ) : tab === 'tackle' && selectedTackle ? (
                  <TackleDisplay
                    tackle={selectedTackle}
                    gear={gear}
                    equippedTackle={equippedTackle}
                    busy={busy}
                    offline={catalogOffline}
                    onBuy={() => run(() => onBuyTackle(selectedTackle.id))}
                    onEquip={() => run(() => onEquipTackle(selectedTackle.id))}
                  />
                ) : catalogItems.length === 0 ? (
                  <View style={styles.displayEmpty}>
                    <Text style={styles.displayEmptyIcon}>🛒</Text>
                    <Text style={styles.empty}>加载失败或目录为空</Text>
                    {onRetry ? (
                      <Pressable style={styles.retryBtn} onPress={onRetry}>
                        <Text style={styles.retryText}>重试</Text>
                      </Pressable>
                    ) : null}
                  </View>
                ) : (
                  <View style={styles.displayEmpty}>
                    <Text style={styles.displayEmptyIcon}>🛒</Text>
                    <Text style={styles.empty}>选择右侧商品查看详情</Text>
                  </View>
                )}
              </View>

              <View style={[styles.gridPane, isDesktop && styles.gridPaneDesktop]}>
                <Text style={styles.paneLabel}>
                  {tab === 'bait' ? `鱼饵 · ${baits.length} 种` : `渔具 · ${tackles.length} 种`}
                </Text>
                {catalogItems.length === 0 ? (
                  <View style={styles.emptyCatalog}>
                    <Text style={styles.empty}>加载失败或目录为空</Text>
                    {onRetry ? (
                      <Pressable style={styles.retryBtn} onPress={onRetry}>
                        <Text style={styles.retryText}>重试</Text>
                      </Pressable>
                    ) : null}
                  </View>
                ) : (
                  <ScrollView style={styles.gridScroll} contentContainerStyle={styles.gridContent}>
                  <View style={styles.grid}>
                    {tab === 'bait' &&
                      baits.map((bait) => {
                        const stock = bait.consumed ? (gear?.baitInventory[bait.id] ?? 0) : null;
                        const isEquipped = equippedBait === bait.id;
                        const active = selected?.kind === 'bait' && selected.id === bait.id;
                        return (
                          <Pressable
                            key={bait.id}
                            style={[
                              styles.cell,
                              isEquipped && styles.cellEquipped,
                              active && styles.cellActive,
                            ]}
                            onPress={() => setSelected({ kind: 'bait', id: bait.id })}
                          >
                            {stock !== null && stock > 0 && (
                              <View style={styles.badge}>
                                <Text style={styles.badgeText}>{stock}</Text>
                              </View>
                            )}
                            <Text style={styles.cellIcon}>{bait.icon}</Text>
                            <Text style={styles.cellName} numberOfLines={1}>
                              {bait.name}
                            </Text>
                          </Pressable>
                        );
                      })}

                    {tab === 'tackle' &&
                      tackles.map((tackle) => {
                        const owned = gear?.ownedTackles.includes(tackle.id) ?? tackle.id === 'basic';
                        const isEquipped = equippedTackle === tackle.id;
                        const active = selected?.kind === 'tackle' && selected.id === tackle.id;
                        return (
                          <Pressable
                            key={tackle.id}
                            style={[
                              styles.cell,
                              !owned && styles.cellUnowned,
                              isEquipped && styles.cellEquipped,
                              active && styles.cellActive,
                            ]}
                            onPress={() => setSelected({ kind: 'tackle', id: tackle.id })}
                          >
                            <Text style={styles.cellIcon}>{tackle.icon}</Text>
                            <Text style={styles.cellName} numberOfLines={1}>
                              {tackle.name}
                            </Text>
                          </Pressable>
                        );
                      })}

                    {Array.from({
                      length: Math.max(
                        0,
                        GRID_COLS -
                          ((tab === 'bait' ? baits.length : tackles.length) % GRID_COLS || GRID_COLS),
                      ),
                    }).map((_, i) => (
                      <View key={`empty-${i}`} style={styles.cellEmpty} />
                    ))}
                  </View>
                </ScrollView>
                )}
              </View>
            </View>
          )}
        </View>
      </View>
    </Modal>
  );
}

function BaitDisplay({
  bait,
  gear,
  equippedBait,
  busy,
  offline,
  onBuy,
  onEquip,
}: {
  bait: BaitConfig;
  gear: PlayerGearState | null;
  equippedBait: BaitId;
  busy: boolean;
  offline?: boolean;
  onBuy: () => void;
  onEquip: () => void;
}) {
  const stock = bait.consumed ? (gear?.baitInventory[bait.id] ?? 0) : '∞';
  const isEquipped = equippedBait === bait.id;
  const tags = affinityTags(bait);

  return (
    <View style={styles.displayCard}>
      <View style={styles.displayIconWrap}>
        <Text style={styles.displayIcon}>{bait.icon}</Text>
      </View>
      <Text style={styles.displayName}>{bait.name}</Text>
      <Text style={styles.displayMeta}>
        {bait.price > 0 ? `${bait.price} 金币/个` : '免费'} · 每 {BITE_CHECK_SEC} 秒 +{formatBiteRatePct(bait.globalBonus)} 基础咬钩
      </Text>
      <Text style={styles.displayHint}>对每种鱼 = 基础 + 对应食性偏好（图鉴/Debug 可查）</Text>
      <Text style={styles.displayMeta}>库存 {String(stock)}</Text>
      {tags.length > 0 && (
        <View style={styles.tagRow}>
          {tags.map((t) => (
            <Text key={t} style={styles.tag}>
              {t}
            </Text>
          ))}
        </View>
      )}
      <View style={styles.displayActions}>
        {bait.id !== 'basic' && bait.price > 0 && (
          <Pressable style={styles.buyBtn} disabled={busy || offline} onPress={onBuy}>
            <Text style={[styles.buyText, offline && styles.buyTextDisabled]}>
              {offline ? '请检查网络' : '买 1'}
            </Text>
          </Pressable>
        )}
        <Pressable
          style={[styles.equipBtn, isEquipped && styles.equipBtnActive]}
          disabled={busy || isEquipped || offline}
          onPress={onEquip}
        >
          <Text style={[styles.equipText, isEquipped && styles.equipTextActive]}>
            {isEquipped ? '已装备' : '装备'}
          </Text>
        </Pressable>
      </View>
    </View>
  );
}

function TackleDisplay({
  tackle,
  gear,
  equippedTackle,
  busy,
  offline,
  onBuy,
  onEquip,
}: {
  tackle: TackleConfig;
  gear: PlayerGearState | null;
  equippedTackle: TackleId;
  busy: boolean;
  offline?: boolean;
  onBuy: () => void;
  onEquip: () => void;
}) {
  const owned = gear?.ownedTackles.includes(tackle.id) ?? tackle.id === 'basic';
  const isEquipped = equippedTackle === tackle.id;

  return (
    <View style={styles.displayCard}>
      <View style={styles.displayIconWrap}>
        <Text style={styles.displayIcon}>{tackle.icon}</Text>
      </View>
      <Text style={styles.displayName}>{tackle.name}</Text>
      <Text style={styles.displayMeta}>
        {tackle.price > 0 ? `${tackle.price} 金币` : '默认'} · 脱钩率 ×
        {((1 - tackle.escapeReduction) * 100).toFixed(0)}%
      </Text>
      <Text style={styles.displayMeta}>{owned ? '已拥有' : '未拥有'}</Text>
      <View style={styles.displayActions}>
        {!owned && tackle.price > 0 && (
          <Pressable style={styles.buyBtn} disabled={busy || offline} onPress={onBuy}>
            <Text style={[styles.buyText, offline && styles.buyTextDisabled]}>
              {offline ? '请检查网络' : '购买'}
            </Text>
          </Pressable>
        )}
        <Pressable
          style={[styles.equipBtn, isEquipped && styles.equipBtnActive]}
          disabled={busy || isEquipped || !owned || offline}
          onPress={onEquip}
        >
          <Text style={[styles.equipText, isEquipped && styles.equipTextActive]}>
            {isEquipped ? '已装备' : '装备'}
          </Text>
        </Pressable>
      </View>
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
    flexDirection: 'column',
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
  coins: { fontSize: 14, fontWeight: '700', color: colors.gold, marginTop: 4 },
  equipped: { fontSize: 11, color: colors.textSecondary, marginTop: 2 },
  closeBtn: { padding: 8, cursor: 'pointer' as const },
  closeText: { fontSize: 18, color: '#999' },
  tabs: { flexDirection: 'row', paddingHorizontal: spacing.md, paddingTop: spacing.sm, gap: 8 },
  tab: {
    flex: 1,
    paddingVertical: 10,
    borderRadius: radius.sm,
    backgroundColor: colors.surfaceMuted,
    alignItems: 'center',
    cursor: 'pointer' as const,
  },
  tabActive: { backgroundColor: colors.primary },
  tabText: { fontWeight: '700', color: colors.textSecondary },
  tabTextActive: { color: '#fff' },
  loader: { marginVertical: 40 },
  errorBar: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginHorizontal: spacing.md,
    marginBottom: spacing.sm,
    padding: spacing.sm,
    backgroundColor: '#FFEBEE',
    borderRadius: radius.sm,
  },
  errorText: { flex: 1, fontSize: 12, color: colors.danger },
  retryBtn: {
    backgroundColor: colors.primary,
    borderRadius: radius.sm,
    paddingHorizontal: 12,
    paddingVertical: 6,
    cursor: 'pointer' as const,
  },
  retryText: { color: '#fff', fontWeight: '700', fontSize: 12 },
  body: { flex: 1, flexDirection: 'column', minHeight: 320 },
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
  gridPane: { flex: 1, minHeight: 200, padding: spacing.md },
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
  displayIcon: { fontSize: 44 },
  displayName: { fontSize: 20, fontWeight: '800', color: colors.text },
  displayMeta: { fontSize: 13, color: colors.textSecondary, marginTop: 4 },
  displayHint: { fontSize: 12, color: colors.textSecondary, marginTop: 4, fontStyle: 'italic' },
  tagRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 10, justifyContent: 'center' },
  tag: {
    fontSize: 10,
    backgroundColor: '#E8F5E9',
    color: '#2E7D32',
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 10,
  },
  displayActions: { flexDirection: 'row', gap: 10, marginTop: 16 },
  displayEmpty: { alignItems: 'center', justifyContent: 'center', paddingVertical: 32 },
  displayEmptyIcon: { fontSize: 48, opacity: 0.3 },
  empty: { textAlign: 'center', color: '#aaa', marginTop: 8 },
  emptyCatalog: { alignItems: 'center', paddingVertical: 24, gap: 12 },
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
  cellEquipped: { borderColor: colors.primary },
  cellUnowned: { opacity: 0.45 },
  cellEmpty: {
    width: '23%',
    minWidth: 68,
    aspectRatio: 1,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: '#f0f0f0',
    borderStyle: 'dashed',
  },
  cellIcon: { fontSize: 28 },
  cellName: { fontSize: 11, fontWeight: '700', marginTop: 2, color: colors.text },
  badge: {
    position: 'absolute',
    top: 4,
    right: 4,
    backgroundColor: colors.primary,
    borderRadius: 8,
    minWidth: 16,
    paddingHorizontal: 4,
    paddingVertical: 1,
  },
  badgeText: { fontSize: 9, fontWeight: '700', color: '#fff', textAlign: 'center' },
  buyBtn: {
    backgroundColor: colors.surfaceMuted,
    borderRadius: radius.sm,
    paddingHorizontal: 14,
    paddingVertical: 8,
    cursor: 'pointer' as const,
  },
  buyText: { fontWeight: '700', color: colors.primary, fontSize: 13 },
  buyTextDisabled: { color: colors.textMuted },
  equipBtn: {
    backgroundColor: colors.primary,
    borderRadius: radius.sm,
    paddingHorizontal: 14,
    paddingVertical: 8,
    cursor: 'pointer' as const,
  },
  equipBtnActive: { backgroundColor: '#ccc' },
  equipText: { fontWeight: '700', color: '#fff', fontSize: 13 },
  equipTextActive: { color: '#666' },
});
