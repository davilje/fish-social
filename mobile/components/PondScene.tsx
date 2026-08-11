import { useCallback, useEffect, useMemo, useState } from 'react';
import { View, Text, StyleSheet } from 'react-native';
import {
  isFishingActive,
  getPondTileMap,
  type PondUser,
  type FishingSpot,
  type FishingPhase,
} from '@fish-social/shared';
import { useResponsive } from '../lib/responsive';
import { formatFishingDuration } from '../lib/config';
import { PondCharacter } from './PondCharacter';
import { FishingFloatText } from './FishingFloatText';
import { TileCameraView, type TileCameraViewState } from '../lib/iso/TileCameraView';
import { MergedTileLayer } from './tiles/MergedTileLayer';
import type { FishingFloatDisplay } from '../lib/usePondSocket';

interface Props {
  users: PondUser[];
  spots: FishingSpot[];
  myUserId: string | null;
  pondId: string;
  onPressUser?: (user: PondUser) => void;
  onPressSpot?: (spotId: string) => void;
  floatTexts?: Record<string, FishingFloatDisplay>;
}

type StatusKind = 'hooked' | 'fishing' | null;

function resolveStatusKind(phase?: FishingPhase, outcome?: string): StatusKind {
  if (phase === 'hooked') return 'hooked';
  if (phase === 'resolving' && outcome === 'catch') return 'hooked';
  if (
    phase === 'waiting' ||
    phase === 'baiting' ||
    phase === 'casting' ||
    phase === 'resolving' ||
    phase === 'stopping'
  ) {
    return 'fishing';
  }
  return null;
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

/** 正交 Tilemap 鱼塘：合并色块 + HUD 状态分层（FEAT-SCENE-TILE-3） */
export function PondScene({
  users,
  spots,
  myUserId,
  pondId,
  onPressUser,
  onPressSpot,
  floatTexts,
}: Props) {
  const { isDesktop, pondSideBySide } = useResponsive();
  const map = useMemo(() => getPondTileMap(pondId), [pondId]);
  const [cam, setCam] = useState<TileCameraViewState | null>(null);
  const [bubbleOpen, setBubbleOpen] = useState<Record<string, boolean>>({});
  const [now, setNow] = useState(Date.now());

  const usersBySpot = useMemo(
    () => new Map(users.filter((u) => u.spotId).map((u) => [u.spotId!, u])),
    [users],
  );

  const seated = useMemo(() => {
    return spots
      .map((spot) => {
        const user = usersBySpot.get(spot.id);
        const bind = map.spotById[spot.id];
        if (!bind) return null;
        return {
          spot,
          user,
          x: bind.x,
          y: bind.y,
          /** 角色锚点中心（HUD 投影用） */
          cx: bind.x + 36,
          cy: bind.y + 40,
          depth: bind.row * 1000 + bind.col,
          faceLeft: bind.x < map.waterCenter.x,
        };
      })
      .filter(Boolean) as Array<{
      spot: FishingSpot;
      user?: PondUser;
      x: number;
      y: number;
      cx: number;
      cy: number;
      depth: number;
      faceLeft: boolean;
    }>;
  }, [spots, usersBySpot, map]);

  const unseated = useMemo(() => {
    return users
      .filter((u) => !u.spotId)
      .map((user, i) => ({
        user,
        x: 8 + i * 76,
        y: map.worldHeight - 88,
        cx: 8 + i * 76 + 36,
        cy: map.worldHeight - 48,
        depth: 90000 + i,
        faceLeft: false,
      }));
  }, [users, map.worldHeight]);

  const actors = useMemo(() => {
    const list = [
      ...seated.map((s) => ({
        key: s.spot.id,
        user: s.user,
        x: s.x,
        y: s.y,
        cx: s.cx,
        cy: s.cy,
        depth: s.depth,
        faceLeft: s.faceLeft,
      })),
      ...unseated.map((u) => ({
        key: u.user.id,
        user: u.user,
        x: u.x,
        y: u.y,
        cx: u.cx,
        cy: u.cy,
        depth: u.depth,
        faceLeft: u.faceLeft,
      })),
    ];
    return list.sort((a, b) => a.depth - b.depth || a.x - b.x);
  }, [seated, unseated]);

  const needTick = useMemo(() => {
    return actors.some(({ user }) => {
      if (!user) return false;
      const kind = resolveStatusKind(user.fishingPhase, user.phaseContext?.outcome);
      if (kind == null) return false;
      return user.id === myUserId || !!bubbleOpen[user.id];
    });
  }, [actors, bubbleOpen, myUserId]);

  useEffect(() => {
    if (!needTick) return;
    const tick = setInterval(() => setNow(Date.now()), 250);
    return () => clearInterval(tick);
  }, [needTick]);

  useEffect(() => {
    setBubbleOpen((prev) => {
      let changed = false;
      const next = { ...prev };
      for (const id of Object.keys(next)) {
        const u = users.find((x) => x.id === id);
        if (!u || resolveStatusKind(u.fishingPhase, u.phaseContext?.outcome) == null) {
          delete next[id];
          changed = true;
        }
      }
      return changed ? next : prev;
    });
  }, [users]);

  const onBubbleChange = useCallback((userId: string, visible: boolean) => {
    setBubbleOpen((prev) => {
      if (!!prev[userId] === visible) return prev;
      const next = { ...prev };
      if (visible) next[userId] = true;
      else delete next[userId];
      return next;
    });
  }, []);

  const onViewChange = useCallback((v: TileCameraViewState) => {
    setCam(v);
  }, []);

  const hudFont = cam ? Math.max(10, Math.min(14, 11 / Math.sqrt(Math.max(cam.scale, 0.4)))) : 11;

  return (
    <View style={[styles.scene, isDesktop && styles.sceneDesktop, pondSideBySide && styles.sceneSide]}>
      <View style={styles.stage}>
        {/* L2 场景 */}
        <View style={styles.sceneLayer} pointerEvents="box-none">
        <TileCameraView
          worldWidth={map.worldWidth}
          worldHeight={map.worldHeight}
          focus={map.waterCenter}
          minScale={0.7}
          maxScale={2.8}
          initialScaleMode="panable"
          style={styles.camera}
          onViewChange={onViewChange}
          onTapWorld={(worldX, worldY) => {
            if (!onPressSpot) return;
            const col = Math.floor(worldX / 32);
            const row = Math.floor(worldY / 32);
            const spot = spots.find((s) => {
              const bind = map.spotById[s.id];
              return bind?.col === col && bind.row === row;
            });
            if (spot) onPressSpot(spot.id);
          }}
        >
          <View style={[styles.world, { width: map.worldWidth, height: map.worldHeight }]}>
            <MergedTileLayer
              rects={map.mergedRects}
              worldWidth={map.worldWidth}
              worldHeight={map.worldHeight}
            />

            {actors.map((a) => (
              <View
                key={a.key}
                style={[styles.actorSlot, { left: a.x, top: a.y, zIndex: 20000 + a.depth }]}
                pointerEvents="box-none"
              >
                {a.user ? (
                  <PondCharacter
                    user={a.user}
                    isMe={a.user.id === myUserId}
                    faceLeft={a.faceLeft}
                    onBubbleChange={onBubbleChange}
                    onPress={
                      onPressUser && a.user.playerId ? () => onPressUser(a.user!) : undefined
                    }
                  />
                ) : null}
              </View>
            ))}

            {/* 飘字仍在场景锚点附近（短时特效） */}
            <View style={styles.floatLayer} pointerEvents="none">
              {actors.map((a) => {
                const user = a.user;
                if (!user) return null;
                const floatText = floatTexts?.[user.id];
                if (!floatText) return null;
                return (
                  <View
                    key={`ft-${user.id}`}
                    style={[styles.overlayAnchor, { left: a.x, top: a.y, zIndex: 50000 + a.depth }]}
                  >
                    <FishingFloatText
                      text={floatText.text}
                      color={floatText.color}
                      animKey={floatText.token}
                    />
                  </View>
                );
              })}
            </View>
          </View>
        </TileCameraView>
        </View>

        {/* L1 HUD：与场景同区域 */}
        <View style={styles.hud} pointerEvents="none">
          {actors.map((a) => {
            const user = a.user;
            if (!user) return null;
            const isMe = user.id === myUserId;
            const phase = user.fishingPhase;
            const statusKind = resolveStatusKind(phase, user.phaseContext?.outcome);
            const hookRemainingMs =
              phase === 'hooked' && user.phaseEndsAt
                ? Math.max(0, user.phaseEndsAt - now)
                : 0;
            const sessionMs =
              isFishingActive(phase) &&
              phase !== 'stopping' &&
              (user.sessionStartedAt ?? user.fishingStartedAt) != null
                ? Math.max(0, now - (user.sessionStartedAt ?? user.fishingStartedAt)!)
                : (user.sessionFishingMs ?? 0);
            const statusIcon =
              statusKind === 'hooked' ? '🎣' : statusKind === 'fishing' ? '🐟' : null;
            const statusText =
              statusKind === 'hooked'
                ? `上钩 · ${formatFishingDuration(hookRemainingMs)}`
                : statusKind === 'fishing'
                  ? `钓鱼中 · ${formatFishingDuration(sessionMs)}`
                  : null;
            const showOwnBadge = isMe && statusKind != null && statusText;
            const showOtherBubble =
              !isMe && !!bubbleOpen[user.id] && statusKind != null && statusText;
            if (!showOwnBadge && !showOtherBubble) return null;
            const pos = project(a.cx, a.cy - 48, cam);
            if (!pos) return null;
            return (
              <View
                key={`hud-${user.id}`}
                style={[
                  showOtherBubble ? styles.hudBubble : styles.hudBadge,
                  { left: pos.left - 55, top: pos.top - (showOtherBubble ? 28 : 8) },
                ]}
              >
                <Text style={[styles.hudIcon, { fontSize: hudFont }]}>{statusIcon}</Text>
                <Text
                  style={[
                    showOtherBubble ? styles.hudBubbleText : styles.hudBadgeText,
                    { fontSize: hudFont },
                  ]}
                >
                  {statusText}
                </Text>
              </View>
            );
          })}
        </View>
      </View>
      <Text style={styles.hint}>拖拽移动 · 滚轮/捏合缩放</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  scene: {
    flex: 1,
    margin: 8,
    minHeight: 300,
    borderRadius: 0,
    borderWidth: 2,
    borderColor: '#3E5C38',
    overflow: 'hidden',
    backgroundColor: '#2E4A2C',
  },
  sceneDesktop: { margin: 12, minHeight: 480 },
  sceneSide: { minHeight: 500 },
  stage: { flex: 1, position: 'relative', minHeight: 0, overflow: 'hidden' },
  sceneLayer: { ...StyleSheet.absoluteFillObject, zIndex: 1 },
  camera: { ...StyleSheet.absoluteFillObject, borderRadius: 0 },
  world: { position: 'relative' },
  actorSlot: {
    position: 'absolute',
    width: 72,
    alignItems: 'center',
  },
  floatLayer: {
    ...StyleSheet.absoluteFillObject,
    zIndex: 40000,
  },
  overlayAnchor: {
    position: 'absolute',
    width: 72,
    height: 96,
    alignItems: 'center',
  },
  hud: {
    ...StyleSheet.absoluteFillObject,
    zIndex: 2,
    overflow: 'hidden',
  },
  hudBadge: {
    position: 'absolute',
    minWidth: 110,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 4,
    backgroundColor: 'rgba(44, 95, 111, 0.92)',
    borderRadius: 6,
    paddingHorizontal: 8,
    paddingVertical: 3,
  },
  hudBubble: {
    position: 'absolute',
    minWidth: 110,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: 'rgba(33, 33, 33, 0.92)',
    borderRadius: 8,
    paddingHorizontal: 8,
    paddingVertical: 5,
  },
  hudIcon: {},
  hudBadgeText: {
    color: '#C8F7C5',
    fontWeight: '600',
  },
  hudBubbleText: {
    color: '#fff',
    fontWeight: '600',
  },
  hint: {
    position: 'absolute',
    right: 10,
    bottom: 8,
    fontSize: 10,
    color: 'rgba(255,255,255,0.75)',
    fontWeight: '600',
    zIndex: 40,
  },
});
