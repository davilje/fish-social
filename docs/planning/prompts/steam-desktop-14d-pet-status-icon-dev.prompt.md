# 开发提示词：Overlay 宠物状态图标与上钩圆环（STEAM-DESKTOP-14D）

你是 Fish Social 的 **Unity 桌面 / Overlay 工程师**。取消头顶相位文字；时长仍只在 09B 悬停。

## 必读

1. `docs/planning/specs/Steam桌面端-14DOverlay宠物状态图标与上钩圆环.md`（**已确认** / **STEAM-DESKTOP-14D**）
2. `docs/planning/specs/Steam桌面端-09DOverlay布局与角色表现优化.md`、`Steam桌面端-09BOverlay悬停状态与钓鱼时长.md`
3. `desktop-overlay/OverlayPetActor.cs`

## 顺序

1. 去掉常驻「钓鱼中/上钩」等头顶字徽章。
2. 16～20px icon：`OverlayResources/status/fishing.png`、`hooked.png`（缺图占位）。
3. 上钩圆环与 64×64 猫身同心，直径约 72～80；不要放在 18px 矮行。
4. 悬停热区仍为猫身。自检 spec §5。

## 验收

对照 spec §验收。完成后按 Skill `planning-progress-sync` Checklist B：**须用户确认** 后再把 spec 改为已实现并刷新计划表。

```text
@docs/planning/prompts/steam-desktop-14d-pet-status-icon-dev.prompt.md 按此实现 STEAM-DESKTOP-14D
```
