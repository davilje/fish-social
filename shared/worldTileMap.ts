import { POND_CATALOG } from './pondCatalog.js';
import { TILE_SIZE, tileCenter, tileDepth, tileViewRange } from './tileMath.js';
import { mergeColorGrid, type MergedTileRect } from './tileMerge.js';

export type WorldTileKind = 'void' | 'grass' | 'dirt' | 'water' | 'shore' | 'pond_entry';

export interface WorldTileCell {
  col: number;
  row: number;
  kind: WorldTileKind;
  pondId?: string;
  color: string;
  depth: number;
}

export interface WorldPondLabel {
  pondId: string;
  /** 陆地中心（世界坐标） */
  x: number;
  y: number;
  col: number;
  row: number;
}

export interface WorldTileMap {
  cols: number;
  rows: number;
  /** 稠密 kind 数组 row-major：index = row * cols + col */
  kinds: WorldTileKind[];
  pondIds: (string | undefined)[];
  colors: string[];
  worldWidth: number;
  worldHeight: number;
  entries: Array<{ pondId: string; col: number; row: number; x: number; y: number }>;
  /** HUD 塘名锚点 */
  labels: WorldPondLabel[];
  /** 预合并色块（跳过 void 底色） */
  mergedRects: MergedTileRect[];
  voidColor: string;
}

/** FEAT-SCENE-TILE-3：边长 144（> TILE-2 的 96） */
const WORLD = 144;

type Rect = { c0: number; r0: number; c1: number; r1: number };

/** 相对 cell 原点的不规则陆地（≥2 矩形拼合） */
const LAND_SHAPES: Rect[][] = [
  [
    { c0: 0, r0: 0, c1: 16, r1: 20 },
    { c0: 0, r0: 14, c1: 22, r1: 22 },
  ],
  [
    { c0: 0, r0: 0, c1: 22, r1: 8 },
    { c0: 6, r0: 8, c1: 16, r1: 22 },
  ],
  [
    { c0: 0, r0: 0, c1: 6, r1: 20 },
    { c0: 16, r0: 0, c1: 22, r1: 20 },
    { c0: 0, r0: 16, c1: 22, r1: 22 },
  ],
  [
    { c0: 0, r0: 0, c1: 12, r1: 10 },
    { c0: 6, r0: 8, c1: 18, r1: 16 },
    { c0: 12, r0: 14, c1: 22, r1: 22 },
  ],
  [
    { c0: 8, r0: 0, c1: 14, r1: 22 },
    { c0: 0, r0: 8, c1: 22, r1: 14 },
  ],
  [
    { c0: 0, r0: 0, c1: 18, r1: 6 },
    { c0: 0, r0: 6, c1: 6, r1: 16 },
    { c0: 0, r0: 16, c1: 18, r1: 22 },
  ],
  [
    { c0: 0, r0: 0, c1: 14, r1: 8 },
    { c0: 8, r0: 6, c1: 22, r1: 14 },
    { c0: 0, r0: 12, c1: 14, r1: 22 },
  ],
  [
    { c0: 4, r0: 0, c1: 22, r1: 10 },
    { c0: 0, r0: 8, c1: 12, r1: 22 },
  ],
  [
    { c0: 0, r0: 4, c1: 10, r1: 22 },
    { c0: 8, r0: 0, c1: 22, r1: 12 },
    { c0: 14, r0: 10, c1: 22, r1: 22 },
  ],
  [
    { c0: 0, r0: 0, c1: 22, r1: 6 },
    { c0: 0, r0: 6, c1: 8, r1: 22 },
    { c0: 14, r0: 10, c1: 22, r1: 22 },
  ],
  [
    { c0: 2, r0: 0, c1: 20, r1: 8 },
    { c0: 0, r0: 6, c1: 10, r1: 18 },
    { c0: 12, r0: 10, c1: 22, r1: 22 },
  ],
  [
    { c0: 0, r0: 0, c1: 10, r1: 22 },
    { c0: 8, r0: 0, c1: 22, r1: 8 },
    { c0: 8, r0: 14, c1: 22, r1: 22 },
  ],
  [
    { c0: 0, r0: 8, c1: 22, r1: 16 },
    { c0: 4, r0: 0, c1: 12, r1: 10 },
    { c0: 10, r0: 14, c1: 18, r1: 22 },
  ],
  [
    { c0: 0, r0: 0, c1: 16, r1: 12 },
    { c0: 10, r0: 8, c1: 22, r1: 22 },
  ],
  [
    { c0: 6, r0: 0, c1: 22, r1: 14 },
    { c0: 0, r0: 10, c1: 14, r1: 22 },
  ],
  [
    { c0: 0, r0: 0, c1: 8, r1: 22 },
    { c0: 6, r0: 4, c1: 16, r1: 12 },
    { c0: 12, r0: 10, c1: 22, r1: 22 },
  ],
  [
    { c0: 0, r0: 0, c1: 22, r1: 10 },
    { c0: 0, r0: 8, c1: 10, r1: 22 },
    { c0: 12, r0: 14, c1: 22, r1: 22 },
  ],
  [
    { c0: 4, r0: 0, c1: 18, r1: 22 },
    { c0: 0, r0: 6, c1: 8, r1: 16 },
    { c0: 14, r0: 6, c1: 22, r1: 16 },
  ],
  [
    { c0: 0, r0: 0, c1: 12, r1: 16 },
    { c0: 8, r0: 10, c1: 22, r1: 22 },
    { c0: 16, r0: 0, c1: 22, r1: 12 },
  ],
  [
    { c0: 0, r0: 4, c1: 22, r1: 12 },
    { c0: 0, r0: 10, c1: 8, r1: 22 },
    { c0: 14, r0: 0, c1: 22, r1: 18 },
  ],
];

