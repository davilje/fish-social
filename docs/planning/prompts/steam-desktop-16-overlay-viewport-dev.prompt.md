# 开发提示词：Overlay 窗口视口缩放（STEAM-DESKTOP-16）

**状态：已实现（2026-08-31 验收）。** 本文件结案归档，勿再按此新开实现。

你是 Fish Social 的 **Overlay / Unity 桌面工程师**。按规格实现视口裁切，**禁止**整窗 Scale/Viewbox。勿扩需求。

## 必读

1. `docs/planning/specs/Steam桌面端-16Overlay窗口视口缩放.md`（**已实现** / **STEAM-DESKTOP-16**）§4 实现分析
2. 已实现：09D 窗口 960×560；13A 渐隐跟视口；14B 横移钳制；ART-03 HUD JSON 仍 960×560
3. 预估文件：
   - `desktop-overlay/MainWindow.xaml`、`MainWindow.xaml.cs`（产品右键「窗口大小」）
   - `desktop-overlay/OverlayHudLayout.cs`（视口重锚，JSON 仍校验 960×560）
   - `desktop-overlay/OverlayScenePan.cs`（`ViewportWidth` 改为当前视口宽）
   - `desktop-overlay/OverlayEdgeVignette.cs`、`OverlayPondLayout.cs`

## 顺序

1. 产品菜单加三档；改 `Window.Width/Height`；`Top` 钉底边，`Left` 按中线内收。
2. 场景层 `ClipToBounds`：`TranslateY = -(sceneH - viewH)` 只切顶；猫/座位 1:1 不缩放。
3. HUD 按 spec 锚点 Relayout；600 宽先缩 `dock_chat` 避免叠钓鱼栏；小档菜单轨默认收起。
4. 13A `ApplySize(viewW, viewH)`；14B 钳制改用 `viewW`；回归点击穿透。
5. 抽查：猫仍 64×64，按钮逻辑尺寸不缩小。不要改 Unity HUD 导出器，不要改 `mobile/`/`server/`/`shared/`。

## 验收

对照 spec §验收；完成后按 Skill `planning-progress-sync`：
spec→**已实现** + `build-master-plan-xlsx.py` 完成时间 + `npm run planning:master-xlsx`。

- [x] 三档尺寸与底边/中线锚点
- [x] 无整窗比例缩放
- [x] 小窗 HUD 仍在窗内
- [x] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/planning/prompts/steam-desktop-16-overlay-viewport-dev.prompt.md 按此实现 STEAM-DESKTOP-16
```
