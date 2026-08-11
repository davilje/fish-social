# BUG 修复：挂机断线 / 离开钓位

| 状态 | **已实现** | 目标版本 v0.4.2 |
|------|------------|-----------------|
| 优先级 | P0 | 体验阻断（挂机核心路径） |
| 范围 | **服务端为主**（P0）；客户端观测与后台保活（P1，另开任务） |
| 关联 | [`状态机需求描述.md`](./状态机需求描述.md) §4.4.10 · §5.10 · [`C-调优与状态机.md`](./C-调优与状态机.md) |

---

## 1. 问题概述

玩家在鱼塘挂机钓鱼时，出现以下现象（可单独或叠加出现）：

| # | 用户感知 | 技术含义 |
|---|----------|----------|
| 1 | 挂着挂着断线 | WebSocket 断开；`connected=false` 或服务端会话丢失 |
| 2 | 挂着挂着离开钓位 | `spotId` 变为 `null`（角色从码头移到塘边），或从鱼塘在线列表消失 |
| 3 | 挂着但不咬钩 | 连接仍在，但长时间无咬钩事件（可能与断线窗口重叠） |

**架构前提**：鱼塘在线状态、`spotId`、钓鱼阶段均在服务端内存（`pondUsers` Map）；SQLite **不持久化钓位**。服务端重启或超时清场后，玩家必须重新占座。

---

## 2. 根因分析（开发对照）

### 2.1 【P0 已确认】重连未取消断线定时器

**现象**：短暂断网后 UI 恢复，约 60 秒后仍被踢出鱼塘、钓位释放。

**代码路径**：

```
socket disconnect
  → index.ts: handleDisconnect()     // 启动 60s setTimeout
  → index.ts: markUserDisconnected()

客户端 reconnect → join_pond
  → index.ts: reconnectSession()     // 恢复 phase / spotId
  → ⚠️ 未调用 cancelDisconnectTimer / restoreDisconnectedUser

60s 后（自首次 disconnect 起算）
  → removeDisconnectedUser()           // 删除用户、释放钓位
  → emit pond_user_left
```

| 文件 | 符号 | 问题 |
|------|------|------|
| `server/src/fishingStateMachine.ts` | `handleDisconnect` | 正确启动 `disconnectTimers` |
| `server/src/fishingStateMachine.ts` | `cancelDisconnectTimer` | 已实现，**未被调用** |
| `server/src/fishingStateMachine.ts` | `restoreDisconnectedUser` | 已实现，**未被调用** |
| `server/src/gameState.ts` | `reconnectSession` | 手写恢复逻辑，**未取消定时器** |
| `server/src/index.ts` | `join_pond` 重连分支 | 调用 `reconnectSession` 后未走统一恢复 |

**与规格冲突**：[`状态机需求描述.md`](./状态机需求描述.md) §5.10 要求「60s 内重连成功：恢复原 phase」；当前实现在重连后仍会在原定时器到期时强制清场。

---

### 2.2 【P1 设计行为】60 秒断线超时

| 常量 | 值 | 位置 |
|------|-----|------|
| `DISCONNECT_TIMEOUT_MS` | 60_000 ms | `server/src/fishingStateMachine.ts` |
| `markUserDisconnected` 的 `phaseEndsAt` | +60_000 ms | `server/src/gameState.ts` |

移动端后台、弱网、WiFi 切换可能导致 Socket 断开。若 **超过 60s 未能完成重连**，按规格释放钓位属预期行为，非 Bug。

断线期间：
- `tickFishingPhases` 跳过 `disconnected` 用户 → 阶段冻结
- `processWaitingBiteTick` 要求 `socketByUserId` 存在 → **不咬钩**

---

### 2.3 【P1 客户端】页面卸载主动离塘

`mobile/lib/usePondSocket.ts` 在组件 unmount 时：

```typescript
socket.emit('leave_pond', pondId);
socket.disconnect();
```

用户切换至社交 / 地图 / 资料等页面会立即释放钓位。**本 spec 服务端任务不覆盖**；客户端另开 P1 任务（见 §6）。

---

### 2.4 【P2】服务端重启 / 热重载

`tsx watch` 或部署重启清空 `pondUsers`。挂机期间若进程重启，所有玩家从鱼塘消失。需运维规避；长期方案为钓位持久化（§6.3）。

---

