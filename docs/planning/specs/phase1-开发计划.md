# Phase 1 — 可观测性增强与运维平台（v0.6）

| 字段 | 内容 |
|------|------|
| 状态 | **已确认** |
| 目标版本 | v0.6.x |
| 前置 | Phase 0（三层数据体系基础）全部实现 · 架构修复 R0~R2 全部实现 · 数值/生态 NUM-01~06 全部实现 |

---

## 1. 背景与目标

### 1.1 背景

Phase 0 已完成三层数据体系基础建设：统一 JSON Logger、健康检查 `/health` + `/ready`、错误日志 DB 持久化（90d 保留）、事件字典 Schema、日聚合表、metrics 归档与备份、日批流水线、33 条埋点对齐。

当前可观测性仍然存在关键缺失：

1. **排障需要手动 grep**：无集中日志平台（Loki/ELK），无法跨实例检索
2. **问题玩家追踪低效**：correlationId 已部分实现但未入库 metrics，Admin 无法按 ID 过滤
3. **无实时会话查看**：发生问题时无法 "实时盯人"，只能事后查慢日志
4. **无告警机制**：服务异常（慢查询、高错误率、断线激增）无人感知
5. **运维管控空白**：Admin 无权限控制，任何人都可用密钥操作所有功能
6. **移动端黑盒**：客户端日志无法上报，服务端看不透移动端问题
7. **调试手段原始**：无法对指定玩家临时提升日志级别做定向排查

### 1.2 目标

- **集中日志平台**：所有服务端日志流入 Loki，支持全文检索与按 correlationId/playerId/pondId 过滤
- **实时排障链路**：correlationId 入库 → Admin 过滤 → 动态采样提级 → Live SSE 实时订阅
- **监控与告警**：Prometheus RED 指标 + Grafana 看板 + 基础告警通知
- **合规与管控**：日志合规保留策略、Admin RBAC 角色权限
- **客户端可观测**：移动端日志上报服务端，与 Live Inspector 同屏对照
- **闭环**：从此线上问题可在 5 分钟内定位到具体玩家请求链路

### 1.3 非目标（明确不做 / 推迟到 Phase 2）

- OpenTelemetry 分布式追踪（D-L1-10）— P2，Jaeger span 树暂不实施
- Socket 全事件 Tap（D-L1-12）— P2，1% 生产采样暂不实施
- PG 存储迁移（D-L2-04）— P2，继续使用 SQLite
- 业务健康检查（D-L2-09）— P2，部分能力已合并入 `/ready`
- 数据去重（D-L2-10）— P2
- admin-web 独立运维台（D-L2-14）— P2，移动端 Admin 保留
- C 期功能特性（C1~C7）— 全部 P2，状态机/图鉴/热更/Sink 等单独排期
- BUG-08 F1 Modal 计时排查 — P2，待开发

---

## 2. Phase 1 范围总览

| 编号 | 标题 | 组别 | 层级 | 优先级 | 预估工期 | 依赖 |
|------|------|------|------|--------|----------|------|
| P1-A1 | correlationId 入库与 Admin 过滤 | A-可观测增强 | L1 | **P0** | 1d | 已有 R1-5 部分实现 |
| P1-A2 | 动态 DEBUG 采样 per playerId | A-可观测增强 | L1 | **P1** | 2d | P1-A1 |
| P1-B1 | Loki 集中日志平台对接 | B-集中日志 | L1 | **P1** | 2~3d | Phase 0 Logger |
| P1-B2 | 日志合规（保留/审计/合规清单） | B-集中日志 | L1 | **P1** | 1d | P1-B1 |
| P1-C1 | Prometheus RED 指标端点 | C-运维监控 | L2 | **P1** | 1.5d | 无 |
| P1-C2 | Grafana 看板与预置仪表盘 | C-运维监控 | L2 | **P1** | 1.5d | P1-C1 |
| P1-C3 | 告警通知（基础规则 + 渠道） | C-运维监控 | L1 | **P1** | 2d | P1-B1, P1-C1 |
| P1-D1 | Live Session Inspector（SSE） | D-实时排障 | L2 | **P1** | 2~3d | P1-A1 |
| P1-D2 | 客户端日志上报 | D-实时排障 | L2 | **P1** | 2d | P1-B1 |
| P1-E1 | Admin RBAC 角色权限 | E-安全管理 | L2 | **P1** | 2d | 无 |

**Phase 1 总计**：约 15~19 人天（可部分并行）

---

## 3. 需求分组与详细任务拆解

### 3.1 A组 — 可观测性增强（2项，3d）

#### P1-A1：correlationId 入库与 Admin 过滤（D-L1-06 完成）

