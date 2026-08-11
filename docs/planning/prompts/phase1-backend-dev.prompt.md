<!-- 来源: docs/planning/specs/phase1-开发计划.md -->
<!-- 用途: 后端开发 — Phase 1「可观测性增强与运维平台」v0.6 -->

你是 Fish Social **后端 / 基础设施开发 Agent**。实现 **Phase 1（v0.6）**，目标：**集中日志平台、实时排障链路、Prometheus 监控、告警通知、Admin RBAC、客户端日志上报、动态 DEBUG 采样**。

## 必读

1. **`docs/planning/specs/phase1-开发计划.md`** — Phase 1 总体规格，含 10 项需求详细描述和验收标准（以此为准）
2. **`docs/planning/specs/三层数据体系-可观测性补充-v0.6.md`** — 补充说明与对话方案映射
3. 现有实现：
   - `server/src/logger.ts` — Phase 0 pino 统一 Logger
   - `server/src/fishingObservability.ts` — `logStructuredEvent`，correlationId 已透传
   - `server/src/fishingMetrics.ts` — SQLite `fishing_metrics` 表写入
   - `server/src/createApp.ts` — `/health` + `/ready` 端点
   - `server/src/admin.ts` — Admin REST API
   - `server/src/fishingDebug.ts` — A2 调试报告
   - `server/src/sessionRegistry.ts` — 统一会话注册中心
4. 关联 spec：`docs/planning/reports/三层数据体系-开发需求清单.xlsx` 中 Phase 1+ sheet

## Phase 1 范围（10 项需求）

| 编号 | 标题 | 优先级 | 预估工期 |
|------|------|--------|----------|
| P1-A1 | correlationId 入库与 Admin 过滤 | **P0** | 1d |
| P1-A2 | 动态 DEBUG 采样 per playerId | P1 | 2d |
| P1-B1 | Loki 集中日志平台对接 | P1 | 2~3d |
| P1-B2 | 日志合规（保留/审计/屏蔽） | P1 | 1d |
| P1-C1 | Prometheus RED 指标端点 | P1 | 1.5d |
| P1-C2 | Grafana 看板与预置仪表盘 | P1 | 1.5d |
| P1-C3 | 告警通知（规则 + Webhook） | P1 | 2d |
| P1-D1 | Live Session Inspector（SSE） | P1 | 2~3d |
| P1-D2 | 客户端日志上报 | P1 | 2d |
| P1-E1 | Admin RBAC 角色权限 | P1 | 2d |

## 不在本次范围

- D-L1-10 OpenTelemetry / Jaeger（P2）
- D-L1-12 Socket 全事件 Tap（P2）
- D-L2-04 PG 迁移（P2）
- D-L2-09 业务健康检查（P2）
- D-L2-10 数据去重（P2）
- D-L2-14 admin-web 独立运维台（P2）
- C 期全部功能（C1~C7，P2）
- BUG-08 F1 Modal 计时排查（P2）

---

## 任务 1 — P1-A1：correlationId 入库与 Admin 过滤

### 1.1 Migration

新建 `server/src/migrations/correlation_id.ts`，对 `fishing_metrics` 表执行 `ALTER TABLE ADD COLUMN correlation_id TEXT`。

### 1.2 改 `fishingMetrics.ts`

`writeMetric` 从 `fields.correlationId` 提取并写入 `correlation_id` 列。

### 1.3 改 `admin.ts`

- `GET /api/admin/metrics/fishing?correlationId=xxx` — 按 ID 过滤
- `GET /api/admin/logs?correlationId=xxx` — error_logs 已有该列

### 1.4 Admin UI

时间线面板增加 correlationId 列展示，点击弹出关联事件列表。

---

## 任务 2 — P1-A2：动态 DEBUG 采样 per playerId

### 2.1 新建 `server/src/debugSampler.ts`

`DebugSampler` 类：内存 `Map<string, DebugSampleTarget>`，支持 start/stop/isActive/listActive/getHistory，60s TTL 清理循环。

### 2.2 改 `admin.ts`

- `POST /api/admin/debug-sample/start` → `{ playerId, reason?, ttlMs? }`
- `POST /api/admin/debug-sample/stop/:playerId`
- `GET /api/admin/debug-sample/list`
- `GET /api/admin/debug-sample/history`

### 2.3 改 `logger.ts`

`setDebugSampler(sampler)`：`logEvent` 内若 `fields.playerId` 匹配活跃采样目标且级别为 debug → 强制输出。

### 2.4 Audit 集成

新建 `audit_log` 表，记录 start/stop/expired 事件。

---

## 任务 3 — P1-B1：Loki 集中日志平台

### 3.1 新建 `server/src/logTransportLoki.ts`

使用 `pino-loki` transport，条件初始化（`LOKI_ENABLED`）。配置 labels、批量推送（5s/100 条）。

### 3.2 改 `logger.ts`

组合 transport（pino + Loki），Loki 不可用时不阻塞、warn 降级。

### 3.3 Docker Compose

新建 `docker/docker-compose.monitoring.yml`（loki + promtail 服务）。

---

## 任务 4 — P1-B2：日志合规

### 4.1 日志保留清理脚本

`scripts/cleanup-old-logs.mjs`：清理 `server/logs/` 下 > `LOG_RETENTION_DAYS`（默认 30）的文件。

### 4.2 敏感数据屏蔽

`logger.ts` 中 `maskSensitiveFields()`：mask `text` / `nickname` 字段（`LOG_MASK_USER_DATA=true`）。

