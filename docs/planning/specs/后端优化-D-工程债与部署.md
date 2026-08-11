# 后端优化 D — 工程债与部署

| 字段 | 内容 |
|------|------|
| 功能名称 | 单测补强 · 模块拆分 · PG 读就绪 · env/Docker · 运维面收敛 |
| 编号 | **BE-OPT-D** |
| 子项 | QUAL-01～QUAL-10 |
| 状态 | **已实现** |
| 设计时间 | **2026-07-12** |
| 完成时间 | **2026-07-12** |
| 优先级 | **P2** |
| 工期估 | 2～4 人天（拆 5 个 PR） |
| 前置 | **建议 BE-OPT-C 合入后再开**（避免 `index`/`createApp`/`admin` 冲突）；A/B 已实现 |
| 总表 | [`后端优化-问题汇总与分批计划.md`](./后端优化-问题汇总与分批计划.md) |
| Kickoff | [`后端优化-Kickoff-C与D.md`](./后端优化-Kickoff-C与D.md) |
| 开发提示词 | 总：[`backend-opt-d-quality-dev.prompt.md`](../prompts/backend-opt-d-quality-dev.prompt.md) · 切片见 §2 |

---

## 1. 背景与目标

### 1.1 背景

A/B 收口安全与热路径后，剩余多为**可维护性与部署脚枪**：vitest 薄、大文件、PG 双写不可切读、compose CORS、运维静态页与 API 同端口、日志默认不掩码。

### 1.2 目标（按 PR 切片）

| 切片 | 子项 | 目标 | Prompt |
|------|------|------|--------|
| **D1** | QUAL-01 | vitest：`auth` / `sessionRegistry` / `humanCapacity` 至少各 1 文件 | [`backend-opt-d1-vitest-dev.prompt.md`](../prompts/backend-opt-d1-vitest-dev.prompt.md) |
| **D2** | QUAL-03 | PG `insertBatch` 真批量；`METRICS_READ_FROM=postgres` 可用或启动拒绝 | [`backend-opt-d2-pg-dev.prompt.md`](../prompts/backend-opt-d2-pg-dev.prompt.md) |
| **D3** | QUAL-04/05/06/10 | `env` 清单 · compose 去 `*` · Node 对齐文档 · 双容量说明 | 含在总 prompt D3 段 |
| **D4** | QUAL-07/08 | 生产默认日志掩码；`OPS_STATIC_ENABLED` 或鉴权门 | 含在总 prompt D4 段 |
| **D5** | QUAL-02/09 | 拆 `admin` 路由组 **或** stateMachine 生命周期；debug cache 上限 | 含在总 prompt D5 段 |

### 1.3 非目标

- S4 多机代码（BE-OPT-E）
- FEAT-05
- 一次 PR 拆光所有大文件

---

## 2. 范围与验收

| # | 子项 | 验收标准 |
|---|------|----------|
| 1 | QUAL-01 | `npm test -w server` ≥ +3 有意义 spec；CI 绿 |
| 2 | QUAL-02 | 至少一个模块拆文件且 `verify:server-boot` + 相关 verify 不回归 |
| 3 | QUAL-03 | 批量 INSERT（单语句或多行）；切读要么 query 通，要么启动 assert 禁止 |
| 4 | QUAL-04 | `server/src/env.ts`（或 `docs/ops/server-env.md`）列出关键 env；`.env.example` 同步 |
| 5 | QUAL-05 | `docker-compose.yml` 默认无 `ALLOWED_ORIGINS=*`；ops 写必填示例 |
| 6 | QUAL-06 | Dockerfile 注释 Node 主版本；若未 compile 产物则 CHANGELOG「已知债」 |
| 7 | QUAL-07 | `OPS_STATIC_ENABLED=false`（或等价）时生产不挂 `/planning`；默认文档写清「仅内网」 |
| 8 | QUAL-08 | `NODE_ENV=production` 时默认掩码 playerId/nickname（可用 env 关闭） |
| 9 | QUAL-09 | `fishingDebugCache`（及同类）设 max entries 或定期扫 |
| 10 | QUAL-10 | `docs/ops/` 一节：`MAX_SOCKET_CONNECTIONS` vs `MAX_HUMAN_SOCKETS` 推荐值 |

**整包门禁**：D1 **必做**；D2～D5 可分期，未做项 CHANGELOG 标「延期」后方可将 BE-OPT-D 标已实现（或仅在 D1+D3+D4 做完且 D2/D5 延期时标已实现并注明范围）。

---

## 3. 技术影响

- `server/src/__tests__/*`
- `server/src/admin*.ts` / `fishingStateMachine*.ts`
- `server/src/postgresMetricsStore.ts` · `metricsStore.ts`
- `Dockerfile` · `docker-compose.yml` · `.env.example`
- `docs/ops/server-env.md`（新建）或扩现有 ops 文
- `server/src/logger.ts` · `createApp.ts` 静态挂载

---

## 4. 验收清单

- [x] D1 完成；其余按 §1.2 勾选
- [x] `npm test -w server` · 相关 `verify:*` 绿
- [x] 计划表 BE-OPT-D → 已实现 → `npm run planning:master-xlsx`

---

## 5. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-12 | 已实现：D1～D5（vitest×3 · PG 批量+禁切读 · env/compose/Dockerfile · OPS_STATIC · 日志掩码 · admin ecology 拆分 · debug cache 上限） |
| 2026-07-12 | 已确认；QUAL-01～10 |
| 2026-07-12 | A/B 后细化：PR 切片 D1～D5、独立 D1/D2 prompt、门禁规则 |
