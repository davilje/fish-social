# BUG 修复：会话计时广播回归（waiting 阶段计时卡死）

| 状态 | **已实现** | 目标版本 v0.5.1-patch |
|------|------------|------------------------|
| 优先级 | P0 | 钓鱼时长显示阻断 |
| 范围 | **服务端** `serverLoops.ts` 1s 会话广播 |
| 前置 | v0.5 第三期 R2-1 增量广播（`consumeDirtyUsers`） |
| 关联 | [`BUG修复-鱼塘钓鱼时长显示.md`](./BUG修复-鱼塘钓鱼时长显示.md) · [`BUG修复-切页误离塘与计时中断.md`](./BUG修复-切页误离塘与计时中断.md) |

---

## 1. 问题

玩家进入 `waiting`（及 `baiting`/`casting` 等）后，头顶 **sessionFishingMs 不再递增**，表现为计时卡死。

服务端日志显示：phase 正常、`ecology_tick` 与 `fanoutCount` 正常，但 **无持续 `pond_user_updated`（会话计时）**。

---

## 2. 根因

v0.5 第三期将 1s 会话计时循环改为 **仅广播 dirty 用户**：

```typescript
// server/src/serverLoops.ts（当前错误）
const dirtyUserIds = consumeDirtyUsers(pond.id);
if (dirtyUserIds.length === 0) continue;
```

`markUserDirty` 仅在 phase 变更、占座、join/leave 时触发。进入 `waiting` 后无新 dirty → **不再每秒推送 `sessionFishingMs`**。

客户端 `PondCharacter` 对非 `hooked` 阶段 **无本地 tick**，完全依赖服务端 `pond_user_updated`。

---

## 3. 修复方案（方案 A）

**会话计时广播与 dirty 增量解耦**：

1. 1s `sessionTimer`：**遍历塘内所有处于 `SESSION_TIMER_PHASES` 的用户**，`enrichPondUser` 后 `emit('pond_user_updated')`
2. **不要**对该循环使用 `consumeDirtyUsers`
3. dirty 机制继续用于：phase 变更触发的即时推送、`socketPondHandlers` 等（保持不变）
4. **禁止**为该循环恢复全量 `buildSnapshot`（保持 R2-1 性能收益）

参考实现：

```typescript
const sessionTimer = setInterval(() => {
  for (const pond of PONDS) {
    const users = listUsersInPond(pond.id)
      .map(enrichPondUser)
      .filter((u) => u.fishingPhase && SESSION_TIMER_PHASES.includes(u.fishingPhase));
    for (const user of users) {
      io.to(pond.id).emit('pond_user_updated', user);
    }
  }
}, 1000);
```

若 `listUsersInPond` 返回原始对象，需 `map(enrichPondUser)` 保证 `sessionFishingMs` 实时计算。

---

## 4. 验收

| 用例 | 预期 |
|------|------|
| 进入 waiting 后停留 10s | 客户端头顶计时时长持续增加 |
| 服务端日志 | 约每秒有 `pond_user_updated`（可无结构化前缀，属正常 socket 事件） |
| `sessionFishingMs` 字段 | 每次广播值递增 |
| 性能 | 仍无 1s 全量 `buildSnapshot`；`snapshotCalls` 保持 0 |

**脚本**：新增或扩展 `scripts/verify-session-timer-broadcast.ts`：

- 模拟/注入塘内 fishing 用户，或直接 API + 内存态断言
- 间隔 2s 两次 `enrichPondUser`，断言 `sessionFishingMs` 第二次 > 第一次

---

## 5. 开发交接

[`docs/planning/prompts/bugfix-session-timer-broadcast-dev.prompt.md`](../prompts/bugfix-session-timer-broadcast-dev.prompt.md)

---

## 6. 变更记录

| 日期 | 变更 |
|------|------|
| 2026-07-09 | **已实现**：sessionTimer 恢复每秒全塘 SESSION_TIMER_PHASES 广播；新增 verify:session-timer-broadcast |
| 2026-07-09 | 初稿：R2-1 dirty 误伤会话计时广播 |
