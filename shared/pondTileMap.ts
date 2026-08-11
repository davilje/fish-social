import { POND_CATALOG, type PondPalette } from './pondCatalog.js';
import { TILE_SIZE, tileCenter, tileDepth, tileViewRange } from './tileMath.js';
import { mergeColorGrid, type MergedTileRect } from './tileMerge.js';
import type { FishingSpot } from './types.js';

export type PondTileTheme = Pick<PondPalette, 'grass' | 'dirt' | 'water' | 'shore' | 'fishSpot'>;

export type PondTileKind = 'grass' | 'dirt' | 'water' | 'shore' | 'fish_spot';

export interface PondTileCell {
  col: number;
  row: number;
  kind: PondTileKind;
  spotId?: string;
  color: string;
  depth: number;
}

export interface PondTileMap {
  pondId: string;
  cols: number;
  rows: number;
  /** 稠密：cols*rows */
  cells: PondTileCell[];
  theme: PondTileTheme;
  worldWidth: number;
  worldHeight: number;
  waterCenter: { x: number; y: number };
  spotById: Record<string, { col: number; row: number; x: number; y: number }>;
  mergedRects: MergedTileRect[];
}

/** FEAT-SCENE-TILE-3：略放大以便辨识 20 岸位 */
const GRID = 28;
const CHAR_W = 72;

type WaterRect = { c0: number; r0: number; c1: number; r1: number };

/** 每塘水面：多矩形拼合（禁止单矩形） */
const WATER_BY_INDEX: WaterRect[][] = [
  [
    { c0: 7, r0: 7, c1: 16, r1: 14 },
    { c0: 10, r0: 12, c1: 19, r1: 19 },
  ],
  [
    { c0: 6, r0: 8, c1: 18, r1: 13 },
    { c0: 9, r0: 6, c1: 16, r1: 18 },
  ],
  [
    { c0: 6, r0: 6, c1: 13, r1: 16 },
    { c0: 12, r0: 10, c1: 20, r1: 18 },
  ],
  [
    { c0: 10, r0: 5, c1: 15, r1: 20 },
    { c0: 7, r0: 9, c1: 18, r1: 16 },
  ],
  [
    { c0: 5, r0: 8, c1: 12, r1: 18 },
    { c0: 11, r0: 6, c1: 20, r1: 12 },
    { c0: 14, r0: 11, c1: 21, r1: 19 },
  ],
  [
    { c0: 8, r0: 5, c1: 18, r1: 10 },
    { c0: 6, r0: 9, c1: 12, r1: 19 },
    { c0: 12, r0: 12, c1: 20, r1: 20 },
  ],
  [
    { c0: 7, r0: 7, c1: 14, r1: 14 },
    { c0: 12, r0: 9, c1: 19, r1: 17 },
    { c0: 9, r0: 15, c1: 16, r1: 21 },
  ],
  [
    { c0: 5, r0: 10, c1: 20, r1: 15 },
    { c0: 9, r0: 5, c1: 15, r1: 12 },
    { c0: 9, r0: 14, c1: 15, r1: 21 },
  ],
  [
    { c0: 6, r0: 6, c1: 11, r1: 18 },
    { c0: 10, r0: 8, c1: 19, r1: 13 },
    { c0: 14, r0: 12, c1: 21, r1: 20 },
  ],
  [
    { c0: 8, r0: 6, c1: 18, r1: 11 },
    { c0: 5, r0: 10, c1: 12, r1: 19 },
    { c0: 13, r0: 13, c1: 20, r1: 20 },
  ],
  [
    { c0: 7, r0: 5, c1: 16, r1: 12 },
    { c0: 11, r0: 10, c1: 20, r1: 19 },
  ],
  [
    { c0: 5, r0: 7, c1: 14, r1: 14 },
    { c0: 12, r0: 9, c1: 21, r1: 18 },
  ],
  [
    { c0: 9, r0: 5, c1: 14, r1: 20 },
    { c0: 5, r0: 9, c1: 19, r1: 14 },
  ],
  [
    { c0: 6, r0: 6, c1: 12, r1: 16 },
    { c0: 11, r0: 8, c1: 18, r1: 14 },
    { c0: 15, r0: 12, c1: 21, r1: 20 },
  ],
  [
    { c0: 7, r0: 8, c1: 19, r1: 13 },
    { c0: 8, r0: 5, c1: 15, r1: 10 },
    { c0: 10, r0: 12, c1: 17, r1: 20 },
  ],
  [
    { c0: 5, r0: 5, c1: 15, r1: 10 },
    { c0: 5, r0: 9, c1: 10, r1: 19 },
    { c0: 12, r0: 12, c1: 20, r1: 20 },
  ],
  [
    { c0: 8, r0: 6, c1: 18, r1: 16 },
    { c0: 5, r0: 10, c1: 11, r1: 18 },
  ],
  [
    { c0: 6, r0: 7, c1: 13, r1: 18 },
    { c0: 12, r0: 5, c1: 20, r1: 12 },
    { c0: 14, r0: 11, c1: 21, r1: 19 },
  ],
  [
    { c0: 7, r0: 6, c1: 17, r1: 11 },
    { c0: 6, r0: 10, c1: 12, r1: 19 },
    { c0: 13, r0: 13, c1: 20, r1: 20 },
  ],
  [
    { c0: 5, r0: 8, c1: 18, r1: 14 },
    { c0: 8, r0: 5, c1: 14, r1: 10 },
    { c0: 10, r0: 13, c1: 16, r1: 21 },
  ],
];

