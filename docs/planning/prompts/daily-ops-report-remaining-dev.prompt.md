<!-- 来源: docs/planning/specs/运营日报-剩余需求交接.md -->
<!-- 用途: 后端/数据 — 运营日报 v1.1 剩余项 -->

你是 Fish Social **后端开发 Agent**（可与 `@data-analyst` 协作报告口径）。实现 **运营日报剩余需求**。

## 必读（按序）

1. `docs/planning/specs/运营日报-剩余需求交接.md`（**本交接权威：范围、验收、顺序**）
2. `docs/planning/specs/运营日报-v1.md`（指标口径与页面 IA）
3. 已有实现：`scripts/analytics/daily-pipeline.mjs` · `compute-daily-summary.mjs` · `generate-daily-ops-report.mjs`
4. 已完成勿重做：D-L3-01 / 02 / 09

## 实现顺序（严格）

### R1（先做）

| 编号 | 标题 |
|------|------|
| D-L3-05 | 鱼塘生态健康日报（日末快照 + report §6） |
| D-L3-04 | 经济 faucet/sink（**先补 faucet 埋点**，再报表） |
| — | 同期：v1 §4 体验与稳定写入 summary + report |

### R2

| 编号 | 标题 |
|------|------|
| D-L3-03 | 留存 D1/D7 + 会话时长 |

### R3

| 编号 | 标题 |
|------|------|
| D-L3-07 | 真实 rulesVersion 写入日报与对照 |
| D-L3-08 | 日批后 Webhook 告警 |

不做：D-L3-06 BI · D-L3-10 合规（另案）。

## 每项完成后（强制）

按 Skill `.cursor/skills/planning-progress-sync/SKILL.md`：

1. `build-master-plan-xlsx.py`：该编号 → **已实现**，完成时间=当天
2. `npm run planning:master-xlsx`
3. 更新 `运营日报-剩余需求交接.md` / `运营日报-v1.md` 验收勾选

## 验收

```bash
npm run verify:daily-ops-report
npm run verify:data-platform-dp-c
# 扩展或新增 verify 覆盖 R1~R3
npm run analytics:daily -- --date=2026-07-05
```

人工：打开 `docs/analytics/daily/<date>/report.html` 检查 §4/5/6 与顶栏版本。

## 开工

从 **R1 · D-L3-05** 开始实现。
