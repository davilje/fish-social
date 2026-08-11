<!-- 来源: docs/planning/specs/运营日报-v1.md（已确认） -->
<!-- 用途: 脚本 + analytics — 运营日报 v1 MVP（D-L3-01 扩展） -->

你是 Fish Social **数据分析 / 脚本开发 Agent**。实现 **运营日报 v1 MVP**：按 **Asia/Shanghai 自然日** 从线上 SQLite 聚合昨日运营数据，生成静态 HTML + JSON 归档，并接入 `docs/analytics/index.html` 索引。

## 必读

1. **产品需求（权威）**：[`docs/planning/specs/运营日报-v1.md`](../specs/运营日报-v1.md) — §5 指标口径、§7 数据流、§9 验收
2. **现有雏形**（在之上扩展，勿重写无关模块）：
   - `scripts/aggregate-daily-metrics.mjs` — 写入 `daily_player_stats` / `daily_pond_stats`
   - `scripts/analytics/daily-pipeline.mjs` — 极简 HTML（4 KPI）
   - `scripts/analytics/generate-pond-day-report.mjs` — **样式/Chart.js 参考模板**
   - `scripts/analytics/lib.mjs` — `buildManifest` 已支持 `type: 'live-daily'`
   - `scripts/analytics/build-index.mjs` — 已有「线上日报」表格区
   - `scripts/analytics/build-live-vs-sim.mjs` — D-L3-02 对照（保持可用）
3. **数据源**：`data/fish-social.db`（`DB_PATH` 环境变量）；主表 `fishing_metrics`；聚合表见 `server/src/migrations/daily_stats.ts`
4. **埋点权威**：`docs/planning/reports/v0.4.4-埋点表清单.xlsx`；代码：`server/src/fishingMetrics.ts`

## 校对结论（spec vs 现状）

| 项 | spec 要求 | 现状 | 本次必须 |
|----|-----------|------|----------|
| 日界时区 | Asia/Shanghai `[dayStart, dayEnd)` | `T00:00:00Z` **错误** | ✅ 修正 |
| 聚合触发 | pipeline 内可靠调用 aggregate | `import()` 异步且未 await | ✅ 改 `spawnSync` 或内联调用 |
| `--date=` | pipeline + aggregate 均支持补跑 | 仅 aggregate 支持 | ✅ |
| 产出物 | `summary.json` + `compact.json` + `report.html` | 缺 `summary.json` | ✅ |
| 报告模块 | §1/§3/§7/§8 MVP | 仅 4 KPI + 鱼塘表 | ✅ |
| KPI 对比 | 当日 / 前日 / 7 日均 / 状态色 | 无 | ✅ |
| 报告样式 | 与 pond-day report 一致 | 内联简陋 CSS | ✅ 新建生成器 |
| npm 串联 | aggregate → pipeline → index | 仅 pipeline | ✅ |
| index 置顶 | 最新一日运营日报 | manifest 有 live-daily，index 区较弱 | ✅ 增强 latest 卡片 |
| §2/4/5/6 | P1 | 未做 | ⏸ 本次可选，MVP 不阻塞 |
| 生态日末快照 | P1 `ecology-daily-snapshot.mjs` | 无 | ⏸ P1 |
| cron 部署 | 00:30 文档 | 无 ops 说明 | ✅ 简短 `docs/ops/daily-analytics-cron.md` |
| Webhook 告警 | P2 D-L3-08 | 无 | ❌ 不做 |

---

## 范围

### MVP（必须交付）

- **§1 健康度总览**：`kpi_daily_catch`、`kpi_dau`、`kpi_fishing_dau`、`kpi_catch_per_fisher`、`kpi_disconnect_rate`、`kpi_abandon_rate`、`kpi_avg_pop_ratio`（后两项无 join 分母时展示「—」并注明）
- **§3 钓鱼产出简表**：四塘钓获、咬钩 hit/miss、断线、平均人口；品质分布（从 metrics payload 聚合）
- **§7 目标对照**：`TARGET_DAILY_CATCH = 100`；读取 `docs/analytics/pond-day-simulation/analysis.json` 中 5 人/塘场景的 `perDayCaught`（或等价字段）作模拟参考
- **§8 异常清单**：spec §5.9 六条规则（页内标红，无 Webhook）
- **页眉**：统计日、生成时间、规则版本 `live`、数据完整度（metrics 条数、首末事件时间）

