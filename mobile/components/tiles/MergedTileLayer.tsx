import { memo, useMemo } from 'react';
import { View, StyleSheet } from 'react-native';
import Svg, { Rect } from 'react-native-svg';
import { TILE_SIZE, type MergedTileRect, mergedRectPixels } from '@fish-social/shared';

interface Props {
  rects: MergedTileRect[];
  worldWidth: number;
  worldHeight: number;
  /** 整底色（世界 void）；不传则不铺底 */
  backgroundColor?: string;
  tileSize?: number;
  seam?: boolean;
}

/** 低节点正交色块层：合并矩形 + SVG，禁止每格 View/Pressable */
function MergedTileLayerInner({
  rects,
  worldWidth,
  worldHeight,
  backgroundColor,
  tileSize = TILE_SIZE,
  seam = true,
}: Props) {
  const nodes = useMemo(
    () =>
      rects.map((r, i) => {
        const p = mergedRectPixels(r, tileSize, seam);
        return (
          <Rect
            key={`${r.col},${r.row},${r.cols},${r.rows},${i}`}
            x={p.x}
            y={p.y}
            width={p.width}
            height={p.height}
            fill={r.color}
          />
        );
      }),
    [rects, tileSize, seam],
  );

  return (
    <View style={[styles.wrap, { width: worldWidth, height: worldHeight }]} pointerEvents="none">
      <Svg width={worldWidth} height={worldHeight}>
        {backgroundColor ? (
          <Rect x={0} y={0} width={worldWidth} height={worldHeight} fill={backgroundColor} />
        ) : null}
        {nodes}
      </Svg>
    </View>
  );
}

export const MergedTileLayer = memo(MergedTileLayerInner);

const styles = StyleSheet.create({
  wrap: {
    position: 'absolute',
    left: 0,
    top: 0,
    borderRadius: 0,
  },
});
