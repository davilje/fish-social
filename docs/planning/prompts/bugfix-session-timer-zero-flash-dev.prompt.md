<!-- 来源: docs/planning/specs/BUG修复-垂钓中头顶0秒闪烁.md（BUG-13） -->

你是 Fish Social **全栈开发 Agent**。修复 **BUG-13：垂钓中头顶反复显示「0秒」**。

## 必读

1. [`docs/planning/specs/BUG修复-垂钓中头顶0秒闪烁.md`](../specs/BUG修复-垂钓中头顶0秒闪烁.md)
2. 对照：BUG-07、PERF-03b（**禁止**恢复每秒全量 `pond_user_updated` 刷秒表）

## 必须做

### 服务端

1. `bots.ts` 所有 `pond_user_updated` 改为 `enrichPondUser(...)`（开钓/停钓等）  
2. 建议抽 `emitPondUserUpdated(io, pondId, user)` 统一出口  
3. enrich / session tick 前对在钓相位 `ensureFishingStartedAt`

### 客户端

1. `usePondSocket`：`pond_user_updated` **合并**，在钓时勿用缺失的 `sessionFishingMs`/`fishingStartedAt` 冲掉本地值  
2. 插值条件改为 `isFishingActive(phase) && fishingStartedAt != null`（与 badge 对齐）

### 验证

- 扩展 `verify:session-timer-broadcast`（或新脚本）：bots 源码守卫 + 客户端非整替换守卫 + enrich 递增断言  
- 手测真人/Bot waiting 10s

## 不做

- 回退 PERF-03b 字段瘦身  
- 改 todayFishingMs / 玩法

## 完成后

- [x] 勾选 spec §4  
- [x] CHANGELOG + BUG-13 → 已实现 + `npm run planning:master-xlsx`

