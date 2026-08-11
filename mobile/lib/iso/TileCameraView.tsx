import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  View,
  StyleSheet,
  PanResponder,
  Platform,
  type LayoutChangeEvent,
  type ViewStyle,
  type View as ViewType,
} from 'react-native';

export interface TileCameraViewState {
  scale: number;
  translateX: number;
  translateY: number;
  left: number;
  top: number;
  right: number;
  bottom: number;
  viewportW: number;
  viewportH: number;
}

interface Props {
  worldWidth: number;
  worldHeight: number;
  children: React.ReactNode;
  style?: ViewStyle;
  minScale?: number;
  maxScale?: number;
  focus?: { x: number; y: number };
  /**
   * panable：初始 scale 保证内容至少一边大于视口，便于拖拽（FEAT-SCENE-TILE-2）
   * fit：尽量塞满视口
   */
  initialScaleMode?: 'fit' | 'panable';
  /** 内容小于视口时允许的过冲平移（px） */
  overscrollPad?: number;
  onViewChange?: (view: TileCameraViewState) => void;
  /** 短按世界坐标（已换算）；拖拽超过阈值不触发 */
  onTapWorld?: (worldX: number, worldY: number) => void;
}

const TAP_SLOP = 8;

type Point = { x: number; y: number };

/** 正交 Tilemap 相机：视口客户区坐标统一用于缩放/命中/HUD（FEAT-SCENE-TILE-4） */
export function TileCameraView({
  worldWidth,
  worldHeight,
  children,
  style,
  minScale = 0.35,
  maxScale = 2.2,
  focus,
  initialScaleMode = 'panable',
  overscrollPad = 64,
  onViewChange,
  onTapWorld,
}: Props) {
  const [viewport, setViewport] = useState({ w: 320, h: 280 });
  const [scale, setScale] = useState(1);
  const [translate, setTranslate] = useState({ x: 0, y: 0 });
  const rootRef = useRef<ViewType | null>(null);
  const viewportRef = useRef(viewport);
  const scaleRef = useRef(scale);
  const translateRef = useRef(translate);
  /** 视口左上角在窗口中的位置（与 layout 同单位，非设备像素） */
  const originRef = useRef<Point>({ x: 0, y: 0 });
  const pinchRef = useRef<{ dist: number; scale: number } | null>(null);
  const dragRef = useRef<{ x: number; y: number; tx: number; ty: number } | null>(null);
  const tapRef = useRef<Point | null>(null);
  const rafRef = useRef<number | null>(null);
  const pendingViewRef = useRef<TileCameraViewState | null>(null);
  const onViewChangeRef = useRef(onViewChange);
  const onTapWorldRef = useRef(onTapWorld);

  scaleRef.current = scale;
  translateRef.current = translate;
  viewportRef.current = viewport;
  onViewChangeRef.current = onViewChange;
  onTapWorldRef.current = onTapWorld;

  const syncOrigin = useCallback(() => {
    const node = rootRef.current as unknown as {
      measureInWindow?: (cb: (x: number, y: number) => void) => void;
      getBoundingClientRect?: () => { left: number; top: number };
      _nativeNode?: { getBoundingClientRect?: () => { left: number; top: number } };
    } | null;
    if (!node) return;
    // Web：同步取 CSS 布局矩形，避免 measureInWindow 异步导致滚轮 pivot 错位
    if (Platform.OS === 'web') {
      const el = node.getBoundingClientRect
        ? node
        : node._nativeNode?.getBoundingClientRect
          ? node._nativeNode
          : null;
      if (el?.getBoundingClientRect) {
        const r = el.getBoundingClientRect();
        originRef.current = { x: r.left, y: r.top };
        return;
      }
    }
    if (typeof node.measureInWindow === 'function') {
      node.measureInWindow((x, y) => {
        originRef.current = { x, y };
      });
    }
  }, []);

  const pageToViewport = useCallback((pageX: number, pageY: number): Point => {
    return {
      x: pageX - originRef.current.x,
      y: pageY - originRef.current.y,
    };
  }, []);

  const clampTranslate = useCallback(
    (tx: number, ty: number, s: number, vw: number, vh: number) => {
      const contentW = worldWidth * s;
      const contentH = worldHeight * s;
      const pad = overscrollPad;

      let minX: number;
      let maxX: number;
      let minY: number;
      let maxY: number;

      if (contentW <= vw) {
        const centerX = (vw - contentW) / 2;
        minX = centerX - pad;
        maxX = centerX + pad;
      } else {
        minX = vw - contentW;
        maxX = 0;
      }

      if (contentH <= vh) {
        const centerY = (vh - contentH) / 2;
        minY = centerY - pad;
        maxY = centerY + pad;
      } else {
        minY = vh - contentH;
        maxY = 0;
      }

      return {
        x: Math.min(maxX, Math.max(minX, tx)),
        y: Math.min(maxY, Math.max(minY, ty)),
      };
    },
    [worldWidth, worldHeight, overscrollPad],
  );

  const flushView = useCallback(() => {
    rafRef.current = null;
    const pending = pendingViewRef.current;
    if (!pending || !onViewChangeRef.current) return;
    pendingViewRef.current = null;
    onViewChangeRef.current(pending);
  }, []);

  const scheduleEmitView = useCallback(
    (s: number, tx: number, ty: number, vw: number, vh: number) => {
      if (!onViewChangeRef.current) return;
      pendingViewRef.current = {
        scale: s,
        translateX: tx,
        translateY: ty,
        left: -tx / s,
        top: -ty / s,
        right: (-tx + vw) / s,
        bottom: (-ty + vh) / s,
        viewportW: vw,
        viewportH: vh,
      };
      if (rafRef.current != null) return;
      if (typeof requestAnimationFrame === 'function') {
        rafRef.current = requestAnimationFrame(flushView);
      } else {
        flushView();
      }
    },
    [flushView],
  );

  const emitViewNow = useCallback(
    (s: number, tx: number, ty: number, vw: number, vh: number) => {
      if (!onViewChangeRef.current) return;
      onViewChangeRef.current({
        scale: s,
        translateX: tx,
        translateY: ty,
        left: -tx / s,
        top: -ty / s,
        right: (-tx + vw) / s,
        bottom: (-ty + vh) / s,
        viewportW: vw,
        viewportH: vh,
      });
    },
    [],
  );

  const centerOn = useCallback(
    (fx: number, fy: number, s: number, vw: number, vh: number) => {
      const tx = vw / 2 - fx * s;
      const ty = vh / 2 - fy * s;
      return clampTranslate(tx, ty, s, vw, vh);
    },
    [clampTranslate],
  );

  const pickInitialScale = useCallback(
    (vw: number, vh: number) => {
      const fit = Math.min(vw / worldWidth, vh / worldHeight);
      if (initialScaleMode === 'fit') {
        return Math.min(maxScale, Math.max(minScale, fit * 1.02));
      }
      // 全屏后按真实视口重算：至少一边略大于视口，便于拖拽
      const coverH = (vh / worldHeight) * 1.18;
      const coverW = (vw / worldWidth) * 1.18;
      const panable = Math.max(coverH, coverW, fit * 1.12);
      return Math.min(maxScale, Math.max(minScale, panable));
    },
    [worldWidth, worldHeight, initialScaleMode, minScale, maxScale],
  );

  useEffect(() => {
    const { w, h } = viewport;
    if (w <= 0 || h <= 0) return;
    const s0 = pickInitialScale(w, h);
    const fx = focus?.x ?? worldWidth / 2;
    const fy = focus?.y ?? worldHeight / 2;
    const t = centerOn(fx, fy, s0, w, h);
    setScale(s0);
    setTranslate(t);
    emitViewNow(s0, t.x, t.y, w, h);
    syncOrigin();
  }, [worldWidth, worldHeight, viewport.w, viewport.h, pickInitialScale]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(
    () => () => {
      if (rafRef.current != null && typeof cancelAnimationFrame === 'function') {
        cancelAnimationFrame(rafRef.current);
      }
    },
    [],
  );

  const applyZoom = useCallback(
    (nextScale: number, pivotX: number, pivotY: number) => {
      const s0 = scaleRef.current;
      const t0 = translateRef.current;
      const vw = viewportRef.current.w;
      const vh = viewportRef.current.h;
      const s1 = Math.min(maxScale, Math.max(minScale, nextScale));
      // pivot 必须是视口客户区坐标（与 translate 同一原点）
      const wx = (pivotX - t0.x) / s0;
      const wy = (pivotY - t0.y) / s0;
      const tx = pivotX - wx * s1;
      const ty = pivotY - wy * s1;
      const t = clampTranslate(tx, ty, s1, vw, vh);
      setScale(s1);
      setTranslate(t);
      scheduleEmitView(s1, t.x, t.y, vw, vh);
    },
    [clampTranslate, scheduleEmitView, maxScale, minScale],
  );

  const panResponder = useMemo(
    () =>
      PanResponder.create({
        onStartShouldSetPanResponder: () => !!onTapWorldRef.current,
        onMoveShouldSetPanResponder: (_, g) => Math.abs(g.dx) > 4 || Math.abs(g.dy) > 4,
        onPanResponderGrant: (e) => {
          syncOrigin();
          const touches = e.nativeEvent.touches;
          const local = pageToViewport(e.nativeEvent.pageX, e.nativeEvent.pageY);
          tapRef.current = local;
          if (touches && touches.length >= 2) {
            const t0 = touches[0]!;
            const t1 = touches[1]!;
            const dist = Math.hypot(t1.pageX - t0.pageX, t1.pageY - t0.pageY);
            pinchRef.current = { dist: Math.max(dist, 1), scale: scaleRef.current };
            dragRef.current = null;
            return;
          }
          pinchRef.current = null;
          dragRef.current = {
            x: e.nativeEvent.pageX,
            y: e.nativeEvent.pageY,
            tx: translateRef.current.x,
            ty: translateRef.current.y,
          };
        },
        onPanResponderMove: (e, g) => {
          const touches = e.nativeEvent.touches;
          if (pinchRef.current && touches && touches.length >= 2) {
            const t0 = touches[0]!;
            const t1 = touches[1]!;
            const dist = Math.hypot(t1.pageX - t0.pageX, t1.pageY - t0.pageY);
            const ratio = dist / pinchRef.current.dist;
            const mid = pageToViewport((t0.pageX + t1.pageX) / 2, (t0.pageY + t1.pageY) / 2);
            applyZoom(pinchRef.current.scale * ratio, mid.x, mid.y);
            return;
          }
          if (!dragRef.current) return;
          const vw = viewportRef.current.w;
          const vh = viewportRef.current.h;
          const t = clampTranslate(
            dragRef.current.tx + g.dx,
            dragRef.current.ty + g.dy,
            scaleRef.current,
            vw,
            vh,
          );
          setTranslate(t);
          scheduleEmitView(scaleRef.current, t.x, t.y, vw, vh);
        },
        onPanResponderRelease: (_, g) => {
          const tap = tapRef.current;
          dragRef.current = null;
          pinchRef.current = null;
          tapRef.current = null;
          if (
            tap &&
            onTapWorldRef.current &&
            Math.abs(g.dx) < TAP_SLOP &&
            Math.abs(g.dy) < TAP_SLOP
          ) {
            const s = scaleRef.current;
            const t = translateRef.current;
            const worldX = (tap.x - t.x) / s;
            const worldY = (tap.y - t.y) / s;
            onTapWorldRef.current(worldX, worldY);
          }
        },
        onPanResponderTerminate: () => {
          dragRef.current = null;
          pinchRef.current = null;
          tapRef.current = null;
        },
      }),
    [applyZoom, clampTranslate, pageToViewport, scheduleEmitView, syncOrigin],
  );

  const onLayout = (e: LayoutChangeEvent) => {
    const { width, height } = e.nativeEvent.layout;
    setViewport({ w: width, h: height });
    syncOrigin();
  };

  const wheelProps =
    Platform.OS === 'web'
      ? ({
          onWheel: (ev: {
            preventDefault?: () => void;
            deltaY?: number;
            clientX?: number;
            clientY?: number;
            currentTarget?: { getBoundingClientRect?: () => { left: number; top: number } };
            nativeEvent?: {
              deltaY?: number;
              clientX?: number;
              clientY?: number;
              pageX?: number;
              pageY?: number;
              offsetX?: number;
              offsetY?: number;
            };
          }) => {
            ev.preventDefault?.();
            const ne = ev.nativeEvent ?? {};
            const deltaY = ne.deltaY ?? ev.deltaY ?? 0;
            const factor = deltaY > 0 ? 0.9 : 1.1;
            const clientX = ne.clientX ?? ev.clientX;
            const clientY = ne.clientY ?? ev.clientY;
            const rect = ev.currentTarget?.getBoundingClientRect?.();
            let pivot: Point;
            if (
              rect &&
              typeof clientX === 'number' &&
              typeof clientY === 'number'
            ) {
              // 视口客户区坐标 = 指针相对 layout 矩形（与 onLayout 宽高同单位）
              pivot = { x: clientX - rect.left, y: clientY - rect.top };
              originRef.current = { x: rect.left, y: rect.top };
            } else if (typeof ne.offsetX === 'number' && typeof ne.offsetY === 'number') {
              pivot = { x: ne.offsetX, y: ne.offsetY };
            } else {
              syncOrigin();
              const pageX = ne.pageX ?? clientX;
              const pageY = ne.pageY ?? clientY;
              pivot =
                typeof pageX === 'number' && typeof pageY === 'number'
                  ? pageToViewport(pageX, pageY)
                  : { x: viewportRef.current.w / 2, y: viewportRef.current.h / 2 };
            }
            applyZoom(scaleRef.current * factor, pivot.x, pivot.y);
          },
        } as Record<string, unknown>)
      : {};

  return (
    <View
      ref={rootRef}
      style={[styles.viewport, style]}
      onLayout={onLayout}
      {...panResponder.panHandlers}
      {...wheelProps}
    >
      <View
        pointerEvents="box-none"
        style={{
          width: worldWidth,
          height: worldHeight,
          transform: [{ translateX: translate.x }, { translateY: translate.y }, { scale }],
          // 与 screen = world * scale + translate 对齐（原点左上）
          transformOrigin: '0 0' as unknown as string,
        }}
      >
        {children}
      </View>
    </View>
  );
}

/** @deprecated 使用 TileCameraView */
export const IsoCameraView = TileCameraView;

const styles = StyleSheet.create({
  viewport: {
    flex: 1,
    overflow: 'hidden',
    backgroundColor: '#2E4A2C',
    borderRadius: 0,
    minHeight: 0,
    cursor: 'grab' as unknown as undefined,
  },
});
