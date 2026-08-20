# 开发提示词：钓位点位线索文字泡（FEAT-SPOT-01）

你是 Fish Social **Unity Overlay 为主、后端等级校验为辅**的开发 Agent。按规格做 R1 **薄实现**，勿扩需求。

## 必读

1. `docs/planning/specs/钓位点位线索文字泡.md`（**已确认** / **FEAT-SPOT-01**）
2. `docs/planning/specs/鱼塘分级与玩家成长.md`（玩家/塘熟练度）
3. Overlay 聊天气泡组件（09C）可复用样式

## 顺序

1. 读 `spot_clues` 表；按 `minPlayerLevel` + 当前塘熟练度过滤。
2. Overlay 钓位旁环境线索泡（仅本玩家可见）；未达标默认不显示。
3. 换塘/升级后刷新；文案占位即可。
4. 不泄露 bite/escape 原始数值。
5. verify / 自检。

## 不做

- 独立夜钓/时段系统、主窗观察面板、最终文案/美术锁定

## 验收

对照 spec §3；完成后回写计划表 **已实现** + `npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/feat-spot-01-spot-clue-bubbles-dev.prompt.md 按此实现 FEAT-SPOT-01
```

建议角色：Unity Overlay / `@frontend-dev`（`fish-social-unity/`）+ 必要时 `@backend-dev` 下发进度。
