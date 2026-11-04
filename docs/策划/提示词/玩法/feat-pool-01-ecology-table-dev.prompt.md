# FEAT-POOL-01 开发交接：塘生态数值表驱动

<!-- fishing-rule-status: historical-partial; reviewed: 2026-11-04 -->

> **交接适用状态（2026-11-04）：表驱动交接保留；旧生成与补入方式局部替代。**
>
> 不得用旧先抽种再抽品质、rollJuvenileSize统一补入和固定上限覆盖当前实现。表驱动、sheet结构与重量仅展示等仍保留；成长与日常50/40/10规格依FISH-ECOLOGY-01。
>
> 先读[钓鱼系统现行规则](../../规格/玩法/钓鱼系统现行规则.md)与[FISH-ECOLOGY-01开发交接](fish-ecology-entry-growth-dev.prompt.md)，再按有效范围使用本文件。概率现状见[代码审计](../../../数据分析/fishing-probability-audit-2026-11-04/当前钓鱼概率完整说明.md)，新的概率方案尚未定稿。
>
> 以下保留原阶段交接供追溯。不要直接按旧派发命令重做已被替代的实现，也不要用旧验收项覆盖现行需求。

---

你是 Fish Social **后端 + shared** 开发 Agent（Unity 重量展示可顺手）。策划已完成，**只写代码**。

## 必读（顺序）

1. `docs/策划/规格/玩法/塘生态数值表驱动.md` — **以最新修订为准**（种池无 quality 列）
2. `docs/策划/规格/玩法/中国鱼种与区域分布.md` — 种品质带 / pool 权威
3. `shared/pondEcology.ts` / `server/src/pondEcology.ts` — seed / supplement
4. `shared/fishing.ts` — 公式（**不改形状**，常数改读表）
5. `scripts/game-data/build_balance_xlsx.py`

## P0 实现清单

### A. 数值表

1. **`pond_fish_pool`**：`pondId, speciesId, speciesName, spawnWeight, enabled`（**无 quality**）
2. **`ponds`**：含 `maxPopulation` / `minPopulation` / `initialPopulation`（原 `pond_ecology` 已并入，勿再单独 sheet）
3. **`pond_category_quality_weights`**：7 category × 品质权重
4. **`fish_quality_stats`**：`sizeCapM`, `biteBaseAtMaxSize`, **卖价列** `QUALITY_BASE`/`SIZE_REF`/`MIN_SELL`（原 `fish_sell` 已并入；**无** SPECIES_MULT）
5. **`fishing_formula_constants`**：含 `LENGTH_WEIGHT_A/B`
6. 移除接线：`pond_fish_size_cap`、`pond_quality_cap`、`fish_species_habitat`、`pond_ecology`、`fish_sell`、`spot_tags`
7. `npm run game-data:build` + `export`

### B. gameData

- `pickPondSpecies(pondId)` → 种
- `rollPondQuality(pondId, qMin, qMax)` → 塘权重 ∩ 种带
- `pickSpawnFish(pondId)` → `{ speciesId, quality }`
- `getFishQualityStats` / `getFishingFormulaConstant`

### C. 生态接线

- 删除 `STOCK_TEMPLATES`；seed / supplement 用 `pickSpawnFish` + `rollJuvenileSize`
- 补充 D8 ideal 用 category 权重表

### D. fishing.ts

- sizeCap / bite / escape 读表
- `calcFishWeightKg(sizeM)` 展示用；`rollInitialSize` **不**用 `typicalMinM` 作 floor

### E. 展示（建议）

- Unity / Mobile：体长旁现算重量；图鉴 typical 标「图鉴」

### F. 验证

- `scripts/verify-feat-pool-01.ts` + `verify:feat-pool-01`
- 与 `verify:fish-cn-01` 联跑

## 非目标（禁止做）

- 改咬钩/脱钩公式结构
- pool 行再绑 quality
- 重量落库
- Mobile UI 大改

## 完成后

- [ ] spec §5 验收全勾（须用户验收）
- [ ] 状态 → **已实现**；`planning:master-xlsx`
- [ ] CHANGELOG 一条

## 自检

```bash
npm run game-data:export
npm run verify:feat-pool-01
npm run verify:fish-cn-01
npm run verify:feat-return-02
```

## 派发

```text
@docs/策划/提示词/玩法/feat-pool-01-ecology-table-dev.prompt.md 按此实现 FEAT-POOL-01
```

建议角色：`@backend-dev`（兼 shared）。
