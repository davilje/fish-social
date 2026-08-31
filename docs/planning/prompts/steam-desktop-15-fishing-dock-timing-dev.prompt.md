# 开发提示词：Overlay 中央钓鱼栏按钮时序（STEAM-DESKTOP-15）

**状态：已实现（2026-08-31 验收）。** 本文件结案归档，勿再按此新开实现。

你是 Fish Social 的 **Unity 桌面 / Overlay 工程师**，并改一处服务端 `leave_spot` 门禁。按规格实现，勿扩需求。

## 必读

1. `docs/planning/specs/Steam桌面端-15Overlay钓鱼栏按钮时序.md`（**已实现** / **STEAM-DESKTOP-15**）
2. 已实现口径：`STEAM-DESKTOP-08G`、`STEAM-DESKTOP-13C`、`FEAT-GROUND-01`（打窝经济不变；离席可打断打窝覆盖「等完」）
3. 预估文件：
   - `desktop-overlay/MainWindow.xaml.cs`（`ApplyFishingControls`、`ApplyGroundbaitStatus`）
   - `fish-social-unity/Assets/Scripts/Desktop/OverlayPondStateBuilder.cs`（`MapAvailableActions`）
   - `fish-social-unity/Assets/Scripts/Desktop/Auth/SocialPondSessionController.cs`
   - `server/src/pondSession.ts`（`leaveSpot` 放行 `groundbaiting`）

## 顺序

1. Overlay：有座时主按钮不 `Collapsed`；显隐按 spec 相位表，去掉 `stack>0` 保活打窝钮。
2. Unity：`availableActions` 与相位表对齐——`stopping`/`resolving` 不要发 `leave_spot` / `groundbait_start`；`groundbaiting` 发 `leave_spot`、不发 `start_fishing`。
3. 服务端：`leave_spot` 允许 `groundbaiting`，取消未完成 cast，不叠层，离席清窝。
4. 乐观点击只禁用，不提前藏主按钮、不提前出侧钮。
5. 自检：未打窝开钓、打窝后再开钓、打窝中点开钓/离席、点收杆等到 seated。

## 验收

对照 spec §验收；完成后按 Skill `planning-progress-sync`：
spec→**已实现** + `build-master-plan-xlsx.py` 完成时间 + `npm run planning:master-xlsx`。

- [x] 打窝后再开钓，打窝钮消失
- [x] 收杆后侧钮等到 phase 结束再与「开始钓鱼」文案同时出现
- [x] 打窝中离席立即生效
- [x] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/planning/prompts/steam-desktop-15-fishing-dock-timing-dev.prompt.md 按此实现 STEAM-DESKTOP-15
```
