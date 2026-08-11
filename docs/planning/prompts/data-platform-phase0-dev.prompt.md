<!-- 来源: docs/planning/reports/三层数据体系-开发需求清单.xlsx · Phase 0 -->
<!-- 用途: 后端 + 脚本 — 三层数据体系 Phase 0「能排障、不丢数据」 -->

你是 Fish Social **后端 / 基础设施开发 Agent**。实现**三层数据体系 Phase 0**，目标：**日志可归档、错误可回溯、健康可探活、metrics 有生命周期、DB 可备份、线上有日批报表**。

## 必读

1. [`docs/planning/reports/三层数据体系-开发需求清单.xlsx`](../reports/三层数据体系-开发需求清单.xlsx) — Phase 0 条目（总览 + 各需求 sheet）
2. [`docs/planning/specs/三层数据体系-可观测性补充-v0.6.md`](../specs/三层数据体系-可观测性补充-v0.6.md)
3. 现有实现：
   - `server/src/fishingObservability.ts` — `logStructuredEvent`
   - `server/src/errorLog.ts` — 内存 200 条
   - `server/src/fishingMetrics.ts` — SQLite `fishing_metrics`
   - `server/src/createApp.ts` — 浅 `/health`
   - `server/src/db.ts` — migrations 模式
4. 关联 spec：`v0.4.4-埋点缺口复核与补全.md`（D-L2-08）

## Phase 0 范围（10 条需求）

| 编号 | 标题 | 层级 |
|------|------|------|
| D-L1-01 | 统一结构化 Logger | L1 |
| D-L1-02 | 日志级别策略（随 L1-01 最小实现） | L1 |
| D-L1-03 | 日志落盘轮转 | L1 |
| D-L1-05 | 错误日志 DB 持久化 | L1 |
| D-L1-08 | 健康检查 `/health` + `/ready` | L1 |
| D-L2-01 | 事件字典 Schema | L2 |
| D-L2-03 | 日聚合表 | L2 |
| D-L2-02 | metrics 保留归档 | L2 |
| D-L2-08 | v0.4.4 埋点 33 条对齐 | L2 |
| D-L2-11 | SQLite 定时备份 | L2 |
| D-L3-01 | 日批分析流水线（读线上 DB） | L3 |

## 不在本次范围（Phase 1+）

- D-L1-04 集中日志 Loki、D-L1-06 correlationId 入 metrics、D-L1-07 告警
- D-L2-05~07 Grafana/RBAC、D-L2-13 Live Inspector
- D-L2-04 PG 迁移、D-L3-02~10 运营分析扩展

---

## 任务 1 — D-L1-01 + D-L1-02 + D-L1-03：统一 Logger

### 1.1 新建 `server/src/logger.ts`

推荐 **pino**（轻量 JSON）：

```ts
// 对外 API（示意）
export type LogLevel = 'error' | 'warn' | 'info' | 'debug';
export function logInfo(fields: Record<string, unknown>, msg?: string): void;
export function logWarn(...): void;
export function logError(...): void;
export function logDebug(...): void;
// 兼容现有 prefix 风格
export function logEvent(prefix: string, eventType: string, fields: Record<string, unknown>): void;
```

**JSON 字段契约**（每条日志）：

```
ts, level, service='fish-social-server', eventType?, correlationId?, playerId?, pondId?, durationMs?, msg?
```

- `LOG_LEVEL` 环境变量，默认：`production` → `info`，`development` → `debug`
- `LOG_DIR` 默认 `server/logs/`（或项目根 `logs/`），`.gitignore` 已排除
- 开发：`pino-pretty` 可读输出（`LOG_PRETTY=1` 或 `NODE_ENV=development`）
- 生产：单行 JSON + **按日轮转**（`pino-roll` 或 `pino.destination` + 日期文件名）
- Docker 部署时仍写 stdout（`LOG_DIR` 空或未设则仅 stdout）

### 1.2 迁移现有日志入口

| 文件 | 改动 |
|------|------|
| `fishingObservability.ts` | `logStructuredEvent` 内部改调 `logger.logEvent`；保留 correlationId 解析逻辑 |
| `errorLog.ts` | `logError` 改调 `logger.logError`（任务 2 再加 DB） |
| `db.ts` | `[slow_query]` 改 `logWarn`，`eventType: 'sqlite_query_slow'` |
| `index.ts` / 其它 | P0 路径消除裸 `console.log`（`console.warn/error` 用于启动致命错误可保留） |

### 1.3 级别策略（D-L1-02 最小版）

| 级别 | 典型事件 |
|------|----------|
| error | 未捕获异常、metrics 刷盘失败 |
| warn | slow_query、phase_transition_invalid、admin 慢路由 |
| info | join_pond、phase_transition、disconnect、shutdown |
| debug | bite_tick_hit/miss、perf 节流日志 |

