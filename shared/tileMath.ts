/** 正交俯视 Tilemap（星露谷式） */

export const TILE_SIZE = 32;

export interface TilePoint {
  x: number;
  y: number;
}

/** 格左上角 → 屏幕 */
export function tileToScreen(col: number, row: number, tileSize: number = TILE_SIZE): TilePoint {
  return { x: col * tileSize, y: row * tileSize };
}

/** 格中心 → 屏幕 */
export function tileCenter(col: number, row: number, tileSize: number = TILE_SIZE): TilePoint {
  return {
    x: col * tileSize + tileSize / 2,
    y: row * tileSize + tileSize / 2,
  };
}

export function screenToTile(
  x: number,
  y: number,
  tileSize: number = TILE_SIZE,
): { col: number; row: number } {
  return {
    col: Math.floor(x / tileSize),
    row: Math.floor(y / tileSize),
  };
}

/** 绘制深度：同排 row 优先，再 col（俯视时通常自上而下） */
export function tileDepth(col: number, row: number): number {
  return row * 1000 + col;
}

export function tileViewRange(
  view: { left: number; top: number; right: number; bottom: number },
  cols: number,
  rows: number,
  tileSize: number = TILE_SIZE,
  marginTiles: number = 1,
): { col0: number; row0: number; col1: number; row1: number } {
  const col0 = Math.max(0, Math.floor(view.left / tileSize) - marginTiles);
  const row0 = Math.max(0, Math.floor(view.top / tileSize) - marginTiles);
  const col1 = Math.min(cols - 1, Math.ceil(view.right / tileSize) + marginTiles);
  const row1 = Math.min(rows - 1, Math.ceil(view.bottom / tileSize) + marginTiles);
  return { col0, row0, col1, row1 };
}
