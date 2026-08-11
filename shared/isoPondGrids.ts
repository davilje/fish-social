/** @deprecated 使用 pondTileMap（正交 Tilemap）；此文件仅兼容旧 import */
export {
  getPondTileMap,
  getPondIsoGrid,
  buildPondSpotsFromTiles,
  buildPondSpotsFromIso,
  pondTilesInView,
  type PondTileKind as PondCellKind,
  type PondTileTheme as PondGridTheme,
  type PondTileCell as PondGridCell,
  type PondTileMap as PondIsoGrid,
} from './pondTileMap.js';
