<!-- 来源: docs/planning/specs/排查-挂机断线诊断阶段2-4.md -->
<!-- 用途: 实现挂机断线排查阶段 2–4（客户端观测 + leave_pond 埋点 + Admin 时间线） -->

你是 Fish Social 开发 Agent。实现挂机断线**排查与归因**能力（阶段 2–4），便于区分：弱网断线、超时踢人、主动 leave_pond。

必读 spec：

- `docs/planning/specs/排查-挂机断线诊断阶段2-4.md`
- `docs/planning/specs/BUG修复-挂机断线离位.md`（P0 前置，disconnect/reconnect 已存在）

## 背景

P0 已修复重连未取消 60s 定时器。仍须：

1. **阶段 2**：客户端 Socket + AppState 日志，执行后台/弱网测试矩阵
2. **阶段 3**：`leave_pond` 带 `reason` 埋点，排除「用户自己切页」
3. **阶段 4**：按 `player_id` 查 `fishing_metrics` 时间线 API + SQL 索引

## 任务 1 — 阶段 3 服务端 leave_pond 埋点（优先）

**`server/src/index.ts`** `leave_pond` 处理器：

- 接受 `string | { pondId, reason? }`
- 日志 `[leave_pond]`：playerId, userId, pondId, spotId, fishingPhase, reason, ts
- `recordFishingMetric('leave_pond', { playerId, pondId, payload: { reason, spotId, fishingPhase } })`

**`server/src/fishingMetrics.ts`**：扩展 `FishingMetricEvent` 加 `'leave_pond'`。

## 任务 2 — 阶段 3 客户端 reason

**`shared/types.ts`**：扩展 `leave_pond` 载荷与 `LeavePondReason` 类型。

**`mobile/lib/usePondSocket.ts`**：

- cleanup：`leave_pond({ pondId, reason: 'unmount' })`
- 导出 `leavePondWithReason(reason)` 供页面调用

**`mobile/app/pond/[id].tsx`**：

- 返回地图 → `navigation_back`
- 社交 → `navigation_social`
- 资料 → `navigation_profile`

避免 navigation 与 unmount 双发 leave（幂等或只发一次）。

## 任务 3 — 阶段 4 Admin 单用户时间线

**`server/src/fishingMetrics.ts`**：

```typescript
getPlayerFishingTimeline(playerId: string, hours: number, limit: number)
```

返回 `events[]` + `summary`（disconnect/reconnect/disconnect_timeout/leave_pond 计数）。

**`server/src/admin.ts`**：

```
GET /api/admin/metrics/fishing/player/:playerId?hours=24&limit=500
```

**Migration**：`idx_fishing_metrics_player_time` on `(player_id, created_at DESC)`。

**`mobile/lib/adminApi.ts`** + **`AdminMetricsPanel.tsx`**（建议）：player_id 输入 + 时间线表格。

## 任务 4 — 阶段 2 客户端生命周期日志

**`mobile/lib/usePondSocket.ts`**（或 `pondLifecycleLog.ts`）：

- `[pond-socket]` connect / disconnect(reason) / connect_error / join_pond_ok|fail
- `[pond-app]` AppState background/foreground；Web `visibilitychange`

**`mobile/app/pond/[id].tsx`**（可选）：`__DEV__` 或 admin 模式显示连接探针一行。

## 任务 5 — 测试与文档

- 手动：T1 社交离开有 `leave_pond`；M7/M8 断网序列符合 spec
- 更新 spec 状态 → **已实现**
- CHANGELOG v0.4.3

## 不改

- `DISCONNECT_TIMEOUT_MS`
- 后台 Foreground Service
- 客户端日志远程上报（仅 console + 服务端 metrics）

## 完成后 commit 建议

```
feat(diag): pond leave_pond reasons and player fishing timeline API

Add client socket lifecycle logs and admin endpoint to correlate
AFK disconnect reports by player_id.
```

## 自检

- [ ] `leave_pond` metric 含 reason
- [ ] player timeline API 按时间 ASC/DESC 一致且 summary 正确
- [ ] SQL 索引 migration 已应用
- [ ] M7/M8 或等效手动测试有记录
- [ ] shared types 向后兼容 string pondId