`bite_tick_*` 与 `perf` 类默认 **debug**；生产 `LOG_LEVEL=info` 时不输出。

---

## 任务 2 — D-L1-05：错误日志 DB 持久化

### 2.1 Migration `server/src/migrations/error_logs.ts`

```sql
CREATE TABLE IF NOT EXISTS error_logs (
  id TEXT PRIMARY KEY,
  message TEXT NOT NULL,
  stack TEXT,
  context TEXT,
  correlation_id TEXT,
  created_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_error_logs_created ON error_logs(created_at DESC);
```

在 `db.ts` 注册 migration。

### 2.2 改 `errorLog.ts`

- `logError`：**同步写 DB**（或入队批量，与 metrics 类似）+ `logger.logError`
- 保留内存 ring **可选**（Admin 快速读最近 50 条）；主数据源为 DB
- 保留策略：启动时删除 `created_at < now - 90d`（`ERROR_LOG_RETENTION_DAYS`）

### 2.3 改 `admin.ts`

`GET /api/admin/logs`：

- 查 `error_logs` 表
- 支持 `?since=`、`?limit=`（max 200）、`?context=`
- `POST /api/admin/logs/clear` 改清 DB（保留审计 log）

---

## 任务 3 — D-L1-08：健康检查

### 3.1 扩展 `createApp.ts`

**`GET /health`**（liveness，轻量）：

```json
{ "ok": true, "uptimeSec": 123, "version": "0.1.0" }
```

**`GET /ready`**（readiness，深度）：

```json
{
  "ok": true,
  "db": "ok",
  "metricsQueueDepth": 0,
  "memoryMb": { "rss": 120, "heapUsed": 80 },
  "activeSockets": 12,
  "pondUserCount": 8
}
```

- `db`：`SELECT 1` 或 `db.pragma('quick_check')`
- `metricsQueueDepth`：从 `fishingMetrics.ts` 导出 `getPendingMetricsCount()`
- `activeSockets` / `pondUserCount`：从 `sessionRegistry` 或 `gameState` 导出只读计数

### 3.2 优雅停机

`index.ts` shutdown 开始后，`/ready` 返回 **503** `{ ok: false, draining: true }`（设模块级 `isShuttingDown` 标志）。

`/metrics` Prometheus：**本次不做**（属 D-L2-05 Phase 1）。

---

## 任务 4 — D-L2-01：事件字典 Schema

### 4.1 新建 `shared/src/metrics-schema.ts`（或 `shared/metrics-events.json`）

- 导出 `FISHING_METRIC_EVENTS` 常量数组 + 每事件 `requiredFields` / `optionalFields`
- 与 `FishingMetricEvent` 类型保持同步（33 条，见 `v0.4.4-埋点表清单.xlsx`）

### 4.2 `fishingMetrics.ts` 开发环境校验

```ts
if (process.env.NODE_ENV !== 'production') {
  validateMetricPayload(eventType, payload); // warn only, 不阻断
}
```

### 4.3 文档

在 `shared/src/metrics-schema.ts` 顶部注释：新增 event 须同步更新 xlsx（`npm run planning:metrics-xlsx` 维护流程）。

---

## 任务 5 — D-L2-03 + D-L2-02：日聚合 + 保留归档

### 5.1 Migration `daily_stats.ts`

```sql
CREATE TABLE IF NOT EXISTS daily_player_stats (
  date_key TEXT NOT NULL,
  player_id TEXT NOT NULL,
  catch_count INTEGER NOT NULL DEFAULT 0,
  escape_count INTEGER NOT NULL DEFAULT 0,
  disconnect_count INTEGER NOT NULL DEFAULT 0,
  fishing_ms INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY (date_key, player_id)
);

CREATE TABLE IF NOT EXISTS daily_pond_stats (
  date_key TEXT NOT NULL,
  pond_id TEXT NOT NULL,
  catch_count INTEGER NOT NULL DEFAULT 0,
  bite_tick_hit INTEGER NOT NULL DEFAULT 0,
  bite_tick_miss INTEGER NOT NULL DEFAULT 0,
  disconnect_count INTEGER NOT NULL DEFAULT 0,
  avg_population REAL,
  PRIMARY KEY (date_key, pond_id)
);
```

### 5.2 `scripts/aggregate-daily-metrics.mjs`

- 入参：`--date=YYYY-MM-DD`（默认昨日）
- 从 `fishing_metrics` 聚合写入 `daily_*` 表
- 幂等：`INSERT OR REPLACE` 或先删后插

`package.json`：

```json
"metrics:aggregate-daily": "node scripts/aggregate-daily-metrics.mjs"
```

### 5.3 `scripts/archive-metrics.mjs`

- `METRICS_RETENTION_DAYS` 默认 90
- 超期行：导出 `data/archives/metrics-YYYYMMDD.jsonl` 后 `DELETE`
- **不删除** `daily_*` 聚合表
- 启动时（`index.ts`）若 `fishing_metrics` 行数 > 阈值打 `warn` 日志

