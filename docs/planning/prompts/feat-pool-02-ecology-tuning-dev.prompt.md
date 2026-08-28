# FEAT-POOL-02 开发交接（已实现）

> **状态：已实现（2026-08-27）**。本文档为调参结案归档；无需再开开发对话实现。

## 派发（归档）

```text
@docs/planning/prompts/feat-pool-02-ecology-tuning-dev.prompt.md 结案归档 FEAT-POOL-02
```

角色：数值表 + `@backend-dev`（生态常量）— **已完成**。

## 规格

[塘人口与品质权重调参.md](../specs/塘人口与品质权重调参.md)

## 已交付摘要

1. `ECOLOGY_BY_CATEGORY`：novice `1/1/1`；其余 `100/15/100`
2. `POND_SUPPLEMENT_CHECK_MS` / `FISH_MIGRATION_CHECK_MS` = 60min
3. `CATEGORY_QUALITY_WEIGHTS`：老手 / 野外·水库 / 禁止新占比；表列 `pondCategoryName`
4. 新手塘禁止补充与重种（`pondAllowsEcologySupplement`）
5. 运维重种：`npx tsx scripts/ops/reset-pond-ecology.ts --apply`

## 自检

- [x] `npm run game-data:build` + `game-data:export`
- [x] 重种后正式塘 100、新手 1；品质分布贴近新表
- [x] 补充逻辑仍走 D8 × category 理想占比

## 非目标

咬钩间隔、种池、回鱼、卖价 — 本票未改。
