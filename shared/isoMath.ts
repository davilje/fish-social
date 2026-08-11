/** 等距 diamond（2:1）投影与深度 */

export const ISO_TILE_W = 48;
export const ISO_TILE_H = 24;

export interface IsoScreenPoint {
  x: number;
  y: number;
}

/** 格子中心 → 屏幕（未加 origin 偏移） */
export function isoToScreen(
  col: number,
  row: number,
  tileW: number = ISO_TILE_W,
  tileH: number = ISO_TILE_H,
): IsoScreenPoint {
  return {
    x: (col - row) * (tileW / 2),
    y: (col + row) * (tileH / 2),
  };
}

/** 屏幕点（相对 origin）→ 近似格子 */
export function screenToIso(
  x: number,
  y: number,
  tileW: number = ISO_TILE_W,
  tileH: number = ISO_TILE_H,
): { col: number; row: number } {
  const a = x / (tileW / 2);
  const b = y / (tileH / 2);
  return {
    col: (a + b) / 2,
    row: (b - a) / 2,
  };
}

export function isoDepth(col: number, row: number): number {
  return col + row;
}

/** 菱形四角（用于包围盒） */
export function isoTileBounds(
  col: number,
  row: number,
  tileW: number = ISO_TILE_W,
  tileH: number = ISO_TILE_H,
): { minX: number; minY: number; maxX: number; maxY: number } {
  const c = isoToScreen(col, row, tileW, tileH);
  return {
    minX: c.x - tileW / 2,
    maxX: c.x + tileW / 2,
    minY: c.y,
    maxY: c.y + tileH,
  };
}