### 2.5 【P1 已实现】重连后 `hooked` 阶段处理

规格 §5.10：重连时若 `hookEndsAt` 已过期，应立即进入 `resolving`。

`handleDisconnect` 在 `hooked` 时将 `phaseEndsAt` 存入 `hookContext.hookEndsAt`；`resumeAfterReconnect` 重连时若已过期则复用 `advanceFromHooked`，否则恢复 `hooked` 并保留剩余计时。

---

### 2.6 辅助：`markUserDisconnected` 重复写入

`index.ts` 的 `disconnect` 处理器在 `handleDisconnect`（已 `updatePondUser`）之后再次调用 `markUserDisconnected`，逻辑重复。修复 P0 时可一并收敛，非阻断项。

---

## 3. 产品需求

### 3.1 断线重连（必须，P0）

| # | 需求 | 验收 |
|---|------|------|
| R1 | 60s 内成功 `join_pond` 重连后，**必须取消**该用户的断线清场定时器 | 断网 10s → 恢复 → 再等满 60s（自首次断线计）→ 用户仍在塘、钓位保留 |
| R2 | 重连后恢复 `fishingPhase`：`fishing` → `waiting`；有 `spotId` 非钓鱼 → `seated`；无 `spotId` → `idle` | 与 [`restoreDisconnectedUser`](../../../server/src/fishingStateMachine.ts) 行为一致 |
| R3 | 重连后 `disconnectedAt = null`，`phaseEndsAt` 按恢复后的 phase 重算 | Admin debug 可见正确 phase |
| R4 | 重连成功向全塘广播 `pond_user_updated` | 他人 UI 角色不再灰色 |

**实现约束**：`reconnectSession` 应调用已有的 `restoreDisconnectedUser`，避免与 `fishingStateMachine` 双份逻辑漂移。

---

### 3.2 断线超时（保持现状，P0 仅文档化）

| # | 需求 | 验收 |
|---|------|------|
| R5 | 断线超过 60s 未重连：释放钓位、`removeDisconnectedUser`、广播 `pond_user_left` | 断网 >60s 后用户被清出 |
| R6 | 断线期间不扣额外饵、不退已扣饵 | 与规格 §5.10 一致 |

---

### 3.3 可观测性（必须，P0）

| # | 需求 | 验收 |
|---|------|------|
| R7 | 服务端结构化日志：`disconnect`、`reconnect`、`disconnect-timeout-remove`、`disconnect-timer-cancelled` | 含 `userId`、`playerId`、`pondId`、`spotId`、时间戳 |
| R8 | `fishing_metrics` 新增事件：`disconnect`、`reconnect`（可选 `disconnect_timeout`） | Admin metrics 可查询 |

---

### 3.4 重连中鱼恢复（P1，已实现）

| # | 需求 | 验收 |
|---|------|------|
| R9 | 断线前为 `hooked`，重连时若 `phaseEndsAt <= now`，立即进入 `resolving` 流程 | 模拟 hooked 中断网 5s 重连，鱼不丢失、不卡在 waiting |

---

## 4. 技术方案

### 4.1 P0 修复（最小 diff）

**文件 `server/src/gameState.ts` — `reconnectSession`**

在 `return user` 前调用：

```typescript
import { restoreDisconnectedUser } from './fishingStateMachine.js';

// reconnectSession 内，sessions.set 之后：
restoreDisconnectedUser(user);
updatePondUser(pondId, user);
```

或改由 `index.ts` 的 `join_pond` 重连分支在 `reconnectSession` 返回后调用 `restoreDisconnectedUser`。**二选一，禁止两处重复调用。**

**禁止**：在 `reconnectSession` 内保留与 `restoreDisconnectedUser` 重复的 phase 恢复代码；应删除重复分支，单一来源。

---

### 4.2 P0 日志与指标

**`server/src/fishingStateMachine.ts`**

- `handleDisconnect`：log + `recordFishingMetric('disconnect', …)`
- `restoreDisconnectedUser`：log `disconnect-timer-cancelled` + `recordFishingMetric('reconnect', …)`
- `removeDisconnectedUser` 调用前（timeout 回调）：log + `recordFishingMetric('disconnect_timeout', …)`

**`server/src/fishingMetrics.ts`**：扩展 `FishingMetricEvent` 联合类型（若使用 TypeScript 枚举/union）。