/** 相对水面：多矩形，禁止单矩形 */
const WATER_SHAPES: Rect[][] = [
  [
    { c0: 4, r0: 4, c1: 12, r1: 12 },
    { c0: 8, r0: 8, c1: 16, r1: 16 },
  ],
  [
    { c0: 6, r0: 3, c1: 16, r1: 8 },
    { c0: 8, r0: 7, c1: 14, r1: 16 },
  ],
  [
    { c0: 2, r0: 4, c1: 6, r1: 14 },
    { c0: 14, r0: 4, c1: 18, r1: 14 },
    { c0: 4, r0: 12, c1: 16, r1: 16 },
  ],
  [
    { c0: 3, r0: 3, c1: 10, r1: 9 },
    { c0: 8, r0: 7, c1: 15, r1: 14 },
    { c0: 12, r0: 12, c1: 18, r1: 18 },
  ],
  [
    { c0: 9, r0: 3, c1: 13, r1: 18 },
    { c0: 3, r0: 9, c1: 18, r1: 13 },
  ],
  [
    { c0: 3, r0: 2, c1: 14, r1: 6 },
    { c0: 2, r0: 5, c1: 6, r1: 14 },
    { c0: 3, r0: 13, c1: 14, r1: 17 },
  ],
  [
    { c0: 3, r0: 3, c1: 11, r1: 8 },
    { c0: 9, r0: 6, c1: 17, r1: 12 },
    { c0: 3, r0: 10, c1: 11, r1: 16 },
  ],
  [
    { c0: 6, r0: 2, c1: 16, r1: 8 },
    { c0: 2, r0: 7, c1: 10, r1: 16 },
  ],
  [
    { c0: 2, r0: 6, c1: 8, r1: 16 },
    { c0: 8, r0: 2, c1: 16, r1: 10 },
    { c0: 12, r0: 9, c1: 18, r1: 16 },
  ],
  [
    { c0: 3, r0: 2, c1: 16, r1: 6 },
    { c0: 2, r0: 5, c1: 7, r1: 16 },
    { c0: 12, r0: 9, c1: 18, r1: 16 },
  ],
  [
    { c0: 4, r0: 2, c1: 14, r1: 7 },
    { c0: 2, r0: 6, c1: 8, r1: 14 },
    { c0: 10, r0: 9, c1: 17, r1: 16 },
  ],
  [
    { c0: 2, r0: 3, c1: 8, r1: 16 },
    { c0: 7, r0: 2, c1: 16, r1: 7 },
    { c0: 7, r0: 12, c1: 16, r1: 17 },
  ],
  [
    { c0: 3, r0: 8, c1: 17, r1: 13 },
    { c0: 5, r0: 2, c1: 11, r1: 9 },
    { c0: 10, r0: 12, c1: 16, r1: 17 },
  ],
  [
    { c0: 3, r0: 3, c1: 12, r1: 10 },
    { c0: 9, r0: 8, c1: 17, r1: 16 },
  ],
  [
    { c0: 7, r0: 2, c1: 16, r1: 11 },
    { c0: 2, r0: 9, c1: 12, r1: 16 },
  ],
  [
    { c0: 2, r0: 3, c1: 7, r1: 16 },
    { c0: 6, r0: 5, c1: 13, r1: 11 },
    { c0: 11, r0: 9, c1: 17, r1: 16 },
  ],
  [
    { c0: 3, r0: 2, c1: 16, r1: 8 },
    { c0: 2, r0: 7, c1: 8, r1: 16 },
    { c0: 11, r0: 12, c1: 17, r1: 17 },
  ],
  [
    { c0: 6, r0: 3, c1: 14, r1: 16 },
    { c0: 2, r0: 7, c1: 7, r1: 13 },
    { c0: 14, r0: 7, c1: 18, r1: 13 },
  ],
  [
    { c0: 2, r0: 2, c1: 10, r1: 12 },
    { c0: 8, r0: 9, c1: 17, r1: 17 },
    { c0: 14, r0: 2, c1: 18, r1: 10 },
  ],
  [
    { c0: 2, r0: 5, c1: 16, r1: 10 },
    { c0: 2, r0: 9, c1: 7, r1: 16 },
    { c0: 12, r0: 2, c1: 18, r1: 14 },
  ],
];