function inRects(col: number, row: number, rects: WaterRect[]): boolean {
  return rects.some((r) => col >= r.c0 && col <= r.c1 && row >= r.r0 && row <= r.r1);
}

function neighbors4(col: number, row: number): Array<[number, number]> {
  return [
    [col - 1, row],
    [col + 1, row],
    [col, row - 1],
    [col, row + 1],
  ];
}

function themeFromCatalog(pondId: string): PondTileTheme {
  const e = POND_CATALOG.find((p) => p.id === pondId) ?? POND_CATALOG[0]!;
  const p = e.palette;
  return {
    grass: p.grass,
    dirt: p.dirt,
    water: p.water,
    shore: p.shore,
    fishSpot: p.fishSpot,
  };
}

function prefixOf(pondId: string): string {
  return POND_CATALOG.find((p) => p.id === pondId)?.prefix ?? pondId.replace(/^pond-/, '');
}

function waterRectsFor(pondId: string): WaterRect[] {
  const i = POND_CATALOG.findIndex((p) => p.id === pondId);
  return WATER_BY_INDEX[i >= 0 ? i % WATER_BY_INDEX.length : 0]!;
}

function buildOne(pondId: string): PondTileMap {
  const theme = themeFromCatalog(pondId);
  const prefix = prefixOf(pondId);
  const rects = waterRectsFor(pondId);

  const water = new Set<string>();
  let wSumC = 0;
  let wSumR = 0;
  for (let row = 0; row < GRID; row++) {
    for (let col = 0; col < GRID; col++) {
      if (inRects(col, row, rects)) {
        water.add(`${col},${row}`);
        wSumC += col;
        wSumR += row;
      }
    }
  }
  const wCount = Math.max(1, water.size);
  const cx = wSumC / wCount;
  const cy = wSumR / wCount;

  const shore: Array<{ col: number; row: number; angle: number }> = [];
  for (let row = 0; row < GRID; row++) {
    for (let col = 0; col < GRID; col++) {
      const key = `${col},${row}`;
      if (water.has(key)) continue;
      if (!neighbors4(col, row).some(([c, r]) => water.has(`${c},${r}`))) continue;
      shore.push({ col, row, angle: Math.atan2(row - cy, col - cx) });
    }
  }
  shore.sort((a, b) => a.angle - b.angle);

  const spotKeys = new Set<string>();
  const spotList: Array<{ col: number; row: number; spotId: string }> = [];
  for (let i = 0; i < 20 && shore.length > 0; i++) {
    const idx = Math.min(shore.length - 1, Math.floor((i * shore.length) / 20));
    let picked: (typeof shore)[0] | null = null;
    for (let k = 0; k < shore.length; k++) {
      const cand = shore[(idx + k) % shore.length]!;
      const k2 = `${cand.col},${cand.row}`;
      if (!spotKeys.has(k2)) {
        picked = cand;
        break;
      }
    }
    if (!picked) break;
    spotKeys.add(`${picked.col},${picked.row}`);
    spotList.push({ col: picked.col, row: picked.row, spotId: `${prefix}-spot-${i + 1}` });
  }

  let expand = 1;
  while (spotList.length < 20 && expand < 12) {
    for (let row = 0; row < GRID && spotList.length < 20; row++) {
      for (let col = 0; col < GRID && spotList.length < 20; col++) {
        const key = `${col},${row}`;
        if (water.has(key) || spotKeys.has(key)) continue;
        let near = false;
        for (const wk of water) {
          const [wc, wr] = wk.split(',').map(Number);
          if (Math.abs(wc! - col) + Math.abs(wr! - row) <= expand) {
            near = true;
            break;
          }
        }
        if (!near) continue;
        spotKeys.add(key);
        spotList.push({ col, row, spotId: `${prefix}-spot-${spotList.length + 1}` });
      }
    }
    expand += 1;
  }

  const spotAt = new Map(spotList.map((s) => [`${s.col},${s.row}`, s.spotId]));
  const cells: PondTileCell[] = [];
  const spotById: PondTileMap['spotById'] = {};
  const colors: string[] = [];

  for (let row = 0; row < GRID; row++) {
    for (let col = 0; col < GRID; col++) {
      const key = `${col},${row}`;
      let kind: PondTileKind = 'grass';
      let spotId: string | undefined;
      if (water.has(key)) kind = 'water';
      else if (spotAt.has(key)) {
        kind = 'fish_spot';
        spotId = spotAt.get(key);
      } else if (neighbors4(col, row).some(([c, r]) => water.has(`${c},${r}`))) {
        kind = 'shore';
      } else if (col <= 1 || row <= 1 || col >= GRID - 2 || row >= GRID - 2) {
        kind = 'dirt';
      }

      const color =
        kind === 'water'
          ? theme.water
          : kind === 'fish_spot'
            ? theme.fishSpot
            : kind === 'shore'
              ? theme.shore
              : kind === 'dirt'
                ? theme.dirt
                : theme.grass;

      colors.push(color);
      cells.push({
        col,
        row,
        kind,
        spotId,
        color,
        depth: tileDepth(col, row),
      });

      if (spotId) {
        const c = tileCenter(col, row);
        spotById[spotId] = {
          col,
          row,
          x: Math.round(c.x - CHAR_W / 2),
          y: Math.round(c.y - 40),
        };
      }
    }
  }

  const wc = tileCenter(Math.round(cx), Math.round(cy));
  return {
    pondId,
    cols: GRID,
    rows: GRID,
    cells,
    theme,
    worldWidth: GRID * TILE_SIZE,
    worldHeight: GRID * TILE_SIZE,
    waterCenter: { x: wc.x, y: wc.y },
    spotById,
    mergedRects: mergeColorGrid(colors, GRID, GRID),
  };
}