---

### 4.3 P1 hooked 重连（已实现）

`handleDisconnect` 在 `hooked` 时保存 `hookContext.hookEndsAt`；`resumeAfterReconnect` 重连路径：

```
if hookContext.hookEndsAt exists {
  if hookEndsAt <= now → advanceFromHooked（与 tick 到期共用）
  else → 恢复 hooked，phaseEndsAt = hookEndsAt
}
```

`join_pond` 重连分支调用 `resumeAfterReconnect(io, pondId, user, socket.id)`。

---

### 4.4 不改（本版范围外）

- `DISCONNECT_TIMEOUT_MS` 数值（仍 60s）
- 钓位 SQLite 持久化
- 移动端 `AppState` / 后台保活
- Socket.io 传输层配置
- `leave_spot` 事件（规格有、代码未实现）

---

## 5. 测试计划

### 5.1 手动 / 集成

| 用例 | 步骤 | 预期 |
|------|------|------|
| TC1 重连保位 | 占座钓鱼 → 断网 10s → 恢复 → 等待至首次断线后 90s | 仍在塘，`spotId` 不变，无 `pond_user_left` |
| TC2 超时清场 | 占座钓鱼 → 断网 >65s | `pond_user_left`，钓位可被他人占用 |
| TC3 未钓鱼占座 | seated 占座 → 断网 10s → 恢复 → 等 90s | 仍在塘，`spotId` 保留 |
| TC4 咬钩恢复 | waiting 中断网 10s → 恢复 | 恢复后下一咬钩 tick 可正常 `fish_bite` |
| TC5 双端观察 | A 挂机，B 同塘 | A 断线时 B 见灰色；A 重连后 B 见恢复正常 |

### 5.2 自动化（建议）

在 `scripts/a0-verify.ts` 或新建 `scripts/verify-disconnect-reconnect.ts`：

- Mock / 直连 Socket.io：connect → join → start_fishing → 强制 disconnect socket → reconnect → join → assert user still in snapshot with same `spotId`
- 可选：fake timer 快进 60s，assert 未重连时被移除

### 5.3 回归

- 正常 `leave_pond` 仍释放钓位
- Bot 不受 disconnect 逻辑影响
- `fishing_stop` / 每日时长落库不受影响

---

## 6. 后续任务（不在本版 P0）

| 任务 | 优先级 | 说明 |
|------|--------|------|
| 客户端断线日志 + `AppState` 前台重连 | P1 | `mobile/lib/usePondSocket.ts` |
| 避免导航误触 `leave_pond`（确认弹窗 / 后台挂机模式） | P1 | 产品决策 |
| 钓位 + phase 持久化（SQLite/Redis） | P2 | 防服务端重启 |
| 统一 disconnect 恢复入口，删除 `markUserDisconnected` 重复 | P2 | 代码卫生 |
| `disconnected` 他人可见灰色 UI | P2 | 客户端 `PondCharacter` |

---

## 7. 关键文件索引

| 区域 | 路径 |
|------|------|
| WebSocket 入口 | `server/src/index.ts` |
| 断线定时器 | `server/src/fishingStateMachine.ts` |
| 重连 / 清场 | `server/src/gameState.ts` |
| 咬钩 tick | `server/src/index.ts`（`processWaitingBiteTick` 循环） |
| 指标 | `server/src/fishingMetrics.ts` |
| 客户端 socket | `mobile/lib/usePondSocket.ts` |
| 状态机规格 | `docs/planning/specs/状态机需求描述.md` |

---

## 8. 开发交接

后端实现提示词（可直接 @ 给开发 Agent）：

**[`docs/planning/prompts/bugfix-afk-disconnect-dev.prompt.md`](../prompts/bugfix-afk-disconnect-dev.prompt.md)**

阶段 2–4 排查与归因（弱网矩阵 / leave_pond / Admin 时间线）：

**[`docs/planning/specs/排查-挂机断线诊断阶段2-4.md`](./排查-挂机断线诊断阶段2-4.md)** · **[`docs/planning/prompts/diag-afk-phase2-4-dev.prompt.md`](../prompts/diag-afk-phase2-4-dev.prompt.md)**

---

## 变更记录

| 日期 | 变更 |
|------|------|
| 2026-07-08 | P1 已实现：`hooked` 重连续接 / `resumeAfterReconnect` |
