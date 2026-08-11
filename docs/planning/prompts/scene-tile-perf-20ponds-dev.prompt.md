# 开发提示词：Tilemap 性能与二十塘扩容（FEAT-SCENE-TILE-3）

你是 Fish Social **前端为主** Agent（可改 `mobile/`、`shared/`；服务端仅在扩 `PONDS` 后冒烟/调 bot 上限时动）。按规格优化性能并扩到 20 非矩形塘。

## 必读

1. `docs/planning/specs/Tilemap性能与二十塘扩容.md`（**已确认** / **FEAT-SCENE-TILE-3**）
2. 卡顿根因：每格 View/Pressable + 拖拽每帧 `emitView` 全量重建  
3. `WorldMapView` · `TileLayer` · `TileCameraView` · `worldTileMap` · `pondTileMap` · `ponds.ts`

## 顺序

1. **性能**：世界改为 Canvas/Skia/合并色块等低节点方案；单点 hit-test；拖拽节流；去掉每格 Pressable  
2. **去图标**：世界层剔除 🎣/钓点入口图标  
3. **扩图**：世界边长 > 96；布局容纳 20 塘  
4. **20 塘**：`PONDS` + 世界 footprint + 塘内 tile；外形 **非**单矩形；每塘 20 spot  
5. 控制 bot/生态压力；`verify:server-boot`；Web 拖缩与进塘钓一条  

## 非目标

Unity；Tiled 完整管线；改协议事件名。

## 验收

对照 spec §3；完成后 `planning-progress-sync` Checklist B → **已实现**。

## 派发

```text
@docs/planning/prompts/scene-tile-perf-20ponds-dev.prompt.md 按此实现 FEAT-SCENE-TILE-3
```

建议角色：`@frontend-dev`（主）· 扩塘后 `@backend-dev` 旁观 bot/生态负载。
