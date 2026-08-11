import { useCallback, useMemo, useState } from 'react';
import { View, Text, StyleSheet, ActivityIndicator } from 'react-native';
import {
  MAX_POND_USERS,
  getWorldTileMap,
  worldPondIdAt,
  screenToTile,
  getPondById,
} from '@fish-social/shared';
import { useResponsive } from '../lib/responsive';
import { colors } from '../lib/theme';
import { TileCameraView, type TileCameraViewState } from '../lib/iso/TileCameraView';
import { MergedTileLayer } from './tiles/MergedTileLayer';
import { AppNoticeModal } from './AppNoticeModal';

interface Props {
  regions: unknown[];
  occupancy: Record<string, number>;
  pondByRegion: Record<string, { id: string; name: string }>;
  loading: boolean;
  onEnterPond: (pondId: string, pondName: string) => void;
  /** 可选：演示/错误角标文案（画在 L1，不占布局高度） */
  bannerHint?: string | null;
}

function project(
  worldX: number,
  worldY: number,
  cam: TileCameraViewState | null,
): { left: number; top: number } | null {
  if (!cam) return null;
  return {
    left: worldX * cam.scale + cam.translateX,
    top: worldY * cam.scale + cam.translateY,
  };
}

/**
 * MapStage：L2 场景 + L1 HUD（同区域 absoluteFill）
 * 不画进顶栏；无页脚占高（FEAT-SCENE-TILE-4）
 */
