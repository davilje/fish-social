# FEAT-POOL-02 开发交接（已实现）

<!-- fishing-rule-status: historical-partial; reviewed: 2026-11-04 -->

> **交接适用状态（2026-11-04）：历史调参结案；当前补鱼和实际库重建流程已更新。**
>
> 100条目标、类别权重和小时迁徙仍沿用；补鱼改为每小时≤40补80和04:00补100，品质限补依当前规则。下文reset旧重种命令不得用于本轮实际库切换，须先备份并按L1交接执行。
>
> 先读[钓鱼系统现行规则](../../规格/玩法/钓鱼系统现行规则.md)与[FISH-ECOLOGY-01开发交接](fish-ecology-entry-growth-dev.prompt.md)，再按有效范围使用本文件。概率现状见[代码审计](../../../数据分析/fishing-probability-audit-2026-11-04/当前钓鱼概率完整说明.md)，新的概率方案尚未定稿。
>
> 以下保留原阶段交接供追溯。不要直接按旧派发命令重做已被替代的实现，也不要用旧验收项覆盖现行需求。

---

> **状态：已实现（2026-08-27）**。本文档为调参结案归档；无需再开开发对话实现。

## 派发（归档）

```text
@docs/策划/提示词/玩法/feat-pool-02-ecology-tuning-dev.prompt.md 结案归档 FEAT-POOL-02
```

角色：数值表 + `@backend-dev`（生态常量）— **已完成**。

## 规格

[塘人口与品质权重调参.md](../../规格/玩法/塘人口与品质权重调参.md)

## 已交付摘要

1. `ECOLOGY_BY_CATEGORY`：novice `1/1/1`；其余 `100/15/100`
2. `POND_SUPPLEMENT_CHECK_MS` / `FISH_MIGRATION_CHECK_MS` = 60min
3. `CATEGORY_QUALITY_WEIGHTS`：老手 / 野外·水库 / 禁止新占比；表列 `pondCategoryName`
4. 新手塘禁止补充与重种（`pondAllowsEcologySupplement`）
5. 运维重种：`npx tsx scripts/运维/reset-pond-ecology.ts --apply`

## 自检

- [x] `npm run game-data:build` + `game-data:export`
- [x] 重种后正式塘 100、新手 1；品质分布贴近新表
- [x] 补充逻辑仍走 D8 × category 理想占比

## 非目标

咬钩间隔、种池、回鱼、卖价 — 本票未改。
