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
import type { FishInventoryItem } from '@fish-social/shared';
import {
  calcFishSellPrice,
  calcFishWeightKg,
  formatFishSize,
  formatFishWeight,
  getQualityInfo,
  getSpecies,
} from '@fish-social/shared';
import { useResponsive } from '../lib/responsive';

interface Props {
  visible: boolean;
  items: FishInventoryItem[];
  coins?: number;
  loading?: boolean;
  onClose: () => void;
  onSell?: (fishId: string) => void;
  onShare?: (fishId: string) => void;
}

const MIN_SLOTS = 80;

export function BackpackModal({
  visible,
  items,
  coins = 0,
  loading,
  onClose,
  onSell,
  onShare,
}: Props) {
  const { isDesktop } = useResponsive();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const slotCount = Math.max(MIN_SLOTS, items.length);

  useEffect(() => {
    if (!visible) {
      setSelectedId(null);
      return;
    }
    if (items.length > 0 && !items.some((i) => i.id === selectedId)) {
      setSelectedId(items[0].id);
    }
    if (items.length === 0) setSelectedId(null);
  }, [visible, items, selectedId]);

  const selected = items.find((i) => i.id === selectedId) ?? null;

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <View style={styles.overlay}>
        <View style={[styles.panel, isDesktop && styles.panelDesktop]}>
          <View style={styles.header}>
            <View>
              <Text style={styles.title}>🎒 背包</Text>
              <Text style={styles.coins}>💰 {coins} 金币</Text>
            </View>
            <Pressable onPress={onClose} style={styles.closeBtn}>
              <Text style={styles.closeText}>✕</Text>
            </Pressable>
          </View>

          {loading ? (
            <ActivityIndicator style={styles.loader} color="#4A90A4" />
          ) : (
            <View style={[styles.body, isDesktop && styles.bodyDesktop]}>
              <View style={[styles.displayPane, isDesktop && styles.displayPaneDesktop]}>
                <Text style={styles.paneLabel}>展示</Text>
                {selected ? (
                  <FishDisplay
                    item={selected}
                    onSell={onSell}
                    onShare={onShare}
                  />
                ) : (
                  <View style={styles.displayEmpty}>
                    <Text style={styles.displayEmptyIcon}>🐟</Text>
                    <Text style={styles.empty}>选择右侧鱼获查看详情</Text>
                  </View>
                )}
              </View>

              <View style={[styles.gridPane, isDesktop && styles.gridPaneDesktop]}>
                <Text style={styles.paneLabel}>
                  背包格子 · {items.length}/{slotCount}
                </Text>
                <ScrollView style={styles.gridScroll} contentContainerStyle={styles.gridContent}>
                  {items.length === 0 ? (
                    <Text style={styles.emptyGrid}>背包空空如也，去钓鱼吧！</Text>
                  ) : null}
                  <View style={styles.grid}>
                    {Array.from({ length: slotCount }, (_, i) => {
                      const item = items[i];
                      if (!item) {
                        return <View key={`empty-${i}`} style={styles.cellEmpty} />;
                      }
                      const species = getSpecies(item.speciesId);
                      const quality = getQualityInfo(item.quality);
                      const active = item.id === selectedId;
                      return (
                        <Pressable
                          key={item.id}
                          style={[
                            styles.cell,
                            { borderColor: quality.color },
                            active && styles.cellActive,
                          ]}
                          onPress={() => setSelectedId(item.id)}
                        >
                          <Text style={styles.cellIcon}>{species.icon}</Text>
                          <Text style={[styles.cellName, { color: quality.color }]} numberOfLines={1}>
                            {species.name}
                          </Text>
                        </Pressable>
                      );
                    })}
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

function FishDisplay({
  item,
  onSell,
  onShare,
}: {
  item: FishInventoryItem;
  onSell?: (id: string) => void;
  onShare?: (id: string) => void;
}) {
  const species = getSpecies(item.speciesId);
  const quality = getQualityInfo(item.quality);
  const price = calcFishSellPrice(item);

  return (
    <View style={[styles.displayCard, { borderColor: quality.color }]}>
      <View style={[styles.displayIconWrap, { backgroundColor: quality.color + '22' }]}>
        <Text style={styles.displayIcon}>{species.icon}</Text>
      </View>
      <Text style={styles.displaySpecies}>{species.name}</Text>
      <Text style={[styles.displayQuality, { color: quality.color }]}>
        【{quality.name}】
      </Text>
      <Text style={styles.displaySize}>体长 {formatFishSize(item.sizeM)}</Text>
      <Text style={styles.displaySize}>重量 {formatFishWeight(calcFishWeightKg(item.sizeM))}</Text>
      <Text style={styles.displayPrice}>出售可得 {price} 金币</Text>
      <View style={styles.displayActions}>
        {onShare && (
          <Pressable style={styles.shareBtn} onPress={() => onShare(item.id)}>
            <Text style={styles.actionText}>分享到动态</Text>
          </Pressable>
        )}
        {onSell && (
          <Pressable style={styles.sellBtn} onPress={() => onSell(item.id)}>
            <Text style={styles.actionText}>出售</Text>
          </Pressable>
        )}
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
    padding: 16,
  },
  panel: {
    width: '100%',
    maxWidth: 720,
    maxHeight: '90%',
    backgroundColor: '#fff',
    borderRadius: 16,
    overflow: 'hidden',
  },
  panelDesktop: { maxWidth: 860 },
  header: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    justifyContent: 'space-between',
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  title: { fontSize: 18, fontWeight: '700', color: '#2C5F6F' },
  coins: { fontSize: 13, color: '#E6A700', marginTop: 4, fontWeight: '600' },
  closeBtn: { padding: 4, cursor: 'pointer' },
  closeText: { fontSize: 18, color: '#888' },
  loader: { margin: 40 },
  body: { flexDirection: 'column' },
  bodyDesktop: { flexDirection: 'row', minHeight: 360 },
  displayPane: {
    padding: 16,
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
  gridPane: { padding: 16, maxHeight: 360 },
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
    padding: 16,
    borderRadius: 14,
    borderWidth: 2,
    backgroundColor: '#F8FAFB',
  },
  displayIconWrap: {
    width: 88,
    height: 88,
    borderRadius: 44,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 12,
  },
  displayIcon: { fontSize: 44 },
  displaySpecies: { fontSize: 20, fontWeight: '800', color: '#333' },
  displayQuality: { fontSize: 15, fontWeight: '700', marginTop: 4 },
  displaySize: { fontSize: 14, color: '#666', marginTop: 8 },
  displayPrice: { fontSize: 13, color: '#E6A700', marginTop: 6, fontWeight: '600' },
  displayActions: { flexDirection: 'row', gap: 10, marginTop: 16 },
  displayEmpty: { alignItems: 'center', justifyContent: 'center', paddingVertical: 32 },
  displayEmptyIcon: { fontSize: 48, opacity: 0.3 },
  empty: { textAlign: 'center', color: '#aaa', marginTop: 8 },
  gridScroll: { maxHeight: 320 },
  gridContent: { paddingBottom: 8 },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  cell: {
    width: '23%',
    minWidth: 68,
    aspectRatio: 1,
    borderWidth: 2,
    borderRadius: 10,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#F8FAFB',
    padding: 4,
    cursor: 'pointer',
  },
  cellActive: {
    backgroundColor: '#E8F4F8',
    shadowColor: '#4A90A4',
    shadowOpacity: 0.25,
    shadowRadius: 4,
  },
  cellEmpty: {
    width: '23%',
    minWidth: 68,
    aspectRatio: 1,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: '#f0f0f0',
    borderStyle: 'dashed',
  },
  cellIcon: { fontSize: 24 },
  cellName: { fontSize: 9, fontWeight: '700', marginTop: 2 },
  emptyGrid: { textAlign: 'center', color: '#aaa', padding: 24 },
  shareBtn: {
    backgroundColor: '#4A90A4',
    borderRadius: 8,
    paddingHorizontal: 14,
    paddingVertical: 8,
    cursor: 'pointer',
  },
  sellBtn: {
    backgroundColor: '#FFB74D',
    borderRadius: 8,
    paddingHorizontal: 14,
    paddingVertical: 8,
    cursor: 'pointer',
  },
  actionText: { color: '#fff', fontSize: 13, fontWeight: '600' },
});
