# 开发提示词：头顶状态图标与 actor-ring（STEAM-DESKTOP-14E）

你是 Fish Social 的 **Overlay / Unity 桌面**工程师。按 14E 修订 14D，勿扩需求。

## 必读

1. `docs/planning/specs/Steam桌面端-14EOverlay头顶状态图标与进度环.md`（**已实现** / **STEAM-DESKTOP-14E**）
2. 14D 套猫圆环、等鱼 `fishing.png` **废止**
3. 预估：`OverlayPetActor.ResolveStatusIconKind`、Baker `actor-ring-bg`、`OverlayResources/status/ring-bg.png`

## 顺序

1. 图标：仅 hooked / groundbaiting；等鱼无 icon。
2. 环 + ring-bg 跟 actor-ring 槽，上钩与打窝都用 `hookDeadlineMs` 进度；idle 无环。
3. 布局 JSON / Prefab / Baker 导出 `actor-ring-bg`，与 21 塘一致。
4. 不恢复头顶相位字，不把环套回猫身。

代码若已合入，对照 §验收补缺口。

## 验收

对照 spec §验收；完成后按 Skill `planning-progress-sync`：
spec→**已实现** + `build-master-plan-xlsx.py` 完成时间 + `npm run planning:master-xlsx`。

- [ ] 等鱼无图标无环；上钩/打窝头顶环+对应图标
- [ ] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/planning/prompts/steam-desktop-14e-overhead-status-ring-dev.prompt.md 按此实现 STEAM-DESKTOP-14E
```