**背景**：R1-5 已实现连接时生成 `socket.data.correlationId` 并透传到 `fishingMetrics` 和 `logStructuredEvent` 的 `fields.correlationId`。但 correlationId **未写入 fishing_metrics 表**，Admin 侧无法按 correlationId 过滤查询。

**任务**：

1. **metrics 入库增加 correlationId 列**
   - Migration：`ALTER TABLE fishing_metrics ADD COLUMN correlation_id TEXT`
   - `fishingMetrics.ts` `writeMetric`：从 fields 提取 correlationId 写入
   - 兼容旧行（correlation_id 可为 NULL）

2. **Admin API 增加按 correlationId 过滤**
   - `GET /api/admin/metrics/fishing?correlationId=` 支持按 ID 过滤
   - `GET /api/admin/logs?correlationId=`（error_logs 表的 `correlation_id` 列已存在，只需增加 API 参数）
   - 返回结果包含时间线上下文（前后各 5 条同 correlationId 事件）

3. **Admin UI 增加「链路详情」入口**
   - Admin 时间线面板增加 correlationId 列展示
   - 点击 correlationId → 弹出关联事件列表

**验收**：
- [x] fishing_metrics 表存在 `correlation_id` 列
- [x] 写 metric 时 correlationId 正确入库
- [x] Admin API `GET /api/admin/metrics/fishing?correlationId=xxx` 返回正确过滤结果
- [x] Admin API `GET /api/admin/logs?correlationId=xxx` 返回正确过滤结果
- [x] Admin UI 可查看和点击 correlationId

---

#### P1-A2：动态 DEBUG 采样 per playerId（D-L1-11）

**背景**：客诉时需要对指定玩家临时提升日志级别到 debug，持续 30 分钟，带审计记录。当前仅支持全局 `LOG_LEVEL` 切换。

**任务**：

1. **内存采样表 `debugSampleTargets`**
   ```ts
   interface DebugSampleTarget {
     playerId: string;
     reason: string;
     requestedBy: string;
     createdAt: number;
     ttlMs: number;       // 默认 30 分钟 = 1800000
   }
   ```

2. **Admin API**
   - `POST /api/admin/debug-sample/start` — `{ playerId, reason?, ttlMs? }` → 返回 `{ targetId }`
   - `POST /api/admin/debug-sample/stop/:playerId` — 手动终止
   - `GET /api/admin/debug-sample/list` — 当前活跃采样列表
   - `GET /api/admin/debug-sample/history` — 历史采样记录（近 7 天）

3. **Logger 集成**
   - `logger.ts` 新增 `isDebugSampled(playerId): boolean`
   - `logEvent` 内部：若 `fields.playerId` 匹配采样目标且请求级别为 debug → 强制输出 debug 日志，标记 `[debug-sampled]`
   - 不影响全局 `LOG_LEVEL` 设置

4. **审计日志**
   - 每次 start/stop 写入 `audit_log` 表：`who, what, targetPlayerId, reason, timestamp`
   - 超时自动停止并记录 `expired` 事件

5. **TTL 清理**
   - 启动时启动 60s 循环检查，过期目标自动移除并记录 audit

**验收**：
- [x] 对指定 playerId 开启 debug 采样后，该玩家的 debug 日志出现在日志流中
- [x] 30 分钟后采样自动停止，debug 日志不再输出
- [x] Admin API 可查看活跃采样列表和历史记录
- [x] Audit 日志记录每次 start/stop/expired

---

### 3.2 B组 — 集中日志平台（2项，3~4d）

#### P1-B1：Loki 集中日志平台对接（D-L1-04）

**背景**：当前日志通过 pino 写文件 + stdout，生产排查需要 ssh 登入服务器 tail。需要一个集中日志平台支持全文检索。

**任务**：

1. **Loki 日志推送器**
   - 新建 `server/src/logTransportLoki.ts`
   - 使用 `pino-loki` transport
   - 配置：`LOKI_HOST`（默认 `http://localhost:3100`）、`LOKI_BASIC_AUTH`、`LOKI_LABELS`（`{ service: 'fish-social-server', env: '${NODE_ENV}' }`）
   - 批量推送：每 5s / 100 条，先入内存队列
   - `LOKI_ENABLED` 环境变量控制开关

2. **日志结构化增强**
   - pino logger 输出时自动附加 Loki 兼容 labels：`service`, `env`, `eventType`, `level`
   - 确保 `correlationId`, `playerId`, `pondId` 等字段作为结构化 labels 传入

3. **Docker Compose 配置**
   - 新增 `docker/docker-compose.monitoring.yml`（可选，不纳入核心部署）