### P1（MVP 通过后同 PR 或 follow-up，按工期）

- §2 玩家与活跃、§4 体验与稳定、§5 经济 sink、§6 生态（无日末快照前仅 `avg_population`）
- Chart.js：分塘柱状、品质饼图
- `docs/analytics/daily/index.html` 近 30 天日历（可选）

### 明确不做（v1）

- Admin API `GET /api/admin/ops/daily`
- 实时刷新、用户级明细列表
- Webhook / CSV 导出（P2）

---

## 任务 1 — 共享日界工具 `scripts/analytics/date-utils.mjs`

```js
/** 返回 { dateKey, dayStartMs, dayEndMs }，dateKey 为 Asia/Shanghai YYYY-MM-DD */
export function shanghaiDayBounds(dateKeyOrOffset) { ... }
export function yesterdayDateKeyShanghai() { ... }
export function parseDateArg(argv) { ... } // --date=YYYY-MM-DD，默认昨日（上海）
```

**禁止**使用 `new Date(dateKey + 'T00:00:00Z')`。推荐：

- 用固定 offset `+08:00` 构造边界，或
- 用 `Intl` / 手动计算 UTC ms

**单元断言**（写在 verify 脚本）：`2026-07-11` 上海日界对应 UTC `2026-07-10T16:00:00Z` ~ `2026-07-11T16:00:00Z`。

---

## 任务 2 — 修正 `scripts/aggregate-daily-metrics.mjs`

1. 引入 `date-utils.mjs`
2. `dayStart` / `dayEnd` 改用上海日界 ms
3. 排除 bot：`player_id NOT LIKE 'bot-%'`（与 spec §5.1 一致）
4. 保持现有 INSERT OR REPLACE 逻辑；**不**改表结构（MVP）

---

## 任务 3 — 新建 `scripts/analytics/compute-daily-summary.mjs`

从 DB 计算 **单日 + 对比日** 的全量指标，导出 `summary.json` 结构：

```js
{
  meta: { dateKey, generatedAt, rulesVersion: 'live', timezone: 'Asia/Shanghai', completeness: { metricCount, firstEventAt, lastEventAt } },
  kpis: { kpi_daily_catch: { value, prev, avg7d, deltaPct, status: 'ok'|'warn'|'bad' }, ... },
  sections: {
    players: { ... },      // MVP 可仅占位 + 已有字段
    catch: { byPond, byQuality, biteHitRate, escapeRate, pendingExpiredRate },
    stability: { ... },    // MVP: disconnect 计数即可
    economy: { sinkTotal, faucetTotal: null, note: 'faucet 待埋点' },
    ecology: { ponds: [{ pondId, avgPopulation }] },
    targetCompare: { target: 100, simRef, actual, deviationPct },
  },
  alerts: [{ id, level, message }]
}
```

**计算要点**（直接查 `fishing_metrics` + `daily_*`）：

| KPI | SQL/逻辑 |
|-----|----------|
| DAU | 当日任意 event 的 distinct `player_id`（非 bot） |
| 钓鱼 DAU | `fishing_start` distinct |
| 断线率 | disconnect 次数 / `join_pond_success` 次数 |
| 弃钓率 | `abandon_fishing` / `fishing_start` |
| 咬钩命中率 | hit / (hit+miss) |
| 7 日均 | 对前 6 天 + 当日调用同一聚合（可复用 daily 表或缓存） |

导出函数：`export function computeDailySummary(db, dateKey)`.

---

## 任务 4 — 新建 `scripts/analytics/generate-daily-ops-report.mjs`

```bash
node scripts/analytics/generate-daily-ops-report.mjs <summary.json> <out/report.html>
```

- **视觉**：复用 `generate-pond-day-report.mjs` 的 CSS 变量、KPI 卡片、表格、`.badge.ok/warn/bad`
- **导航**：链到 `../../index.html`、`../../live-vs-sim.html`、`../../pond-day-simulation/analysis-report.html`
- **内容**：按 spec §4 信息架构渲染 §1、§3、§7、§8（MVP）
- **数据完整度**：页眉展示 completeness；`metricCount=0` 时 §8 触发 `alert_no_data`

