<!-- 来源: docs/planning/specs/BUG修复-会话计时广播回归.md -->
<!-- 用途: 服务端 — 修复 waiting 阶段 sessionFishingMs 不再递增 -->

你是 Fish Social **后端开发 Agent**。修复「钓鱼会话计时在 waiting 阶段卡死」。

## 必读

1. `docs/planning/specs/BUG修复-会话计时广播回归.md`
2. `server/src/serverLoops.ts`（1s `sessionTimer`）
3. `server/src/gameState.ts`（`enrichPondUser` · `computeSessionFishingMs` · `listUsersInPond`）

## 背景

v0.5 第三期 R2-1 将 1s 广播改为 `consumeDirtyUsers` 增量推送。  
phase 进入 `waiting` 后不再 dirty → 客户端收不到更新的 `sessionFishingMs` → 头顶计时冻结。

**方案 A**：会话计时广播 **不走 dirty**；每秒对塘内所有 `SESSION_TIMER_PHASES` 用户 `enrichPondUser` + `pond_user_updated`。

---

## 任务 1（P0）修复 `sessionTimer` 循环

文件：`server/src/serverLoops.ts`

**删除**（错误逻辑）：

```typescript
const dirtyUserIds = consumeDirtyUsers(pond.id);
if (dirtyUserIds.length === 0) continue;
const users = getEnrichedUsersByIds(pond.id, dirtyUserIds);
```

**改为**（示意，按项目现有 API 微调）：

```typescript
import { enrichPondUser, listUsersInPond } from './gameState.js';

const sessionTimer = setInterval(() => {
  for (const pond of PONDS) {
    const fishingUsers = listUsersInPond(pond.id)
      .map(enrichPondUser)
      .filter(
        (u) => u.fishingPhase && SESSION_TIMER_PHASES.includes(u.fishingPhase),
      );
    for (const user of fishingUsers) {
      io.to(pond.id).emit('pond_user_updated', user);
    }
  }
}, 1000);
```

要点：

- `enrichPondUser` 必须调用，以实时计算 `sessionFishingMs = now - fishingStartedAt`
- **不要**在此循环调用 `buildSnapshot`（保持 R2-1 性能）
- `consumeDirtyUsers` 仍可用于其它「仅状态变更才推送」的路径，勿删 `markUserDirty` 体系

---

## 任务 2（P0）验收脚本

新增 `scripts/verify-session-timer-broadcast.ts` + `package.json`：

```json
"verify:session-timer-broadcast": "npm run build:shared && npx tsx scripts/verify-session-timer-broadcast.ts"
```

脚本最小断言：

1. 构造或复用塘内 `status=fishing`、`fishingStartedAt` 已设、`fishingPhase=waiting` 的用户
2. 调用 `enrichPondUser` 两次，中间 `sleep(1100)`
3. 第二次 `sessionFishingMs` > 第一次
4. （可选）启服后手测说明：waiting 10s 计时递增

---

## 任务 3 回归

```bash
npm run verify:server-observability
npm run verify:disconnect-reconnect
npm run verify:session-checkpoint
npm run verify:session-timer-broadcast
```

确认 R2-1 其它优化未回退：`bite_check_loop` 仍用 `getWaitingUserIds`，生态 tick 仍可不 build 全量 snapshot。

---

## 不改

- 不改客户端 `PondCharacter`（本期仅服务端）
- 不改 dirty / waiting 索引在咬钩循环中的用法
- 不改钓鱼公式与状态机

---

## 完成后

1. 更新 `BUG修复-会话计时广播回归.md` 状态 → **已实现**
2. 补 `CHANGELOG.md`
3. 回复：改动文件 + verify 输出摘要

## commit 建议

```text
fix(server): restore per-second session timer broadcast for fishing phases

Broadcast enriched pond_user_updated for all SESSION_TIMER_PHASES users
every second instead of dirty-only updates, fixing frozen sessionFishingMs
in waiting phase.
```
