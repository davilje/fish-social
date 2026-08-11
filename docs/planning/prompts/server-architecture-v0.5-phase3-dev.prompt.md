<!-- 来源: docs/planning/specs/服务器架构优化路线图-v0.5.md -->
<!-- 用途: v0.5 第三期 P2 — 性能 / Admin安全 / Bot配置 -->

你是 Fish Social **后端开发 Agent**。实现 **v0.5 第三期（P2 性能与安全）**。

## 前置

- 第一、二期已完成
- 必读：`docs/planning/specs/服务器架构优化路线图-v0.5.md` §3.3
- xlsx sheet：`10-R2-1` ~ `14-R2-5`

---

## 任务 I — R2-1 snapshot 增量

1. `gameState`：`markUserDirty(pondId, userId)`；phase/占座变更时标记
2. 1s 广播循环：只 `emit` dirty 用户，emit 后 clear dirty set
3. `waitingUsersByPond: Map<pondId, Set<userId>>`；进出 `waiting` 时维护
4. 咬钩循环只迭代 `waitingUsersByPond`，不全塘 `buildSnapshot`
5. 用 `snapshot_build_duration_ms` 对比优化前后

---

## 任务 J — R2-2 metrics 批量写

1. `fishingMetrics`：内存队列 + 每 1s 或满 50 条 `INSERT` 批量
2. `recordFishingMetric` → enqueue；`shutdown` 时 drain
3. `pondEcology` tick 写合并为单事务（若尚未）

---

## 任务 K — R2-4 Admin / CORS 安全

1. `NODE_ENV=production` 且未设置 `ADMIN_SECRET` → `process.exit(1)` 并打印明确错误
2. `ALLOWED_ORIGINS` 环境变量（逗号分隔）；未设时 development 用 `*`，production 拒绝 `*`
3. 破坏性 Admin（clear users、ecology reset）写审计日志行 `[admin_audit]`

---

## 任务 L — R2-5 Bot 配置

1. runtime config：`MAX_BOTS_PER_POND`、`BOT_EVICT_POLICY`（可选）
2. Admin overview 展示 bot 占比
3. 确认 bot 咬钩已走统一路径（第一期）；本任务仅配置与观测

---

## 任务 M — R2-3 单实例（仅文档）

**不写多实例代码**。在 `docs/planning/specs/服务器架构优化路线图-v0.5.md` 或 `docs/deploy.md` 补充：

- 建议单实例并发上限（如活跃 socket < 200，按压测调整）
- 多实例前置：checkpoint、Redis adapter、分布式锁
- 监控项：连接数、`tick_fishing_phases_duration_ms`

xlsx R2-3 状态改为 **已文档化**。

---

## 验收

- perf 日志显示 snapshot 调用次数下降
- metrics 同步 insert 消失（批量）
- production 无默认 Admin 密钥启动失败
- 全套 verify 通过

---

## 完成后

更新 spec 第三期、xlsx R2-*、CHANGELOG。

## commit 建议

```text
perf(server): v0.5 phase3 incremental broadcast, metrics batch, admin hardening

Reduce snapshot fanout, batch metric writes, enforce production secrets,
and document single-instance capacity limits.
```
