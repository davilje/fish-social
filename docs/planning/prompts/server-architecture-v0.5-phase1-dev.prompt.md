<!-- 来源: docs/planning/specs/服务器架构优化路线图-v0.5.md -->
<!-- 用途: v0.5 第一期 P0 — 鉴权 / checkpoint / timerRegistry / 咬钩收敛 -->

你是 Fish Social **后端开发 Agent**。实现 **v0.5 第一期（P0 正确性加固）**。

## 必读

1. `docs/planning/specs/服务器架构优化路线图-v0.5.md` §3.1、§4
2. `docs/planning/reports/服务器架构问题与修复方案-v0.5.xlsx` sheet：`01`~`04`（R0-1 ~ R0-4）
3. 现有 verify：`verify-disconnect-reconnect`、`verify-server-observability`

## 推荐实施顺序

`R0-1 鉴权` → `R0-3 timerRegistry` → `R0-4 咬钩收敛` → `R0-2 checkpoint`  
（R0-2 与 R0-3 可部分并行，但 reconnect 恢复应使用 registry）

---

## 任务 A — R0-1 鉴权 JWT

### A1 模块

新建 `server/src/auth.ts`：

- `signPlayerToken(playerId: string): string`
- `verifyPlayerToken(token: string): { playerId: string } | null`
- 环境变量：`JWT_SECRET`（必填，除 development + `AUTH_DISABLED=1`）
- `AUTH_DISABLED` 仅 `NODE_ENV=development` 时生效

### A2 Socket

在 `index.ts` 创建 `io` 后：

```typescript
io.use((socket, next) => {
  if (process.env.AUTH_DISABLED === '1' && process.env.NODE_ENV === 'development') return next();
  const token = socket.handshake.auth?.token;
  const payload = verifyPlayerToken(token);
  if (!payload) {
    logStructuredEvent('auth', 'auth_failed', { reason: 'invalid_socket_token', socketId: socket.id });
    return next(new Error('unauthorized'));
  }
  socket.data.authPlayerId = payload.playerId;
  next();
});
```

- `register_player`：若已有 `authPlayerId`，校验与 body.playerId 一致，否则以 auth 为准
- `join_pond` 等写操作：使用 `socket.data.authPlayerId`，**禁止仅信任 payload.playerId**

### A3 HTTP

新建 `requireAuth` 中间件；写接口（POST/PUT 改状态）挂载。读接口可暂保持原样或同样要求 token。

可选：`POST /api/auth/dev-token` 仅 development，body `{ playerId }` 返回 token（便于 verify）。

### A4 验收

新建 `scripts/verify-auth.ts` + `npm run verify:auth`：

- 有效 token 可 connect
- 无 token / 伪造 playerId 被拒
- `identity_mismatch` 日志（若 body.playerId ≠ token）

---

## 任务 B — R0-3 定时器注册表

新建 `server/src/timerRegistry.ts`（API 见 spec §4.3）。

### 迁移点

1. `fishingStateMachine.ts`：`disconnectTimers` → registry `kind: disconnect_grace`
2. `inventory.ts`：pending timeout → `kind: pending_expire`
3. `fishingSession.ts`：若仍有 hook timer，迁到 registry 或任务 C 直接删除

### 统一取消

在以下路径调用 `cancelByUser` / `cancelBySocket`：

- `handleDisconnect`、`restoreDisconnectedUser`
- `leavePond`、`clearSession`
- `index.ts` socket `disconnect` handler

### 验收

- `verify:disconnect-reconnect` 全绿
- grep 确认无裸 `setTimeout` 用于用户态逻辑（registry 内部除外）

---

## 任务 C — R0-4 咬钩收敛

1. 保留 `fishingSession.ts` 纯函数：选鱼、概率、roll
2. **删除** `hookStateByUser` 与独立 hook `setTimeout`；`hooked` 超时由 `tickFishingPhases` 根据 `phaseEndsAt` 处理
3. `bots.ts`：`processBotBiteTick` 改为调用与真人相同的 `processWaitingBiteTick`（或共享 `runWaitingBiteForUser`）
4. 删除 `index.ts` 中分散的 `cancelHookResolution`（仅保留统一 leave/disconnect 路径若仍需要清 legacy 状态）

### 验收

- `verify:server-observability` 全绿
- bot 与真人咬钩/脱钩手测无回归

---

## 任务 D — R0-2 在线态 checkpoint

### D1 Migration

`server/src/migrations/player_pond_session.ts`：

- 表 `player_pond_session`（字段见 spec §4.2）
- 表 `pending_catch_locks`（user_id PK, catch 字段, locked_at）

### D2 写路径

- `transitionPhase` / 占座 / `handleDisconnect` → upsert session
- `lockPendingCatch` → 写 pending 表 + upsert session
- `leavePond` / `acceptCatch` / expired / timeout 清场 → delete

### D3 恢复路径

`join_pond` / reconnect：

1. 查 `player_pond_session` by (playerId, pondId)
2. 若存在且未过期：复用 `userId`、恢复 spotId/phase；调用 `resumeAfterReconnect` 链
3. 否则走现有新建 user 流程

### D4 验收

新建 `scripts/verify-session-checkpoint.ts` + `npm run verify:session-checkpoint`：

- 占座 waiting → kill 进程模拟（或直接调恢复 API）→ reconnect 恢复 spotId
- pending lock 写入 DB；重启后过期仍可触发 expired

---

## 不改

- 钓鱼概率公式、生态算法
- 多实例 / Redis
- mobile 客户端（一期可文档说明后续需带 token）

---

## 完成后

1. 更新 `服务器架构优化路线图-v0.5.md` 第一期状态 → **已实现**
2. 更新 xlsx 总览 R0-1~R0-4 → **已实现**
3. 补 `CHANGELOG.md`
4. 回复：改动文件列表 + 全部 verify 输出摘要

## commit 建议

```text
feat(server): v0.5 phase1 auth, timer registry, bite unification, session checkpoint

Add JWT auth for socket/HTTP writes, centralize per-user timers,
converge bot/human bite paths, and persist pond session checkpoints.
```
