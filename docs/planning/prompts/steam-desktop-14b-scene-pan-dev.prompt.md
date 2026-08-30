# 开发提示词：Overlay 横轴场景平移（STEAM-DESKTOP-14B）

你是 Fish Social 的 **Unity 桌面 / Overlay 工程师**。视口保持 960×560；只平移场景层。P1：无加宽底图则无法验收平移距离。

## 必读

1. `docs/planning/specs/Steam桌面端-14BOverlay横轴场景平移.md`（**已实现** / **STEAM-DESKTOP-14B**）
2. `docs/planning/specs/Steam桌面端-14Overlay透明点击穿透回归.md`、`Steam桌面端-13AOverlay场景边缘半透明渐隐.md`
3. `OverlayLayoutExporter.cs`、`PondScenePresenter.cs`、`MainWindow.xaml`

## 顺序

1. 导出器按 Prefab 实际宽度写 `canvas.width`（默认场景 1920×560）。
2. 场景根 TranslateTransform.X，钳制不越界；HUD 与 13A mask 跟视口。
3. 左右箭头长按平滑平移（约 240～360 px/s）；窄塘隐藏箭头。
4. 点箭头不 DragMove 窗口。自检 spec §5。

## 验收

对照 spec §验收。完成后按 Skill `planning-progress-sync` Checklist B：**须用户确认** 后再把 spec 改为已实现并刷新计划表。

```text
@docs/planning/prompts/steam-desktop-14b-scene-pan-dev.prompt.md 按此实现 STEAM-DESKTOP-14B
```