export function WorldMapView({ occupancy, loading, onEnterPond, bannerHint }: Props) {
  const { isCompact } = useResponsive();
  const map = useMemo(() => getWorldTileMap(), []);
  const [cam, setCam] = useState<TileCameraViewState | null>(null);
  const [pending, setPending] = useState<{ pondId: string; name: string; count: number } | null>(
    null,
  );

  const openPond = useCallback(
    (pondId: string) => {
      const pond = getPondById(pondId);
      const name = pond?.name ?? pondId;
      setPending({ pondId, name, count: occupancy[pondId] ?? 0 });
    },
    [occupancy],
  );

  const onTapWorld = useCallback(
    (wx: number, wy: number) => {
      const { col, row } = screenToTile(wx, wy);
      const pondId = worldPondIdAt(map, col, row);
      if (pondId) openPond(pondId);
    },
    [map, openPond],
  );

  const onViewChange = useCallback((v: TileCameraViewState) => {
    setCam(v);
  }, []);

  const focus = useMemo(() => {
    const e = map.entries[0];
    return e ? { x: e.x, y: e.y } : { x: map.worldWidth / 2, y: map.worldHeight / 2 };
  }, [map]);

  const hudFont = cam ? Math.max(11, Math.min(16, 12 / Math.sqrt(cam.scale))) : 12;
  const hudCountFont = Math.max(10, hudFont - 1);

  if (loading) {
    return (
      <View style={styles.mapStage}>
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.primary} />
          <Text style={styles.hint}>加载世界地图...</Text>
        </View>
      </View>
    );
  }

  return (
    <View style={styles.mapStage}>
      {/* L2 场景：底 */}
      <View style={styles.sceneLayer} pointerEvents="box-none">
        <TileCameraView
          worldWidth={map.worldWidth}
          worldHeight={map.worldHeight}
          focus={focus}
          minScale={0.22}
          maxScale={2.4}
          initialScaleMode="panable"
          style={styles.camera}
          onViewChange={onViewChange}
          onTapWorld={onTapWorld}
        >
          <View style={[styles.world, { width: map.worldWidth, height: map.worldHeight }]}>
            <MergedTileLayer
              rects={map.mergedRects}
              worldWidth={map.worldWidth}
              worldHeight={map.worldHeight}
              backgroundColor={map.voidColor}
            />
          </View>
        </TileCameraView>
      </View>

      {/* L1 HUD：与场景同区域，裁剪在 MapStage 内，不压顶栏 */}
      <View style={styles.hudLayer} pointerEvents="none">
        {map.labels.map((lab) => {
          const pond = getPondById(lab.pondId);
          const count = occupancy[lab.pondId] ?? 0;
          const pos = project(lab.x, lab.y, cam);
          if (!pos) return null;
          // 裁掉贴边会顶出舞台的标签
          if (
            cam &&
            (pos.top < -8 ||
              pos.left < -60 ||
              pos.top > cam.viewportH + 8 ||
              pos.left > cam.viewportW + 60)
          ) {
            return null;
          }
          return (
            <View
              key={`hud-${lab.pondId}`}
              style={[styles.hudLabel, { left: pos.left - 48, top: pos.top - 28 }]}
            >
              <Text style={[styles.hudName, { fontSize: hudFont }]}>
                {pond?.name ?? lab.pondId}
              </Text>
              <Text style={[styles.hudCount, { fontSize: hudCountFont }]}>
                {count}/{MAX_POND_USERS}
              </Text>
            </View>
          );
        })}
        {bannerHint ? (
          <Text style={[styles.bannerHint, isCompact && styles.bannerHintCompact]} numberOfLines={2}>
            {bannerHint}
          </Text>
        ) : null}
        <Text style={[styles.cornerHint, isCompact && styles.cornerHintCompact]}>
          {isCompact ? '拖拽 · 缩放 · 点击进塘' : '拖拽浏览 · 滚轮缩放 · 点击池塘色块进入'}
        </Text>
      </View>

      <AppNoticeModal
        visible={!!pending}
        title={pending?.name ?? ''}
        message={
          pending
            ? `当前人数 ${pending.count}/${MAX_POND_USERS}\n进入该鱼塘开始钓鱼？`
            : ''
        }
        confirmLabel="进入"
        cancelLabel="取消"
        onCancel={() => setPending(null)}
        onConfirm={() => {
          if (!pending) return;
          const { pondId, name } = pending;
          setPending(null);
          onEnterPond(pondId, name);
        }}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  mapStage: {
    flex: 1,
    minHeight: 0,
    position: 'relative',
    overflow: 'hidden',
    backgroundColor: '#2E4A2C',
  },
  sceneLayer: {
    ...StyleSheet.absoluteFillObject,
    zIndex: 1,
  },
  hudLayer: {
    ...StyleSheet.absoluteFillObject,
    zIndex: 2,
    overflow: 'hidden',
  },
  camera: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: '#2E4A2C',
    borderRadius: 0,
  },
  world: { position: 'relative' },
  center: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  hint: { marginTop: 8, color: colors.textMuted },
  hudLabel: {
    position: 'absolute',
    width: 96,
    alignItems: 'center',
  },
  hudName: {
    fontWeight: '800',
    color: '#fff',
    textShadowColor: 'rgba(0,0,0,0.65)',
    textShadowOffset: { width: 0, height: 1 },
    textShadowRadius: 3,
  },
  hudCount: {
    fontWeight: '700',
    color: '#FFE082',
    textShadowColor: 'rgba(0,0,0,0.55)',
    textShadowOffset: { width: 0, height: 1 },
    textShadowRadius: 2,
  },
  bannerHint: {
    position: 'absolute',
    top: 8,
    left: 12,
    right: 12,
    textAlign: 'center',
    color: 'rgba(255,255,255,0.85)',
    fontSize: 12,
    textShadowColor: 'rgba(0,0,0,0.55)',
    textShadowOffset: { width: 0, height: 1 },
    textShadowRadius: 2,
  },
  bannerHintCompact: { fontSize: 11 },
  cornerHint: {
    position: 'absolute',
    left: 10,
    bottom: 10,
    color: 'rgba(255,255,255,0.78)',
    fontSize: 11,
    fontWeight: '600',
    textShadowColor: 'rgba(0,0,0,0.55)',
    textShadowOffset: { width: 0, height: 1 },
    textShadowRadius: 2,
  },
  cornerHintCompact: { fontSize: 10 },
});
