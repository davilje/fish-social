<!-- 来源: docs/planning/specs/服务器架构缺陷与埋点设计-v0.4.4.md -->
<!-- 用途: 给后端开发 Agent 实现服务端 observability / metrics 补点 -->

你是 Fish Social 后端开发 Agent。实现服务端 observability 第一阶段，依据 v0.4.4 spec 补齐架构关键埋点。

必读：

- `docs/planning/specs/服务器架构缺陷与埋点设计-v0.4.4.md`
- `docs/planning/specs/BUG修复-挂机断线离位.md`
- `docs/planning/specs/排查-挂机断线诊断阶段2-4.md`

## 目标

把“挂机 / 断线 / 重连 / 状态机迁移”这条链路打通，做到：

1. timeline 可还原 `join -> phase transition -> disconnect -> reconnect/timeout`
2. 每个关键事件都带 `playerId/pondId/reason`
3. 后端能区分 join 失败、phase 漂移、主动 leave、timeout 清场

## 本次范围（只做 P0）

### 任务 1 — join/socket 事件

在 `server/src/index.ts` 增加结构化日志和 metrics：

- `join_pond_attempt`
- `join_pond_success`
- `join_pond_fail`
- `socket_connect`
- `socket_connect_error`（若可直接接入）

要求：

- 统一字段：`ts`, `playerId`, `userId`, `socketId`, `pondId`, `spotId`, `reason`
- reconnect 成功时 `joinKind='reconnect'`，首次 join 为 `joinKind='fresh'`

### 任务 2 — 统一 phase transition 埋点

在 `server/src/fishingStateMachine.ts` 增加统一 helper，例如：

```ts
recordPhaseTransition({
  playerId,
  userId,
  pondId,
  spotId,
  fromPhase,
  toPhase,
  cause,
  phaseElapsedMs,
  phaseDeadlineTs,
});
```

覆盖至少：

- `idle -> seated`
- `seated -> baiting`
- `baiting -> casting`
- `casting -> waiting`
- `waiting -> hooked`
- `hooked -> resolving`
- `resolving -> baiting|seated|idle`
- `* -> disconnected`
- `disconnected -> waiting|seated|idle|hooked|resolving`

事件名：`fishing_phase_transition`

### 任务 3 — metrics 类型与 timeline

在 `server/src/fishingMetrics.ts`：

- 扩展 `FishingMetricEvent`
- 支持上述新事件写入
- 扩展 `getPlayerFishingTimeline()`，让 phase transition 能出现在玩家时间线中

### 任务 4 — 保持兼容

- 不删除现有 `disconnect/reconnect/leave_pond/bite_hook/catch_accept`
- 新事件是补充，不破坏现有 Admin summary
- timeline 遇到旧数据应可正常工作

## 不做

- P1 的 `bite_tick_miss/hit`
- P1 的 `pending_catch_created/expired`
- P2 的 tick 性能聚合
- 多实例改造

## 建议实现文件

- `server/src/index.ts`
- `server/src/fishingStateMachine.ts`
- `server/src/fishingMetrics.ts`
- 可选：`server/src/admin.ts`（若 timeline summary 需要展示新字段）

## 验收

至少补 1 个 verify 脚本或扩展现有脚本，证明：

1. `join_pond_success/fail` 可落库或进入 timeline
2. `disconnect -> reconnect` 之间存在 phase transition
3. `hooked` 重连恢复链路能看到 transition

## commit 建议

```text
feat(server): add phase transition and join observability

Record join lifecycle and fishing phase transitions so AFK
disconnect issues can be reconstructed from player timelines.
```