const VOID_COLOR = '#2E4A2C';
const CELL = 27;
const OX = 3;
const OY = 3;
const COLS_P = 5;

let cached: WorldTileMap | null = null;

function idx(col: number, row: number): number {
  return row * WORLD + col;
}

function inRect(col: number, row: number, r: Rect): boolean {
  return col >= r.c0 && col <= r.c1 && row >= r.r0 && row <= r.r1;
}

function inRects(col: number, row: number, rects: Rect[]): boolean {
  return rects.some((r) => inRect(col, row, r));
}

function offsetRects(rects: Rect[], ox: number, oy: number): Rect[] {
  return rects.map((r) => ({
    c0: r.c0 + ox,
    r0: r.r0 + oy,
    c1: r.c1 + ox,
    r1: r.r1 + oy,
  }));
}

function isWaterShore(col: number, row: number, kinds: WorldTileKind[]): boolean {
  if (kinds[idx(col, row)] === 'water') return false;
  const n = [
    [col - 1, row],
    [col + 1, row],
    [col, row - 1],
    [col, row + 1],
  ];
  return n.some(([c, r]) => {
    if (c < 0 || r < 0 || c >= WORLD || r >= WORLD) return false;
    return kinds[idx(c, r)] === 'water';
  });
}

function landBounds(rects: Rect[]): Rect {
  return {
    c0: Math.min(...rects.map((r) => r.c0)),
    r0: Math.min(...rects.map((r) => r.r0)),
    c1: Math.max(...rects.map((r) => r.c1)),
    r1: Math.max(...rects.map((r) => r.r1)),
  };
}

function isLandEdge(col: number, row: number, land: Rect[]): boolean {
  if (!inRects(col, row, land)) return false;
  const n = [
    [col - 1, row],
    [col + 1, row],
    [col, row - 1],
    [col, row + 1],
  ];
  return n.some(([c, r]) => !inRects(c, r, land));
}

/** 测试/热重载可清缓存 */
export function clearWorldTileMapCache(): void {
  cached = null;
}