4. **配置兜底**
   - Loki 不可用时不阻塞主服务（队列满则降级丢弃，打印 warn 日志）
   - 本地开发默认 `LOKI_ENABLED=false`

**验收**：
- [x] Loki 启用后，服务端日志出现在 Loki 中
- [x] 日志包含结构化 labels：`service`, `env`, `eventType`, `correlationId`, `playerId`, `pondId`
- [x] Loki 不可用时服务仍正常运行，有 warn 日志
- [x] 提供可选 docker-compose.monitoring.yml

---

#### P1-B2：日志合规（D-L1-09）

**背景**：日志可能包含用户数据（playerId、昵称、聊天记录），需要合规保留策略。

**任务**：

1. **日志保留策略**
   - 文件日志：保留 30 天（`LOG_RETENTION_DAYS=30`），超期自动清理脚本
   - Loki 日志：保留 14 天（通过 Loki 配置 `retention_period`）
   - error_logs 表：保留 90 天（已有）

2. **敏感数据屏蔽**
   - Logger 输出前对 `fields.text`、`fields.nickname` 等用户内容字段做 hash 或屏蔽（仅保留前 2 字符 + `***`）
   - `playerId` 保留明文（技术排障必需）
   - 配置 `LOG_MASK_USER_DATA=true` 启用

3. **合规审计清单**
   - 新建 `docs/compliance/log-audit-checklist.md`

**验收**：
- [x] 日志文件超过 30 天自动清理（脚本可用）
- [x] Loki retention 配置为 14 天
- [x] 用户聊天内容在日志中被屏蔽（nickname/text）
- [x] 合规审计清单文档可查阅

---

### 3.3 C组 — 运维监控（3项，5d）

#### P1-C1：Prometheus RED 指标端点（D-L2-05）

**任务**：

1. **Prometheus 指标端点**
   - 新建 `server/src/metricsPrometheus.ts`
   - 使用 `prom-client` 库
   - `GET /metrics` 端点（仅在 `METRICS_PROMETHEUS_ENABLED=true` 时挂载）

2. **核心 RED 指标**：HTTP 请求率/错误率/延迟、Socket 事件率、咬钩检测率、慢查询率、生态 tick 耗时

3. **业务指标**：活跃连接数、各塘在线人数、metrics 队列深度、在线玩家数、Bot 数

4. **集成方式**
   - 在 `createApp.ts` 条件挂载 `/metrics` 路由
   - 已有计时逻辑直接写入 Prometheus 直方图
   - `fishingMetrics.ts` 写入时同步更新 Prometheus counter

5. **配置**：`METRICS_PROMETHEUS_ENABLED`（默认 false），`METRICS_PROMETHEUS_PORT`（默认 3002）

**验收**：
- [x] `GET /metrics` 返回 Prometheus 格式指标
- [x] 核心 RED 指标全面覆盖
- [x] 业务指标（在线连接数、各塘人数）存在
- [x] 默认关闭，不影响现有部署

---

#### P1-C2：Grafana 看板与预置仪表盘（D-L2-06）

**任务**：

1. **预置 Grafana Dashboard JSON**
   - 新建 `docs/monitoring/grafana-dashboards/`
   - `fish-social-overview.json`：在线玩家、请求率、错误率、slow query、咬钩检测
   - `fish-social-pond-detail.json`：四塘各自详情

2. **文档说明**：`docs/monitoring/README.md`

**验收**：
- [x] 两个预置 Dashboard JSON 文件存在且语法正确
- [x] Overview 仪表盘覆盖 5 个行区
- [x] 导入文档清晰可用

---

#### P1-C3：告警通知（D-L1-07）

**任务**：

1. **告警规则**：`docs/monitoring/alert-rules/prometheus.yml`（5 条）+ `loki.yml`（1 条）
2. **通知通道**：支持 Webhook URL 通知（`ALERT_WEBHOOK_URL`），支持钉钉/企微/generic
3. **测试脚本**：`scripts/test-alert.sh`

**验收**：
- [x] Prometheus 告警规则文件覆盖 5 个场景
- [x] Loki 告警规则文件覆盖 error log 场景
- [x] 通知渠道支持 Webhook（至少 generic JSON）

---

### 3.4 D组 — 实时排障（2项，4~5d）

#### P1-D1：Live Session Inspector（D-L2-13）

**任务**：

1. **SSE 端点**：`GET /api/admin/live-session?playerId=xxx&token=<admin-token>`
2. **推送内容**：phase、计时、咬钩计数、最近事件、塘人数等
3. **服务端推送循环**：`server/src/liveSessionInspector.ts`，每秒推送，最多 5 连接
4. **Admin UI**：Live Session Tab，输入 playerId 实时查看

