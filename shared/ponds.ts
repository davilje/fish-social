import type { PondConfig, WorldPondRegion } from './types';
import { POND_CATALOG } from './pondCatalog';
import { buildPondSpotsFromTiles, getPondTileMap } from './pondTileMap';

/** 世界地图上的鱼塘区域（遗留矩形元数据；主入口为 Tile 热区） */
export const WORLD_POND_REGIONS: WorldPondRegion[] = POND_CATALOG.map((p, i) => ({
  id: p.regionId,
  name: p.name,
  x: 40 + (i % 5) * 90,
  y: 60 + Math.floor(i / 5) * 90,
  width: 80,
  height: 70,
  color: p.palette.water,
}));

/** 各鱼塘配置：钓位坐标来自正交 Tile 岸格中心 */
export const PONDS: PondConfig[] = POND_CATALOG.map((p) => ({
  id: p.id,
  name: p.name,
  regionId: p.regionId,
  spots: buildPondSpotsFromTiles(p.id),
}));

const calmGrid = getPondTileMap('pond-calm');
export const POND_SCENE_CENTER = { ...calmGrid.waterCenter };

export function getPondById(pondId: string): PondConfig | undefined {
  return PONDS.find((p) => p.id === pondId);
}

export function getPondByRegionId(regionId: string): PondConfig | undefined {
  return PONDS.find((p) => p.regionId === regionId);
}

export function formatPondName(pondId: string | null | undefined): string {
  if (!pondId) return '—';
  return getPondById(pondId)?.name ?? pondId;
}

export function formatSpotName(
  spotId: string | null | undefined,
  pondId?: string | null,
): string {
  if (!spotId) return '—';
  const pond =
    (pondId ? getPondById(pondId) : undefined) ??
    PONDS.find((p) => p.spots.some((s) => s.id === spotId));
  const short = pond?.name ?? '钓位';
  const m = /(?:^|-)spot-(\d+)$/i.exec(spotId) ?? /-(\d+)$/.exec(spotId);
  if (m) return `${short}·${Number(m[1])}号位`;
  return `${short}·${spotId}`;
}