### 4.3 合规文档

`docs/compliance/log-audit-checklist.md`

---

## 任务 5 — P1-C1：Prometheus RED 指标

### 5.1 安装依赖

```bash
npm install prom-client
```

### 5.2 新建 `server/src/metricsPrometheus.ts`

Counter/Histogram/Gauge 定义：HTTP 请求、Socket 事件、DB 查询、咬钩检测、生态 tick、业务指标。

### 5.3 改 `createApp.ts`

条件挂载 `GET /metrics` 端点（独立端口 3002，`METRICS_PROMETHEUS_ENABLED=true`）。

### 5.4 集成

- `serverLoops.ts` 计时写入 Histogram
- `admin.ts` REST 前后计时 + 状态码写入 Counter/Histogram
- `sessionRegistry.ts` 更新 `activeSockets` Gauge

---

## 任务 6 — P1-C2：Grafana 看板

### 6.1 新建

`docs/monitoring/grafana-dashboards/fish-social-overview.json` — 5 行区（概览/HTTP/Socket/DB/业务）
`docs/monitoring/grafana-dashboards/fish-social-pond-detail.json` — 四塘详情

### 6.2 导入文档

`docs/monitoring/README.md`

---

## 任务 7 — P1-C3：告警通知

### 7.1 告警规则

`docs/monitoring/alert-rules/prometheus.yml`（5 条：HighErrorRate/SlowQuerySpike/HighSocketDisconnect/PondDepleted/MetricsQueueBacklog）
`docs/monitoring/alert-rules/loki.yml`（1 条：ErrorLogSpike）

### 7.2 Webhook 通知

`server/src/alertWebhook.ts`：`sendAlert()` → 通用 JSON / 钉钉 / 企微 Webhook。

### 7.3 测试脚本

`scripts/test-alert.sh`

---

## 任务 8 — P1-D1：Live Session Inspector（SSE）

### 8.1 新建 `server/src/liveSessionInspector.ts`

`LiveSessionInspector` 类：SSE 推送，每秒读取玩家 state + recentMetrics，最多 5 并发。

### 8.2 SSE 端点（改 `admin.ts`）

`GET /api/admin/live-session?playerId=xxx` → `text/event-stream`。

### 8.3 Admin UI

新增 Live Session Tab，输入 playerId 开始/停止实时查看。

---

## 任务 9 — P1-D2：客户端日志上报

### 9.1 Migration

新建 `client_logs` 表（`id, player_id, ts, level, event_type, fields, created_at`）。

### 9.2 服务端 API

- `POST /api/client-logs`（requireAuth）— 批量写入 + 若 Loki 启用则转发
- `GET /api/admin/client-logs?playerId=&since=&limit=` — 查询

### 9.3 客户端日志收集器（提示，由前端实现）

`mobile/lib/clientLogger.ts`：内存队列，10s 批量上报，自动采集连接/页面/咬钩/导航事件。

---

## 任务 10 — P1-E1：Admin RBAC

### 10.1 新建 `server/src/adminRbac.ts`

`AdminRbac` 类：从 `ADMIN_RBAC_RULES` 环境变量加载角色映射。无配置时完全向后兼容（ADMIN_SECRET → admin）。

### 10.2 中间件 `requireRole(minRole)`

`viewer`（只读）/ `operator`（写操作）/ `admin`（管控）。

### 10.3 迁移 Admin API

只读 API → `requireRole('viewer')`，写操作 → `requireRole('operator')`，敏感操作 → `requireRole('admin')`。

---

## 验收脚本

新建 `scripts/verify-phase1-core.ts`（`npm run verify:phase1-core`）：

**断言清单**：
1. `fishing_metrics` 表存在 `correlation_id` 列
2. Admin API `?correlationId=xxx` 过滤正常
3. `POST /api/admin/debug-sample/start` 返回成功
4. `GET /api/admin/debug-sample/list` 返回列表
5. `GET /metrics` 返回 Prometheus 格式（启用时）
6. `POST /api/client-logs` 写入 client_logs 表
7. `GET /api/admin/client-logs?playerId=xxx` 返回日志
8. `requireRole('operator')` 拒绝 viewer 写操作（403）
9. SSE `/api/admin/live-session?playerId=xxx` 返回 text/event-stream
10. Loki transport 初始化不阻塞（LOKI_ENABLED=false 时正常启动）

**回归**：
```bash
npm run build:shared && npm run verify:phase1-core && npm run verify:server-observability && npm run verify:auth && npm run verify:session-checkpoint
```

---

## 完成后

1. 更新 `docs/planning/specs/phase1-开发计划.md` 各任务状态为 **已实现**
2. 更新 `docs/planning/CHANGELOG.md` 实现小节
3. 更新 `docs/planning/specs/README.md` 索引
4. 回复：改动文件列表 + 各 `verify:*` 输出摘要

## commit 建议

```
feat(phase1): observability platform v0.6 — Loki, Prometheus, RBAC, live inspector, client logs

- correlationId persistence and admin filtering (P1-A1)
- dynamic debug sampling per playerId (P1-A2)
- Loki centralized log transport (P1-B1)
- log compliance: retention & data masking (P1-B2)
- Prometheus RED metrics endpoint (P1-C1)
- Grafana dashboard presets (P1-C2)
- alert rules & webhook notifications (P1-C3)
- Live Session Inspector SSE (P1-D1)
- client log reporting from mobile (P1-D2)
- Admin RBAC role-based access control (P1-E1)
```