`package.json`：

```json
"metrics:archive": "node scripts/archive-metrics.mjs"
```

### 5.4 Admin 只读（最小）

`GET /api/admin/metrics/daily?date=&pondId=` 返回 `daily_pond_stats` 行（可选，优先脚本 + 任务 7）。

---

## 任务 6 — D-L2-08：埋点 33 条对齐

以 `npm run verify:server-observability` 为门禁，确保全绿。若失败则补齐：

| 缺口 | 落点 |
|------|------|
| `session_rebound` metric | `sessionRegistry.ts` |
| `snapshot_build_duration_ms` | `gameState.ts` → perf 日志 + 可选 metric |
| `sqlite_query_slow` | `db.ts` → 已有日志，补 metric 写入（canonical 名） |
| `socket_disconnect` vs `disconnect` | 统一 canonical，另一写别名到 payload |
| `pending_catch_accept` vs `catch_accept` | 同上 |

完成后运行 `npm run planning:metrics-xlsx` 并手动核对 xlsx 状态列（或脚本注释说明已对齐）。

---

## 任务 7 — D-L2-11：DB 备份

### 7.1 `scripts/backup-db.mjs`

- 使用 `better-sqlite3` backup API 或 `sqlite3 .backup`
- 输出：`data/backups/fish-social-YYYYMMDD-HHmm.db.gz`
- 保留最近 7 份（`DB_BACKUP_RETAIN`）
- 文档：`docs/ops/db-backup-restore.md`（恢复步骤 10 行即可）

`package.json`：

```json
"db:backup": "node scripts/backup-db.mjs"
```

---

## 任务 8 — D-L3-01：线上日批流水线

### 8.1 `scripts/analytics/daily-pipeline.mjs`

读 **线上 SQLite**（`DB_PATH`），产出昨日报表：

1. 调 `metrics:aggregate-daily --date=yesterday`
2. 查询 `daily_pond_stats` + `daily_player_stats` 汇总
3. 写入 `docs/analytics/daily/YYYY-MM-DD/compact.json` + `report.html`
4. 扩展 `build-index.mjs`：增加 **「线上日报」** 区（`type: live-daily`）

`package.json`：

```json
"analytics:daily": "node scripts/analytics/daily-pipeline.mjs"
```

报表最小指标：四塘日钓合计、断线次数、活跃玩家数。版式可参考现有 `pond-day` report。

---

## 任务 9 — 环境变量与 `.env.example`

补充文档（不必全部有默认值）：

```env
LOG_LEVEL=info
LOG_DIR=logs
LOG_PRETTY=1
ERROR_LOG_RETENTION_DAYS=90
METRICS_RETENTION_DAYS=90
DB_BACKUP_RETAIN=7
SLOW_QUERY_MS=30
```

---

## 任务 10 — 验收脚本

新增 `scripts/verify-data-platform-phase0.ts` +：

```json
"verify:data-platform-phase0": "npm run build:shared && npx tsx scripts/verify-data-platform-phase0.ts"
```

**断言清单**：

1. `logger` 模块存在；`logStructuredEvent` 不直接 `console.log`
2. `GET /health` 200；`GET /ready` 含 `db: ok`
3. `error_logs` 表存在；`logError` 后 Admin API 可查到
4. `daily_player_stats` / `daily_pond_stats` 表存在
5. 插入测试 metric 后 `aggregate-daily-metrics` 可跑通（可用内存 DB 或 mock）
6. `verify:server-observability` 仍全绿

---

## 回归

```bash
npm run build:shared
npm run verify:data-platform-phase0
npm run verify:server-observability
npm run verify:auth
npm run verify:session-checkpoint
npm run verify:disconnect-reconnect
```

手动抽检：

1. 启动服务 → `server/logs/` 或 stdout 有 JSON 行
2. 触发 `logError` → 重启 → `GET /api/admin/logs` 仍有记录
3. `npm run db:backup` → `data/backups/` 有文件
4. `npm run analytics:daily` → `docs/analytics/daily/` 有昨日目录

---

## 完成后

1. 更新 xlsx 对应 sheet「当前状态」→ **已实现**（或运行策划 sync 脚本若已接入）
2. 更新 [`三层数据体系-可观测性补充-v0.6.md`](../specs/三层数据体系-可观测性补充-v0.6.md) Phase 0 里程碑
3. 补 `CHANGELOG.md` **实现** 小节
4. 回复：改动文件列表 + 各 `verify:*` 输出摘要

## commit 建议

```text
feat(data-platform): Phase 0 logging, health, metrics lifecycle, and daily pipeline

Add pino logger with file rotation, error_logs persistence, /ready health,
daily stats aggregation, metrics archive, DB backup script, and live daily
analytics pipeline per three-layer data platform spec.
```
