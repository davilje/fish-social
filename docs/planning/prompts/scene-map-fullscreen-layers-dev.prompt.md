# 开发提示词：世界地图全屏与层级缩放（FEAT-SCENE-TILE-4）

你是 Fish Social **前端 Agent**。按规格修好缩放坐标系，并做成「顶栏 / HUD / 地图」三层全屏布局。

## 必读

1. `docs/planning/specs/世界地图全屏与层级缩放.md`（**已确认** / **FEAT-SCENE-TILE-4**）
2. `mobile/app/index.tsx` · `WorldMapView.tsx` · `TileCameraView.tsx` · `MergedTileLayer`（若有）

## 顺序

1. 查缩放：viewport layout vs 绘制缓冲 vs HUD 投影是否同一原点；修滚轮/捏合 pivot 与 DPR  
2. `index`：顶栏 L0 置顶；其下 `MapStage` flex:1 全屏  
3. `WorldMapView`：L2 场景 absoluteFill；L1 HUD 同区域且**不画进顶栏**；去掉挤占高度的页脚/重复大标题  
4. 全屏后重算初始 `panable` scale；自检拖缩点击对齐  

## 非目标

改 20 塘数据；Unity；重做顶栏按钮。

## 验收

对照 spec §4；完成后 `planning-progress-sync` Checklist B → **已实现**。

## 派发

```text
@docs/planning/prompts/scene-map-fullscreen-layers-dev.prompt.md 按此实现 FEAT-SCENE-TILE-4
```

建议角色：`@frontend-dev`
