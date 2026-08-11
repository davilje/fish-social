<!-- 来源: docs/planning/specs/后端优化-C-停机与稳定性.md -->
<!-- 用途: @backend-dev — BE-OPT-C（A/B 已完成后下一刀） -->

你是 Fish Social **后端工程师**。实现 **BE-OPT-C**（STAB-01～STAB-06）。

## 必读（按序）

1. `docs/planning/specs/后端优化-Kickoff-C与D.md`
2. `docs/planning/specs/后端优化-C-停机与稳定性.md`（含 §2.2 SSE query 例外）
3. 代码：`server/src/index.ts`（shutdown）· `createApp.ts`（`/health`）· `admin.ts`（`requireAdmin`）· `errorLog.ts` · `socketPondHandlers.ts` · `playerPondSession.ts`

## 前置已完成（勿重做）

- BE-OPT-A 安全收口 · BE-OPT-B 热路径（含 `session_timer_tick`）

## 实现顺序（强制）

1. **STAB-05** 生产 REST 禁 `query.key`；**仅** `/api/admin/live-session` 保留 query + 审计日志（无密钥明文）
2. **STAB-06** `/health` 在 `shuttingDown` → 503 + `{ ok:false, draining:true }`
3. **STAB-01** shutdown 扫在塘真人 `upsertPlayerPondSession`
4. **STAB-02** await OTEL；PG `pool.end`（若有）；metrics 先停
5. **STAB-03** `uncaughtException` → shutdown → exit(1)；保留 EPIPE
6. **STAB-04** Socket 事件限流（`SOCKET_EVENT_RATE_PER_SEC`，默认 20）；超限 ack `rate_limited` + 埋点

## 建议交付物

- `scripts/verify-backend-opt-c.ts` + `package.json`：`verify:backend-opt-c`
- `docs/ops/` 一句：停机中 health/ready 均为不可调度
- `.env.example` 补新变量

## 不做

BE-OPT-D 大拆分 · FEAT-05 · 千人多机 · 全站取消 SSE query（见 spec §2.2）

## 验收后

计划表 **BE-OPT-C** → **已实现** + 完成时间 → `npm run planning:master-xlsx`  
再开 D：`backend-opt-d-quality-dev.prompt.md` 或先 `backend-opt-d1-vitest-dev.prompt.md`
