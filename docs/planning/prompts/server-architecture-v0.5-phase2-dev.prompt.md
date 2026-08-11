<!-- 来源: docs/planning/specs/服务器架构优化路线图-v0.5.md -->
<!-- 用途: v0.5 第二期 P1 — 入口拆分 / SessionRegistry / 优雅停机 / correlationId -->

你是 Fish Social **后端开发 Agent**。实现 **v0.5 第二期（P1 可维护性）**。

## 前置

- **第一期 P0 必须已完成**（鉴权、timerRegistry、咬钩收敛、checkpoint）
- 必读：`docs/planning/specs/服务器架构优化路线图-v0.5.md` §3.2
- xlsx sheet：`05-R1-1` ~ `09-R1-5`

---

## 任务 E — R1-1 拆分 index.ts

新建模块（从 `index.ts` 迁出，**行为不变**）：

| 文件 | 职责 |
|------|------|
| `server/src/createApp.ts` | Express、cors、static、/health、/api/world、mount routes |
| `server/src/socketLifecycle.ts` | connection、disconnect、register_player、connection_error |
| `server/src/socketPondHandlers.ts` | join/leave/start/stop/chat/accept_catch |
| `server/src/serverLoops.ts` | phase tick、bite loop、ecology、perf；导出 `startLoops(io, deps)` 与 `stopLoops()` |

`index.ts` 仅：load env、createApp、createServer、io、register handlers、startLoops、listen、shutdown。

**目标**：`index.ts` < 150 行。

---

## 任务 F — R1-2 + R1-3 SessionRegistry

新建 `server/src/sessionRegistry.ts`：

```typescript
bindPlayer(playerId, socketId): void  // 覆盖时打 session_rebound
bindPondUser(userId, socketId, pondId): void
unbindSocket(socketId): void
resolveBySocket(socketId)
resolveByUser(userId)
resolveSocketByPlayer(playerId)
```

- 替代直接写 `playerSockets`、`socketByUserId`；`gameState.sessions` 写入也经 registry 或与之同步
- **不再 export** 可变 `playerSockets` Map；`socialRoutes` / `bots` / `shop` 改用 registry
- `gameState` 内部 Map 不 export（渐进：先标记 @internal，再私有化）

---

## 任务 G — R1-4 优雅停机

扩展 `shutdown()`：

1. `stopLoops()` — clear 所有 global interval
2. `timerRegistry.cancelAll()`
3. 若 checkpoint 已实现：对活跃 session best-effort flush
4. `io.close` → `httpServer.close` → `db.close()`（在 `db.ts` 暴露 `closeDb()`）
5. `SHUTDOWN_TIMEOUT_MS` 可配置，默认 8000

日志分阶段：`[shutdown] phase=stop_loops` 等。

---

## 任务 H — R1-5 correlationId

- `socketLifecycle`：`connection` 时 `socket.data.correlationId = randomUUID()`
- `fishingObservability.logStructuredEvent`：若传入 `socketId` 或 AsyncLocalStorage，自动附加 `correlationId`
- HTTP 写接口：读 `X-Request-Id` 或生成
- 可选：Admin timeline query `?correlationId=`

---

## 验收

- 全套 `npm run verify:*` 通过
- `index.ts` 行数 < 150
- 无文件 `import { playerSockets }` 直接写 Map
- SIGTERM 日志含 shutdown 阶段

---

## 完成后

更新 spec 第二期状态、xlsx R1-* 行、CHANGELOG。

## commit 建议

```text
refactor(server): v0.5 phase2 split index, session registry, graceful shutdown

Extract socket/loop modules, centralize session bindings, add correlationId
and structured graceful shutdown sequence.
```
