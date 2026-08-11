import { memo } from 'react';
import { View, Pressable, Text, StyleSheet } from 'react-native';
import { TILE_SIZE, tileToScreen } from '@fish-social/shared';

export interface TileLayerItem {
  col: number;
  row: number;
  color: string;
  depth: number;
  /** 可点（如 pond_entry） */
  onPress?: () => void;
  label?: string;
  kind?: string;
}

interface Props {
  tiles: TileLayerItem[];
  tileSize?: number;
  /** 1px 格缝 */
  seam?: boolean;
}

/** 正交硬边方格层：禁止圆角 */
function TileLayerInner({ tiles, tileSize = TILE_SIZE, seam = true }: Props) {
  return (
    <>
      {tiles.map((t) => {
        const p = tileToScreen(t.col, t.row, tileSize);
        const size = seam ? tileSize - 1 : tileSize;
        const common = {
          left: p.x,
          top: p.y,
          width: size,
          height: size,
          backgroundColor: t.color,
          zIndex: t.depth,
        };
        if (t.onPress) {
          return (
            <Pressable
              key={`${t.col},${t.row}`}
              onPress={t.onPress}
              delayPressIn={40}
              style={[styles.tile, common, t.kind === 'pond_entry' && styles.entry]}
            >
              {t.label ? <Text style={styles.label}>{t.label}</Text> : null}
            </Pressable>
          );
        }
        return <View key={`${t.col},${t.row}`} style={[styles.tile, common]} />;
      })}
    </>
  );
}

export const TileLayer = memo(TileLayerInner);

const styles = StyleSheet.create({
  tile: {
    position: 'absolute',
    borderRadius: 0,
  },
  entry: {
    borderWidth: 1,
    borderColor: '#FFFFFF',
    alignItems: 'center',
    justifyContent: 'center',
    cursor: 'pointer',
  },
  label: {
    fontSize: 11,
    lineHeight: 12,
  },
});