**验收**：
- [x] SSE 端点可连接，每秒推送一次数据
- [x] 同时最多 5 个连接
- [x] Admin UI 可输入 playerId 开始/停止实时查看

---

#### P1-D2：客户端日志上报（D-L2-12）

**任务**：

1. **客户端日志收集器**：`mobile/lib/clientLogger.ts`
2. **上报 API**：`POST /api/client-logs`
3. **客户端自动采集事件**：Socket 连接/断开、前后台切换、join/leave pond、咬钩、导航等
4. **Migration `client_logs` 表**
5. **Admin 查询 API**：`GET /api/admin/client-logs?playerId=`

**验收**：
- [x] 移动端启动后自动建立日志队列，每 10 秒批量上报
- [x] 服务端 `client_logs` 表记录客户端事件
- [x] Admin 可查询指定玩家的客户端日志

---

### 3.5 E组 — 安全管理（1项，2d）

#### P1-E1：Admin RBAC 角色权限（D-L2-07）

**任务**：

1. **角色定义**：viewer（只读）/ operator（操作）/ admin（完整）
2. **配置**：`ADMIN_RBAC_RULES` 环境变量 JSON
3. **中间件**：`requireRole('operator')` / `requireRole('admin')`
4. **权限矩阵**：按角色限制 Admin API 访问
5. **兼容**：无配置时 `ADMIN_SECRET` 视作 admin（向后兼容）

**验收**：
- [x] 三种角色行为符合权限矩阵
- [x] 无 RBAC 配置时向后兼容
- [x] 越权操作返回 403

---

## 4. 实施路线图

### 4.1 推荐开发顺序

```
Phase 0 已完成
    │
    ├── Sprint 1 (5d): 基础补完与监控
    │   ├── P1-A1 correlationId 入库与 Admin 过滤 (1d)
    │   ├── P1-C1 Prometheus RED 指标 (1.5d) ← 可与 A1 并行
    │   └── P1-E1 Admin RBAC (2d) ← 可与 A1 并行
    │
    ├── Sprint 2 (5d): 集中日志与告警
    │   ├── P1-B1 Loki 对接 (2~3d)
    │   ├── P1-B2 日志合规 (1d) ← 依赖 B1
    │   └── P1-C3 告警通知 (2d) ← 依赖 B1 + C1
    │
    ├── Sprint 3 (5d): 实时排障
    │   ├── P1-D1 Live Session Inspector (2~3d) ← 依赖 A1
    │   ├── P1-D2 客户端日志上报 (2d) ← 可与 D1 并行
    │   └── P1-A2 动态 DEBUG 采样 (2d) ← 依赖 A1，可与 D 并行
    │
    └── Sprint 4 (2d): 收尾与看板
        └── P1-C2 Grafana 看板 (1.5d) ← 依赖 C1 + B1
```

### 4.2 并行建议

| 并行组 | 任务 | 说明 |
|--------|------|------|
| 组 1 | A1 + C1 + E1 | 无交叉依赖，各自独立 |
| 组 2 | B1 + (D1 准备) | B1 先开工，D1 可从设计开始 |
| 组 3 | D1 + D2 + A2 | 可并行，但 A2 测试依赖 A1 完成 |
| 组 4 | C2 + 验收 | 最后收尾 |

---

## 5. 数据模型变更汇总

| 表/文件 | 变更类型 | 说明 |
|---------|----------|------|
| `fishing_metrics` | ALTER TABLE | 新增 `correlation_id` 列 |
| `client_logs` | 新建表 | 客户端日志存储 |
| `audit_log` | 新建表 | 审计日志 |
| `debug_sample_targets` | 内存结构 | 无需持久化 |
| `admin_rbac_rules` | 配置/环境变量 | JSON 文件或环境变量 |

---

## 6. 验收总则

每个 P0/P1 子任务验收后须运行以下回归：

```bash
npm run build:shared
npm run verify:data-platform-phase0
npm run verify:server-observability
npm run verify:auth
npm run verify:session-checkpoint
# 新增
npm run verify:phase1-core
```

---

## 7. 风险与依赖

| 风险 | 缓解 |
|------|------|
| Loki 部署增加运维复杂度 | Docker Compose 可选，本地可跳过；文档化部署步骤 |
| Prometheus 对现有服务影响 | 独立端口（3002），默认关闭 |
| RBAC 兼容性问题 | 无配置时完全向后兼容 |
| SSE 长连接资源占用 | 限制最多 5 个并发 Inspector |
| 客户端日志上报增加流量 | 配置上报频率和级别，队列积压时丢弃 |

---

## 8. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-11 | 策划 | Phase 1 初稿：10 项需求分 5 组，Sprint 4 周规划 |
