# 后端优化 C — 停机与稳定性

| 字段 | 内容 |
|------|------|
| 功能名称 | 优雅停机补全 · Socket 事件限流 · 致命错误退出 · Admin 密钥 |
| 编号 | **BE-OPT-C** |
| 子项 | STAB-01～STAB-06 |
| 状态 | **已实现** |
| 设计时间 | **2026-07-12** |
| 完成时间 | **2026-07-12** |
| 优先级 | **P1** |
| 工期估 | 1～2 人天 |
| 前置 | **BE-OPT-A / BE-OPT-B 已实现**；本批为后端优化下一刀 |
| 总表 | [`后端优化-问题汇总与分批计划.md`](./后端优化-问题汇总与分批计划.md) |
| Kickoff | [`后端优化-Kickoff-C与D.md`](./后端优化-Kickoff-C与D.md) |
| 开发提示词 | [`backend-opt-c-stability-dev.prompt.md`](../prompts/backend-opt-c-stability-dev.prompt.md) |

---

## 1. 背景与目标

### 1.1 背景（对照当前代码）

| 现状 | 缺口 |
|------|------|
| `index.ts` `shutdown`：stopLoops → cancelTimers → flush metrics → `void shutdownOtelTracing()` → close io/http/db | 无 session 扫盘；OTEL 未 await；无 PG `pool.end` |
| `/ready` 在 `shuttingDown` 返回 503 | `/health` 仍永远 `{ ok: true }`（`createApp.ts`） |
| `requireAdmin`：`X-Admin-Key` **或** `query.key` | 生产密钥可进访问日志；Live Inspector SSE 依赖 query（EventSource 难设 Header） |
| Socket 有 `MAX_SOCKET_CONNECTIONS` | 无 per-event 限流 |
| `uncaughtException` 多只记日志 | 进程可能半死继续服务 |

### 1.2 目标

1. SIGTERM 时 best-effort 持久化在塘真人的 `player_pond_session`
2. 关闭前等待 OTEL；有 PG 则 `pool.end()`；metrics 队列先停
3. 致命未捕获异常 → 有序 shutdown → 非 0 退出（保留 EPIPE 特例）
4. 易刷 Socket 事件 per-socket 限流（env 可配）
5. 生产 REST 拒绝 `?key=`；SSE 例外策略见 §2.2
6. `/health` 与 `/ready` 在 draining 时一致不可调度

### 1.3 非目标

- 多实例排水 / Redis（BE-OPT-E）
- Admin 路由大拆分（BE-OPT-D）
- 改 FEAT-05 / 咬钩公式

---

## 2. 范围与验收

| # | 子项 | 改动要点 | 涉及文件（锚点） | 验收 |
|---|------|----------|------------------|------|
| 1 | STAB-05 | 生产：普通 Admin REST **禁止** `query.key`，仅 Header。见 §2.2 SSE 例外 | `admin.ts` `requireAdmin` / `requireRole` | 生产 REST `?key=` → 401；Header 仍通 |
| 2 | STAB-06 | `/health` 在 `shuttingDown` → **503** + `{ ok:false, draining:true }`（与 ready 对齐） | `createApp.ts` | 停机中 curl health 非 200 |
| 3 | STAB-01 | shutdown 在 stopLoops 前后：遍历在塘非 bot 用户，调 `upsertPlayerPondSession`（`playerPondSession.ts`） | `index.ts` · `pondUserManager` list API | SIGTERM 后 DB `updated_at` 新鲜 |
| 4 | STAB-02 | `await shutdownOtelTracing()`；导出并 `await closePostgresPool()`（若存在）；先 `stopFishingMetricsQueue` | `index.ts` · `otelTracing.ts` · `postgresMetricsStore.ts` | shutdown 日志 phase 齐全；无 unhandledRejection |
| 5 | STAB-03 | `uncaughtException`：log → `shutdown('uncaughtException')` → 超时 `exit(1)`；EPIPE 保持现逻辑 | `errorLog.ts` · `index.ts` | 注入致命错误退出码 ≠ 0 |
| 6 | STAB-04 | 对 `join_pond` / `chat_message` / 抛竿或等价高频事件：每 socket 滑动窗口限流；超限 ack `{ ok:false, error:'rate_limited' }` 并 `logStructuredEvent` | `socketPondHandlers.ts` 或 middleware | 脚本狂发被限；正常玩法不误伤 |

### 2.1 建议环境变量

| 变量 | 默认 | 说明 |
|------|------|------|
| `SOCKET_EVENT_RATE_PER_SEC` | `20` | 单连接关键事件合计上限（可再分子类） |
| `SHUTDOWN_TIMEOUT_MS` | 已有则沿用 | 强制 exit 超时 |
| `ADMIN_ALLOW_QUERY_KEY` | 非 production 默认 true；production 默认 false | 显式开关；SSE 见下 |

### 2.2 Admin SSE 与 query key（设计决策）

`EventSource` 无法自定义 Header，Live Inspector 现用 `?key=`。

**本批约定**：

1. **REST**（JSON Admin API）：production **禁止** query key  
2. **SSE** `GET /api/admin/live-session`：允许 query key **仅此一路**，且必须：  
   - 打 `admin_sse_query_key_used` 审计日志（无密钥明文）  
   - 文档注明：公网部署应经内网/反向代理，勿把 Admin URL 暴露公网  
3. 可选后续（非本批必做）：短期 SSE ticket（POST 换 ticket → EventSource 带 ticket）

### 2.3 建议新增 verify

`scripts/verify-backend-opt-c.ts` + `npm run verify:backend-opt-c`：

- production 模拟：`requireAdmin` 对 query 拒绝（可用 NODE_ENV=production 子进程或抽纯函数测）  
- `/health` draining：`setShuttingDown(true)` 后期望 503  
- Socket 限流：可选轻量测或文档手工步骤  

---

## 3. 技术影响

- `server/src/index.ts`
- `server/src/errorLog.ts`
- `server/src/admin.ts` · `liveSessionInspector`（若鉴权共用）
- `server/src/createApp.ts`
- `server/src/socketPondHandlers.ts` / `socketLifecycle.ts`
- `server/src/postgresMetricsStore.ts` · `otelTracing.ts`
- `docs/ops/` 补一句：探活请同时看 `/ready`；停机中 `/health` 亦 503
- `admin-web` Live Inspector：保持 query key（与 §2.2 一致）；勿改回全站 query

---

## 4. 验收清单

- [x] STAB-01～06 完成（含 §2.2 SSE 例外）
- [x] `verify:server-boot` · `verify:capacity-limit` · 建议 `verify:backend-opt-c` 绿
- [ ] 手工 SIGTERM 一次：进程退出、session 有 flush、无卡死
- [x] 计划表 BE-OPT-C → **已实现** + 完成时间 → `npm run planning:master-xlsx`

---

## 5. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-12 | 已实现：STAB-01～06（session flush · await OTEL/PG · fatal shutdown · socket 限流 · Admin query 禁 REST / SSE 例外 · health draining） |
| 2026-07-12 | 已确认；STAB-01～06 |
| 2026-07-12 | A/B 已实现后细化：代码锚点、SSE query 例外、verify 建议、Kickoff |
