# 开发提示词：运营增长与商业化指标看板（OPS-KPI-1）

你是 Fish Social **数据 / 前端开发 Agent**。按策划规格实现跨日增长看板（留存可筛折线 + 商业化占位）。

## 必读

1. `docs/planning/specs/运营增长与商业化指标看板.md`（**已实现** / **OPS-KPI-1**）
2. `scripts/analytics/compute-daily-summary.mjs`（已有 retention 雏形）· `daily-pipeline.mjs` · `运营平台.html` · `docs/analytics/index.html`

## 顺序

1. **A**：`docs/analytics/growth/` + nu/dau 折线 + 入口链接  
2. **B**：D1/D3/D7/D10/D14/D30 矩阵 + Dn 多选折线  
3. **C**：玩法健康序列 / 漏斗粗版  
4. **D**：商业化灰态占位（禁止假数）

## 验收

对照 spec §9；日批或 `analytics:growth` 可重生 JSON；完成后 spec→已实现并 `planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/ops-growth-kpi-dashboard-dev.prompt.md 按此实现 OPS-KPI-1
```