---

## 任务 5 — 重写 `scripts/analytics/daily-pipeline.mjs`

顺序（同步、可脚本化）：

```text
1. parseDateArg → dateKey（默认上海昨日）
2. spawnSync: node scripts/aggregate-daily-metrics.mjs --date=<dateKey>
3. computeDailySummary(db, dateKey)
4. 写入 docs/analytics/daily/<dateKey>/summary.json
5. 从 summary 派生 compact.json（图表用精简结构，兼容 build-live-vs-sim）
6. spawnSync: node scripts/analytics/generate-daily-ops-report.mjs ...
7. spawnSync: node scripts/analytics/build-index.mjs
8. （可选）spawnSync: node scripts/analytics/build-live-vs-sim.mjs
```

**compact.json 最小字段**（保持与现有 consumer 兼容）：

```js
{ generatedAt, rulesVersion: 'live', date, totalCatches, totalDisconnects, activePlayers, pondCount, ponds: [...] }
```

---

## 任务 6 — `package.json` 脚本

```json
"metrics:aggregate-daily": "node scripts/aggregate-daily-metrics.mjs",
"analytics:daily": "node scripts/analytics/daily-pipeline.mjs",
"analytics:daily:date": "node scripts/analytics/daily-pipeline.mjs --date="
```

`analytics:daily` **必须** 一次命令完成 aggregate + 报告 + index（pipeline 内部串联即可）。

---

## 任务 7 — 增强 `scripts/analytics/build-index.mjs`

1. 若 `docs/analytics/daily/` 有目录，**最新 dateKey** 在 `#latest` 卡片展示「最新运营日报」+ 日钓/DAU + 链接
2. manifest 中 `live-daily` 条目按日期降序（`lib.mjs` `buildManifest` 已部分支持，确认排序）
3. 保留现有「线上日报」表格区

---

## 任务 8 — 运维文档 `docs/ops/daily-analytics-cron.md`

说明（≤30 行）：

- **数据前提**：游戏服昨日需运行并写入 `fishing_metrics`
- **推荐部署**：与 DB 同机 cron，**非**依赖开发机常开
- **Linux cron 示例**：`30 0 * * * cd /path/to/fish-social && DB_PATH=... npm run analytics:daily`
- **Windows 任务计划**：触发器 00:30，操作 `npm run analytics:daily`
- **补跑**：`npm run analytics:daily -- --date=2026-07-05`

---

## 任务 9 — 验收脚本 `scripts/verify-daily-ops-report-v1.ts`

```json
"verify:daily-ops-report": "npm run build:shared && npx tsx scripts/verify-daily-ops-report-v1.ts"
```

**断言**：

1. `date-utils.mjs` 上海日界单测通过
2. 用 **内存 SQLite** 插入若干 `fishing_metrics`（含 catch、disconnect、fishing_start），跑 pipeline `--date=固定日`
3. 产出目录含 `summary.json`、`compact.json`、`report.html`
4. `summary.json` 含全部 MVP KPI id；`alerts` 为数组
5. `report.html` 含「目标对照」「异常」关键词
6. `npm run analytics:index` 后 `index.html` 含最新运营日报链接
7. 现有 `npm run verify:data-platform-dp-c` **仍通过**

---

## 回归

```bash
npm run build:shared
npm run verify:daily-ops-report
npm run verify:data-platform-dp-c
```

手动（有线上 DB 时）：

```bash
npm run analytics:daily
# 浏览器打开 docs/analytics/daily/<昨日>/report.html
```

---

## 完成后

1. 更新 [`docs/planning/specs/运营日报-v1.md`](../specs/运营日报-v1.md) §9 MVP 验收项勾选状态（或 CHANGELOG **实现** 小节）
2. 更新 [`docs/analytics/README.md`](../../analytics/README.md)：`analytics:daily` 用法一行
3. 回复：改动文件列表 + `verify:daily-ops-report` 输出摘要

## commit 建议

```text
feat(analytics): daily ops report v1 MVP with Asia/Shanghai boundaries

Fix daily aggregation timezone, add summary.json + styled report generator,
wire analytics:daily pipeline to index, and add cron deployment docs.
```
