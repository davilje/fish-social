# 开发提示词：等距网格世界与鱼塘场景（FEAT-SCENE-ISO-1 / ISO-2）

你是 Fish Social **前端 Agent**（可改 `mobile/`、`shared/` 表现配置；**原则上不改** `server/` 玩法）。按规格实现，勿扩需求、勿开 Unity。

## 必读

1. `docs/planning/specs/等距网格世界与鱼塘场景.md`（**已确认**）
2. `docs/planning/product/钓鱼世界与鱼塘场景优化策略.md`（REF-SCENE-1，体验参照）
3. `mobile/components/PondScene.tsx` · `WorldMapView.tsx` · `shared/ponds.ts`

## 顺序（必须）

1. **FEAT-SCENE-ISO-1**：`isoMath` + 相机（平移/缩放）→ 塘内 `land/water/fish_spot` 网格 → 20 岸格绑定现有 `*-spot-1..20` → 替换椭圆布局；`y` 排序；连服钓一条  
2. **FEAT-SCENE-ISO-2**：复用相机/投影 → 世界大网格 + 四陆地板块 + `pond_entry` → 弹窗进入 → 视口 culling  
3. 自检 Web 拖/缩与进塘闭环  

## 非目标

Unity；真 3D；世界地图 80 坐席；改 FSM/生态/人数上限。

## 验收

对照 spec §5（ISO-1 与 ISO-2 分节勾选）。完成后按 `planning-progress-sync` Checklist B 分别或一并回写 **已实现**。

## 派发

```text
@docs/planning/prompts/scene-iso-grid-dev.prompt.md 按此实现 FEAT-SCENE-ISO-1 与 FEAT-SCENE-ISO-2
```

建议角色：`@frontend-dev`（主）· 后端仅在必须改 shared 类型时旁观。
