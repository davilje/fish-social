# 开发提示词：Tilemap 相机拖拽与 HUD 分层（FEAT-SCENE-TILE-2）

你是 Fish Social **前端 Agent**（改 `mobile/`、`shared/worldTileMap`；不改 server 玩法）。按规格修复拖拽并完成扩图/热区/HUD。

## 必读

1. `docs/planning/specs/Tilemap相机拖拽与HUD分层.md`（**已确认** / **FEAT-SCENE-TILE-2**）
2. `mobile/lib/iso/TileCameraView.tsx` · `PondScene.tsx` · `WorldMapView.tsx` · `shared/worldTileMap.ts`

## 顺序

1. **A** 修 `clampTranslate` / 进塘初始 scale，保证塘内可上下拖  
2. **B** 去掉 `PondScene` 误导性 `dock`  
3. **C** 世界边长扩到 ≥96，重排四塘矩形  
4. **D** 凡带 `pondId` 的格点击 → 进塘弹窗  
5. **E** Scene / HUD 分层；文字随 scale 动态可读  
6. Web 自检：拖缩、整塘点击、钓一条  

## 非目标

改协议/FSM；Tiled 导入；Unity。

## 验收

对照 spec §4；完成后 `planning-progress-sync` Checklist B → **已实现**。

## 派发

```text
@docs/planning/prompts/scene-tile-camera-hud-dev.prompt.md 按此实现 FEAT-SCENE-TILE-2
```

建议角色：`@frontend-dev`
