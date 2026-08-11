# 开发提示词：断线 / 离塘未结算丢失钓鱼时长（BUG-16）

你是 Fish Social **后端** Agent（`.cursor/rules/backend-dev-agent.mdc`，仅改 `server/` `shared/` `scripts/`）。按规格修复「钓了很久却不记时长」与「一次写满 8h」两极异常。

## 必读

1. `docs/planning/specs/BUG修复-断线离塘未结算丢失钓鱼时长.md`（**已实现** / **BUG-16**）
2. `docs/planning/specs/BUG修复-今日额度跨日不刷新.md`（BUG-15，已实现：`safeFishingElapsedMs` / 30s `syncHumanQuotaAndEmit`）
3. `server/src/fishingStateMachine.ts`：`syncStatus` · `handleDisconnect` · `resumeAfterReconnect` · `advanceFromStopping`
4. `server/src/pondUserManager.ts`：`flushFishingSessionToToday` · `safeFishingElapsedMs` · `removeDisconnectedUser`
5. 勿回归：BUG-15 闲置对齐 DB / 跨日恢复、BUG-14 底栏插值、BUG-13 头顶秒表

## 顺序

1. **统一结算出口**：抽 `settleFishingSession(user, atMs, reason)`（sanitize → `safeFishingElapsedMs` → `addTodayFishingMs` → 锚点处理，幂等），stop / leave / disconnect / 相位收尾全部改调它
2. **断线前结算**：`handleDisconnect` 在 `transitionPhase(...,'disconnected')` **之前**结算——注意 `syncStatus` 会清 `fishingStartedAt`，晚了就丢
3. **兜底不重复**：`removeDisconnectedUser` 保留防御式结算，但结算后锚点必须为空，避免二次计入
4. **分段落账**：复用 30s 配额扫描，把在钓用户 `now − 锚点` 的增量落库并前移锚点，使单段远小于 8h（同时消除「>8h 记 0」）
5. **重连锚点**：重连恢复相位时锚点取 `now`，断线期不计
6. **结构化日志**：结算写 reason / creditedMs / phase，便于「时长去哪了」排查
7. **verify**：扩展 `scripts/verify-fish-daily-shanghai-rollover.ts`，断言断线结算、宽限期不重复计、长会话分段累计；跑 `npm run verify:fish-daily-shanghai`

## 非目标

改 8h 常量、改客户端（BUG-17 单独做）、断线期继续计时。

## 注意（现场事实）

- server 启动脚本是 `tsx src/index.ts`（**无 watch**），改完必须重启进程才生效
- 若新增 / 变更埋点事件，另跑 Skill `metrics-catalog-sync`

## 验收

对照 spec §4；完成后按 Skill `planning-progress-sync` Checklist B → **已实现** + `npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/bugfix-session-settlement-dev.prompt.md 按此实现 BUG-16
```

建议角色：`@backend-dev`
