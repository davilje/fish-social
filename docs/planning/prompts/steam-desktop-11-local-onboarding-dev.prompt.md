# 开发提示词：新手引导本地教学关（STEAM-DESKTOP-11）

你是 Fish Social **Unity Steam 桌面端开发 Agent**（可附带完成态 REST 的最小服务端改动）。按已确认 spec 实现，勿扩需求。

## 必读

1. [`docs/planning/specs/Steam桌面端-新手引导本地教学关.md`](../specs/Steam桌面端-新手引导本地教学关.md)（**已确认** / **STEAM-DESKTOP-11**）
2. [`docs/planning/specs/鱼塘分级与玩家成长.md`](../specs/鱼塘分级与玩家成长.md) §3.4
3. `fish-social-unity/Assets/Scripts/Desktop/Onboarding/`、`OverlayPondStateBuilder.cs`、`DesktopAppBootstrap.cs`
4. Overlay：`desktop-overlay/MainWindow.xaml(.cs)`、`OverlayChatBubblePresenter.cs`、`OverlayPetActor.cs`

## 必须做

1. 未完成引导：Steam 登录后开 Overlay **本地教学关**，**禁止** `ConnectAndJoin(pond-novice)`。
2. 本地状态机：idle → seated → baiting → casting → waiting（5000ms 必上钩）→ hooked（5000ms）→ resolving → 自动入包。
3. Overlay 命令本地处理：`take_spot` / `start_fishing` / `stop_fishing` / `leave_spot` / `confirm_overlay_prompt`。
4. hooked 点「收杆」= 跑鱼回 seated；`exit_pond` 拒绝；`hide_overlay` 忽略。
5. 收鱼后 `POST /api/progress/complete-onboarding`，弹出鱼获窗（确认或 5 秒）→ 「新手引导已完成」确认 → 关 Overlay，开世界地图。
6. 引导气泡贴角色并随换位/离席跟随；去掉 Overlay 顶栏与 Unity 主窗口重复文案。
7. 引导中锁定世界地图等功能栏（主窗口导航 + Overlay 菜单）；设置除外。
8. Overlay 状态按 fishingPhase 显示装饵/抛竿/等待/上钩/收鱼，不得一律「钓鱼」。
9. 设置「重置新手引导」清完成态后重播。

## 不做

- 改正式塘 `FISH_BITE_CHECK_MS` / `rollBiteHook` / skip-casting
- 教学关聊天、Bot、入门竿
- 把 spec 改成已实现（须用户验收）

## 验收

对照 spec §5。

## 派发

```text
@docs/planning/prompts/steam-desktop-11-local-onboarding-dev.prompt.md 按此实现 STEAM-DESKTOP-11
```
