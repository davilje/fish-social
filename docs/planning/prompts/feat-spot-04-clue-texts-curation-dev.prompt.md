# 开发提示词：线索文案策划审校（FEAT-SPOT-04）

你是 Fish Social **策划 + 数值表** Agent。扩写 `spot_clue_texts`，严格遵守 habitat/activity 文案禁区。

## 必读

1. `docs/planning/specs/线索文案策划审校.md`（**已确认**）
2. `docs/planning/specs/钓位标签与线索库-v2.md` §4.2 禁区
3. `shared/spotClues.ts` → `validateSpotClueWording`（verify 用）

## 顺序

1. 推荐在 **FEAT-SPOT-03** 标签定稿后进行
2. 增删 xlsx **`spot_clue_texts`** 或批量改 `scripts/game-data/spot_clue_data.py` 后 `game-data:build`
3. 每 tag 目标 ≥10 条；activity 必填 `activitySignal`
4. `npm run game-data:export`
5. `npm run verify:feat-spot-01`（零禁区告警 + 覆盖度）

## 不做

- `activitySignal` 运行时过滤（FEAT-SPOT-05）
- 一点一条固定文案

## 验收后策划

`build-master-plan-xlsx.py` → **已实现** + `npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/feat-spot-04-clue-texts-curation-dev.prompt.md 按此完成 FEAT-SPOT-04 文案审校
```

建议：`@producer` 主笔；`@backend-dev` export/verify。
