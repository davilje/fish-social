# 开发提示词：Overlay 透明点击穿透回归（STEAM-DESKTOP-14）

你是 Fish Social 的 **Unity 桌面 / Overlay 工程师**。只修 Overlay 命中；**禁止拆掉 13A 四边 OpacityMask 渐隐**。

## 必读

1. `docs/planning/specs/Steam桌面端-14Overlay透明点击穿透回归.md`（**已实现** / **STEAM-DESKTOP-14**）
2. `docs/planning/specs/Steam桌面端-13AOverlay场景边缘半透明渐隐.md`
3. `desktop-overlay/MainWindow.xaml(.cs)`、`OverlayEdgeVignette.cs`

## 顺序

1. 去掉 `SceneFadeHost` / `SceneContentCanvas` 的 `#01000000` 整层填充，改为真正 Transparent。
2. `WM_NCHITTEST`：命中猫/座位/HUD/可见塘图 → `HTCLIENT`；合成透明处 → `HTTRANSPARENT`。
3. 保留 `OverlayEdgeVignette` 40px Absolute mask；HUD 不参与渐隐。
4. 自检 spec §6。

## 验收

对照 spec §验收。完成后按 Skill `planning-progress-sync` Checklist B：**须用户确认** 后再把 spec 改为已实现并刷新计划表。

```text
@docs/planning/prompts/steam-desktop-14-click-through-dev.prompt.md 按此实现 STEAM-DESKTOP-14
```
