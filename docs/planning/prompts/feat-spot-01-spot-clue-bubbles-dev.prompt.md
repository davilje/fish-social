# 开发提示词：钓位点位线索文字泡（FEAT-SPOT-01）

你是 Fish Social **Unity Overlay 为主、数值表 + 可选后端为辅**的开发 Agent。按**修订后**规格实现，勿扩需求。

## 必读

1. `docs/planning/specs/钓位点位线索文字泡.md`（**已确认** / **FEAT-SPOT-01**，含 2026-08-22 修订）
2. `docs/planning/specs/鱼塘分级与玩家成长.md`（玩家/塘熟练度）
3. `docs/planning/specs/Steam桌面端-09COverlay鱼塘聊天气泡与输入.md`（气泡样式）

## 顺序

1. 数值表：将旧「一点一文案」改为 `spot_clue_texts`（+ 可选 `spot_tags`），录入 spec §4 种子；`game-data:export`。
2. **删除** Overlay 钓位旁常驻线索字。
3. 玩家**坐席**后：按等级/塘熟练度/塘类/点位 tag 过滤，加权随机抽 1 条；用 **09C 聊天气泡样式**仅对本玩家展示（系统昵称如「观察」），不广播、不进公屏历史。
4. 离席收起；换点重抽；同会话避免连抽同一 `clueId`（简单去重即可）。
5. 池须能出 `habitat` 与 `activity` 两类；不泄露 bite/escape。
6. 若已有旧实现：按本修订重做验收；`verify:feat-spot-01` 若有则更新。

## 不做

- 夜钓/时段系统、主窗观察面板、点位旁常驻字

## 派发

```text
@docs/planning/prompts/feat-spot-01-spot-clue-bubbles-dev.prompt.md 按修订后规格实现 FEAT-SPOT-01
```

建议角色：Unity Overlay + 数值表导出；必要时 `@backend-dev`。
