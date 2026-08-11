# 开发提示词：星露谷式正交 Tilemap（FEAT-SCENE-TILE-1）

你是 Fish Social **前端 Agent**（改 `mobile/`、`shared/` 表现层；**不改** server 玩法）。按规格把假等距圆角图纠偏为正交密铺 Tilemap。

## 必读

1. `docs/planning/specs/星露谷式正交Tilemap场景.md`（**已确认** / **FEAT-SCENE-TILE-1**）
2. 现状：`WorldMapView.tsx` · `PondScene.tsx` · `shared/isoWorldGrid.ts` · `shared/isoMath.ts` · `IsoCameraView.tsx`

## 顺序

1. 新增 `shared/tileMath.ts` + `TileLayer`（方格、`borderRadius: 0`、视口 culling）  
2. 重做世界稠密图：矩形陆地/水面 + `pond_entry`；去掉椭圆稀疏主路径  
3. `PondScene` 同套正交 tile；20× `*-spot-*` 锚格中心；去圆角码头主视觉  
4. 相机改正交 bounds；视口无圆角  
5. Web 自检：拖缩、进塘弹窗、连服钓一条  

## 非目标

Tiled 编辑器导入；真 diamond 等距；Unity；改 FSM。

## 验收

对照 spec §5；完成后 `planning-progress-sync` Checklist B → **已实现**。

## 派发

```text
@docs/planning/prompts/scene-ortho-tilemap-dev.prompt.md 按此实现 FEAT-SCENE-TILE-1
```

建议角色：`@frontend-dev`
