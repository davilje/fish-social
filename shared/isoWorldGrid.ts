/** @deprecated 使用 worldTileMap（正交稠密图）；此文件仅兼容旧 import */
export {
  getWorldTileMap,
  getWorldIsoGrid,
  worldTilesInView,
  worldCellsInView,
  type WorldTileKind,
  type WorldTileCell as WorldGridCell,
  type WorldTileMap as WorldIsoGrid,
} from './worldTileMap.js';