const cache = new Map<string, PondTileMap>();

export function clearPondTileMapCache(): void {
  cache.clear();
}

export function getPondTileMap(pondId: string): PondTileMap {
  let g = cache.get(pondId);
  if (!g) {
    g = buildOne(pondId);
    cache.set(pondId, g);
  }
  return g;
}

/** @deprecated 兼容旧名 */
export function getPondIsoGrid(pondId: string): PondTileMap {
  return getPondTileMap(pondId);
}

export function buildPondSpotsFromTiles(pondId: string): FishingSpot[] {
  const g = getPondTileMap(pondId);
  const prefix = prefixOf(pondId);
  const spots: FishingSpot[] = [];
  for (let i = 1; i <= 20; i++) {
    const id = `${prefix}-spot-${i}`;
    const s = g.spotById[id];
    if (s) spots.push({ id, x: s.x, y: s.y });
  }
  return spots;
}

/** @deprecated */
export function buildPondSpotsFromIso(pondId: string): FishingSpot[] {
  return buildPondSpotsFromTiles(pondId);
}

export function pondTilesInView(
  map: PondTileMap,
  view: { left: number; top: number; right: number; bottom: number },
): PondTileCell[] {
  const { col0, row0, col1, row1 } = tileViewRange(view, map.cols, map.rows);
  return map.cells.filter((c) => c.col >= col0 && c.col <= col1 && c.row >= row0 && c.row <= row1);
}

export function pondSpotIdAt(map: PondTileMap, col: number, row: number): string | undefined {
  if (col < 0 || row < 0 || col >= map.cols || row >= map.rows) return undefined;
  return map.cells[row * map.cols + col]?.spotId;
}
