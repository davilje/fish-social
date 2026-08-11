import { TILE_SIZE } from './tileMath.js';

/** 同色轴对齐合并矩形（用于 SVG/Canvas 低节点绘制） */
export interface MergedTileRect {
  col: number;
  row: number;
  cols: number;
  rows: number;
  color: string;
}

/**
 * 将稠密 color 网格合并为水平 run，再纵向合并相同 run。
 * skipColor：整底色可跳过（外层画一大块底即可）。
 */
export function mergeColorGrid(
  colors: string[],
  gridCols: number,
  gridRows: number,
  skipColor?: string,
): MergedTileRect[] {
  type Run = { col: number; row: number; cols: number; color: string };
  const runs: Run[] = [];

  for (let row = 0; row < gridRows; row++) {
    let col = 0;
    while (col < gridCols) {
      const color = colors[row * gridCols + col]!;
      if (skipColor && color === skipColor) {
        col += 1;
        continue;
      }
      let end = col + 1;
      while (end < gridCols && colors[row * gridCols + end] === color) end += 1;
      runs.push({ col, row, cols: end - col, color });
      col = end;
    }
  }

  const used = new Array(runs.length).fill(false);
  const out: MergedTileRect[] = [];

  for (let i = 0; i < runs.length; i++) {
    if (used[i]) continue;
    const base = runs[i]!;
    used[i] = true;
    let rows = 1;
    let nextRow = base.row + 1;
    while (nextRow < gridRows) {
      const j = runs.findIndex(
        (r, idx) =>
          !used[idx] &&
          r.row === nextRow &&
          r.col === base.col &&
          r.cols === base.cols &&
          r.color === base.color,
      );
      if (j < 0) break;
      used[j] = true;
      rows += 1;
      nextRow += 1;
    }
    out.push({
      col: base.col,
      row: base.row,
      cols: base.cols,
      rows,
      color: base.color,
    });
  }

  return out;
}

export function mergedRectPixels(
  r: MergedTileRect,
  tileSize: number = TILE_SIZE,
  seam: boolean = true,
): { x: number; y: number; width: number; height: number } {
  const gap = seam ? 1 : 0;
  return {
    x: r.col * tileSize,
    y: r.row * tileSize,
    width: Math.max(1, r.cols * tileSize - gap),
    height: Math.max(1, r.rows * tileSize - gap),
  };
}
