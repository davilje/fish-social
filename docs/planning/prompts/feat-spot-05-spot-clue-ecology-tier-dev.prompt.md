# 开发提示词：钓位鱼情联动线索（FEAT-SPOT-05）

你是 Fish Social **服务端 + shared + Unity** 开发 Agent。将钓位实时鱼量/迁徙/受惊映射为 `computedTier`，activity 线索按 `activitySignal` 抽选。

## 必读

1. `docs/planning/specs/钓位鱼情联动线索.md`（**已确认**）
2. `docs/planning/specs/钓位标签与线索库-v2.md` §4.3 activitySignal
3. `docs/planning/specs/钓点鱼群流动性与分区咬钩.md`（spot_id 鱼实体）
4. `shared/spotClues.ts`（`filterSpotCluePool` 已支持 `activityTier`）

## 顺序

1. **shared**：`computeSpotClueTier(spotFishCount, ratios, recentBite, disturb, …)` + 单测
2. **server**：坐席/状态广播附带 `spotClueTier`；阈值常数入表或 `fishing_formula_constants`
3. **Unity**：`SpotObservationController` activity 抽选传入 tier；habitat 仍 50% 随机
4. **降级**：无匹配 tier 时相邻档 → 全 activity 池
5. **verify**：`verify-feat-spot-05` 或扩展 spot-01
6. 埋点（可选）：`spot_clue_shown` + tier/clueId

## 依赖

- **FEAT-SPOT-04** 文案档齐全后体验最佳；可与 SPOT-03/04 并行开发 tier 逻辑

## 不做

- UI 展示钓位鱼条数
- 标签/线索影响咬钩

## 验收后策划

`build-master-plan-xlsx.py` → **已实现** + `npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/feat-spot-05-spot-clue-ecology-tier-dev.prompt.md 按此实现 FEAT-SPOT-05
```

建议：`@backend-dev` + Unity Overlay。
