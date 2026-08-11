# 开发提示词：Admin / 业务健康产量对齐背包（OPS-CATCH-1.1）

你是 Fish Social **后端 / Admin Agent**。按规格让 Admin 玩家钓获与业务健康产量与 OPS-CATCH-1 一致（**inventory**）。

## 必读

1. `docs/planning/specs/Admin与业务健康产量对齐背包.md`（**已确认** / **OPS-CATCH-1.1**）
2. `docs/planning/specs/看板产量人机分列-背包口径.md`（OPS-CATCH-1 已实现口径）
3. `server/src/adminPlayersOverview.ts` · `businessHealth.ts` · `scripts/aggregate-daily-metrics.mjs`

## 顺序

1. **A**：overview `catchCount` ← 窗口内 `inventory` 计数（含 bot）  
2. **B**：`aggregate-daily-metrics` 的 `catch_count` ← inventory（塘/玩家）；hook/escape 仍 metrics  
3. **C**：业务健康读新聚合；必要时修上海日 `dateKey`  
4. 重跑近几日聚合抽检；verify  

## 验收

对照 spec §5；完成后 spec→**已实现**，`npm run planning:master-xlsx`。

- [x] `verify:ops-catch-inventory-admin` 通过  
- [x] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/planning/prompts/admin-business-health-catch-inventory-dev.prompt.md 按此实现 OPS-CATCH-1.1
```
