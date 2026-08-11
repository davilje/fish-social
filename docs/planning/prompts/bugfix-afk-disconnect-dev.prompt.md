<!-- 来源: docs/planning/specs/BUG修复-挂机断线离位.md -->
<!-- 用途: 交给后端开发 Agent，修复挂机断线后误踢 / 离位问题 -->

你是 Fish Social **服务端**开发 Agent。修复挂机场景下「断线重连后仍被踢出鱼塘 / 释放钓位」的 P0 Bug。

## 背景

玩家挂机钓鱼时，短暂断网后客户端会重连并 `join_pond`，服务端 `reconnectSession` 会恢复 `spotId` 与 `fishingPhase`，但 **未取消** `handleDisconnect` 启动的 60 秒 `setTimeout`。定时器到期后仍会 `removeDisconnectedUser`，导致用户被踢、钓位释放。

必读策划 spec（完整需求与验收）：

- `docs/planning/specs/BUG修复-挂机断线离位.md`
- `docs/planning/specs/状态机需求描述.md` §5.10（disconnected 重连语义）

## 根因（已确认，勿重复调研）

```
disconnect → handleDisconnect()  // fishingStateMachine.ts, 启动 disconnectTimers
reconnect  → reconnectSession()  // gameState.ts, 恢复 phase 但未 cancelDisconnectTimer
60s 后     → removeDisconnectedUser()  // 误杀已重连用户
```

已有但未接入的 API：

- `cancelDisconnectTimer(userId)` — `server/src/fishingStateMachine.ts`
- `restoreDisconnectedUser(user)` — 同上，内部会 cancel timer + 恢复 phase

## 产品决策（必守）

1. **60s 内重连成功**：保留钓位与钓鱼状态，**不得**触发 timeout 清场
2. **超过 60s 未重连**：仍按规格释放钓位（`removeDisconnectedUser`），行为不变
3. **断线期间**：不额外扣饵；已扣不退
4. **单一恢复入口**：重连 phase 恢复必须走 `restoreDisconnectedUser`，禁止与 `reconnectSession` 内重复逻辑并存

## 任务 1 — P0 修复重连定时器（必须）

**目标文件**：`server/src/gameState.ts`、`server/src/index.ts`（二选一调用点，勿重复）

在重连成功路径调用 `restoreDisconnectedUser(user)`：

- 推荐在 `reconnectSession` 末尾：`restoreDisconnectedUser(user)` + `updatePondUser(pondId, user)`
- 删除 `reconnectSession` 内与 `restoreDisconnectedUser` **重复**的 `fishingPhase` 手写恢复代码

验证：`restoreDisconnectedUser` 会 `cancelDisconnectTimer(user.id)` 并清空 `disconnectedAt`。

**`index.ts` `join_pond` 重连分支**：保持现有 `pond_user_updated` 广播；确认不会在 `reconnectSession` 前后重复恢复。

## 任务 2 — P0 可观测性（必须）

在 `server/src/fishingStateMachine.ts`：

| 时机 | 日志前缀 | 字段 |
|------|----------|------|
| `handleDisconnect` | `[disconnect]` | userId, playerId, pondId, spotId, ts |
| `restoreDisconnectedUser` | `[disconnect-timer-cancelled]` | userId, ts |
| timeout 回调（`removeDisconnectedUser` 前） | `[disconnect-timeout-remove]` | userId, spotId, ts |

扩展 `server/src/fishingMetrics.ts`：

- 新增 metric 事件：`disconnect`、`reconnect`、（可选）`disconnect_timeout`
- 在对应路径调用 `recordFishingMetric`

## 任务 3 — P1 hooked 重连恢复（本版可选，时间允许则做）

规格：断线前 `hooked`，重连时若 `phaseEndsAt <= now`，应立即进 `resolving`。

- 在 `restoreDisconnectedUser` 或抽 `resumeHookedAfterReconnect(io, pondId, user)` 实现
- **复用** `tickFishingPhases` 中 hooked 到期的转移逻辑，不要复制一份 resolving 流程

若无时间，在 spec 中注明「P1 未做」并在 PR 描述列出。

## 任务 4 — 代码卫生（可选）

- `index.ts` `disconnect` 处理器：`handleDisconnect` 已更新用户状态后，`markUserDisconnected` 可能重复；评估是否删除冗余调用或合并到 `handleDisconnect`
- 确保 `disconnectTimers` 在 `removeDisconnectedUser` 后无泄漏（map 条目已 delete）

## 任务 5 — 测试（必须至少一项）

**优先**：新增 `scripts/verify-disconnect-reconnect.ts`（或扩展现有 verify）：

1. Socket 连接 → `join_pond` → `start_fishing`
2. 断开 socket（不 leave_pond）
3. 新 socket 重连 → `join_pond`（同 playerId）
4. Assert：snapshot 中用户仍在、`spotId` 不变
5. 快进 / 等待 60s+：用户 **不应** 被 `pond_user_left`

**手动验收**（PR 中写明已测）：

- TC1：断网 10s 恢复，再等 90s → 仍在塘
- TC2：断网 >65s → 被清出

## 不改（范围外）

- `DISCONNECT_TIMEOUT_MS`（仍 60_000）
- `mobile/lib/usePondSocket.ts`（客户端另任务）
- 钓位 SQLite 持久化
- Socket.io ping 配置
- Bot 逻辑

## 完成后

1. 更新 `docs/planning/specs/BUG修复-挂机断线离位.md` 状态 → **已实现**
2. `docs/planning/specs/README.md` 索引状态同步
3. `docs/planning/CHANGELOG.md` 追加一行
4. Commit message 建议：

```
fix(server): cancel disconnect timer on pond reconnect

Prevent removeDisconnectedUser from firing after a successful
join_pond reconnect within the 60s grace window.
```

## 自检清单

- [ ] 重连后 `disconnectTimers` 无该 userId 条目
- [ ] 60s 内重连：无 `pond_user_left`
- [ ] >60s 未重连：仍有 `pond_user_left`
- [ ] `restoreDisconnectedUser` 为唯一 phase 恢复逻辑
- [ ] 新增 disconnect/reconnect 日志或 metrics
- [ ] 至少一个自动化或文档化手动测试通过
