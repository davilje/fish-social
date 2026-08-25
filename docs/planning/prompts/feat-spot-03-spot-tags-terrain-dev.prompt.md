# 开发提示词：钓位标签地形微调（FEAT-SPOT-03）

你是 Fish Social **策划 + 数值表** Agent。本票以 **xlsx 人工审校** 为主，不改 tag 词表与咬钩逻辑。

## 必读

1. `docs/planning/specs/钓位标签地形微调.md`（**已确认**）
2. `docs/planning/specs/钓位标签与线索库-v2.md` §2 标签词表
3. `shared/pondTileMap.ts` / Unity 进塘目视对照岸位

## 顺序

1. 打开根目录 `钓鱼玩法固定数值表.xlsx` → sheet **`pond_spot_tags`**
2. 逐塘逐点（420 行）按 Tile 岸位微调 `tags`（4–6 个，含 terrain + depth）
3. `npm run game-data:export`
4. `npm run verify:feat-spot-01`（tag 合法、代表点线索池非空）
5. 可选：写 `docs/planning/reports/spot-tags-audit-YYYYMMDD.md` 摘要

## 不做

- 新增 tagId
- 改 `spot_clue_texts`（见 FEAT-SPOT-04）
- 代码层自动标注替代人工定稿

## 验收后策划

`build-master-plan-xlsx.py` → **已实现** + 完成时间 + `npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/feat-spot-03-spot-tags-terrain-dev.prompt.md 按此完成 FEAT-SPOT-03 标签微调
```

建议：`@producer` 主笔 xlsx；`@backend-dev` 跑 export/verify。
