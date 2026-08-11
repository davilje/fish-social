# 开发提示词：看板产量人机分列（OPS-CATCH-1）

你是 Fish Social **数据/运维 Agent**。按规格把看板日钓获改为 **背包入库** 口径，并 **人机分列**。

## 必读

1. `docs/planning/specs/看板产量人机分列-背包口径.md`（**已实现** / **OPS-CATCH-1**）
2. `scripts/analytics/compute-daily-summary.mjs` · `build-growth-dashboard.mjs` · `inventory` 表 · 运营日报 HTML / compact

## 顺序

1. summary：按上海日统计 `inventory.caught_at` → total / human / bot；KPI 主值=总量  
2. 报告 HTML + compact + 今日运维日钓获文案  
3. 增长看板折线接入人机字段  
4. verify + 对历史日重跑抽检（如 2026-07-14）  

## 验收

对照 spec §5；完成后 spec→**已实现**，`npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/ops-catch-inventory-human-bot-dev.prompt.md 按此实现 OPS-CATCH-1
```
