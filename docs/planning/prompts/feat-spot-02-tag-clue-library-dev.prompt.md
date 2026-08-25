# 开发提示词：钓位标签与线索库 v2（FEAT-SPOT-02）

你是 Fish Social **数值表 + shared + Unity Overlay + 服务端** 开发 Agent。本票在 FEAT-SPOT-01 上扩展标签体系与合规线索库；**v1 不按 activitySignal 过滤**（生态 v2 再接）。

## 必读

1. `docs/planning/specs/钓位标签与线索库-v2.md`（**已实现** / **FEAT-SPOT-02**）
2. `docs/planning/specs/钓位点位线索文字泡.md`（FEAT-SPOT-01 表现基线）
3. `docs/planning/specs/鱼塘分级与玩家成长.md`（体长 XP、`fish_xp`）

## 顺序

1. **数值表**（`scripts/game-data/spot_clue_data.py` → xlsx）：
   - `spot_tag_defs`（22 标签 × 6 大类）
   - `pond_spot_tags`（21 塘 × 20 位 = 420 行，每点 4–6 标签）
   - `spot_clue_texts`（每 tag：habitat×3 + activity 多档 + inactive×2 + disturbed×1；`activitySignal` 字段）
   - `fishing_formula_constants` + `XP_SIZE_EXP=0.85`
   - `npm run game-data:build && npm run game-data:export`
2. **shared**：`spotClues.ts` 过滤/抽选；`calcCatchXpGrant(species, quality, sizeM)`；`grantCatchProgress` 传 `sizeM`
3. **Unity**：`DesktopGameData` 读 `pond_spot_tags`；`SpotObservationController` 按点位 tags 过滤线索
4. **verify**：`npm run verify:feat-spot-01`、`verify:feat-prog-01`

## 文案禁区（verify 会扫）

- **habitat**：只写环境，禁止鱼种/喜好/宜钓等
- **activity**：只写泡、纹、色、草、风、气味、声响；禁止鱼星/开口/白费力气/说明有鱼等

## 不做

- 标签影响咬钩/出鱼
- v2 按 `activitySignal` 与实时鱼群匹配（仅留字段）

## 验收后策划

`build-master-plan-xlsx.py` 状态 **已实现** + 完成时间 + `npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/feat-spot-02-tag-clue-library-dev.prompt.md 按此实现 FEAT-SPOT-02
```

建议角色：`@backend-dev`（shared/server/数值表）+ Unity Overlay。