export function getWorldTileMap(): WorldTileMap {
  if (cached) return cached;

  const kinds: WorldTileKind[] = Array(WORLD * WORLD).fill('void');
  const pondIds: (string | undefined)[] = Array(WORLD * WORLD).fill(undefined);
  const colors: string[] = Array(WORLD * WORLD).fill(VOID_COLOR);
  const entries: WorldTileMap['entries'] = [];
  const labels: WorldPondLabel[] = [];

  if (POND_CATALOG.length !== 20) {
    throw new Error(`POND_CATALOG must have 20 ponds, got ${POND_CATALOG.length}`);
  }

  for (let i = 0; i < POND_CATALOG.length; i++) {
    const entry = POND_CATALOG[i]!;
    const gx = i % COLS_P;
    const gy = Math.floor(i / COLS_P);
    const ox = OX + gx * CELL;
    const oy = OY + gy * CELL;
    const land = offsetRects(LAND_SHAPES[i % LAND_SHAPES.length]!, ox, oy);
    const water = offsetRects(WATER_SHAPES[i % WATER_SHAPES.length]!, ox, oy);
    const pal = entry.palette;
    const bounds = landBounds(land);

    for (const r of land) {
      for (let row = r.r0; row <= r.r1; row++) {
        for (let col = r.c0; col <= r.c1; col++) {
          if (col < 0 || row < 0 || col >= WORLD || row >= WORLD) continue;
          const j = idx(col, row);
          kinds[j] = isLandEdge(col, row, land) ? 'dirt' : 'grass';
          pondIds[j] = entry.id;
          colors[j] = kinds[j] === 'dirt' ? pal.dirt : pal.grass;
        }
      }
    }

    for (const r of water) {
      for (let row = r.r0; row <= r.r1; row++) {
        for (let col = r.c0; col <= r.c1; col++) {
          if (!inRects(col, row, land)) continue;
          const j = idx(col, row);
          kinds[j] = 'water';
          pondIds[j] = entry.id;
          colors[j] = pal.water;
        }
      }
    }

    const midCol = Math.floor((bounds.c0 + bounds.c1) / 2);
    const midRow = Math.floor((bounds.r0 + bounds.r1) / 2);
    const mid = tileCenter(midCol, midRow);
    labels.push({ pondId: entry.id, x: mid.x, y: mid.y, col: midCol, row: midRow });

    // 入口格：岸边一点（深蓝区分，无图标）
    let entryCol = midCol;
    let entryRow = bounds.r1;
    for (let row = bounds.r1; row >= bounds.r0; row--) {
      for (let col = bounds.c0; col <= bounds.c1; col++) {
        const j = idx(col, row);
        if (kinds[j] === 'grass' || kinds[j] === 'dirt') {
          entryCol = col;
          entryRow = row;
          break;
        }
      }
      if (kinds[idx(entryCol, entryRow)] === 'grass' || kinds[idx(entryCol, entryRow)] === 'dirt') {
        break;
      }
    }
    const ei = idx(entryCol, entryRow);
    kinds[ei] = 'pond_entry';
    pondIds[ei] = entry.id;
    colors[ei] = pal.entry;
    const ec = tileCenter(entryCol, entryRow);
    entries.push({ pondId: entry.id, col: entryCol, row: entryRow, x: ec.x, y: ec.y });
  }

  for (let row = 0; row < WORLD; row++) {
    for (let col = 0; col < WORLD; col++) {
      const i = idx(col, row);
      if (kinds[i] === 'void' || kinds[i] === 'water' || kinds[i] === 'pond_entry') continue;
      if (!isWaterShore(col, row, kinds)) continue;
      const pid = pondIds[i];
      const pal = POND_CATALOG.find((p) => p.id === pid)?.palette;
      kinds[i] = 'shore';
      colors[i] = pal?.shore ?? '#8D6E63';
    }
  }

  const mergedRects = mergeColorGrid(colors, WORLD, WORLD, VOID_COLOR);

  cached = {
    cols: WORLD,
    rows: WORLD,
    kinds,
    pondIds,
    colors,
    worldWidth: WORLD * TILE_SIZE,
    worldHeight: WORLD * TILE_SIZE,
    entries,
    labels,
    mergedRects,
    voidColor: VOID_COLOR,
  };
  return cached;
}

export function worldPondIdAt(map: WorldTileMap, col: number, row: number): string | undefined {
  if (col < 0 || row < 0 || col >= map.cols || row >= map.rows) return undefined;
  return map.pondIds[row * map.cols + col];
}

export function worldTilesInView(
  map: WorldTileMap,
  view: { left: number; top: number; right: number; bottom: number },
): WorldTileCell[] {
  const { col0, row0, col1, row1 } = tileViewRange(view, map.cols, map.rows);
  const out: WorldTileCell[] = [];
  for (let row = row0; row <= row1; row++) {
    for (let col = col0; col <= col1; col++) {
      const i = row * map.cols + col;
      out.push({
        col,
        row,
        kind: map.kinds[i]!,
        pondId: map.pondIds[i],
        color: map.colors[i]!,
        depth: tileDepth(col, row),
      });
    }
  }
  return out;
}

/** @deprecated */
export function getWorldIsoGrid(): WorldTileMap {
  return getWorldTileMap();
}

/** @deprecated */
export function worldCellsInView(
  map: WorldTileMap,
  view: { left: number; top: number; right: number; bottom: number },
): WorldTileCell[] {
  return worldTilesInView(map, view);
}
