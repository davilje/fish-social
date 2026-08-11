# 开发提示词：每日额度快照与收杆结算（BUG-18）

你是 Fish Social **前端 + 后端** Agent。完整修复每日钓鱼额度：每次进入鱼塘显示当前上海日的真实剩余时间；收杆时记录本局已用时长；只有上海时间每天 00:00 刷新额度；禁止旧状态、错误快照或 demo 数据覆盖真实额度。

## 必读

1. `docs/planning/specs/BUG修复-进塘首帧状态与演示降级.md`（**已实现** / **BUG-18**）
2. mobile/lib/usePondSocket.ts
3. mobile/app/pond/[id].tsx
4. server/src/socketPondHandlers.ts、server/src/pondUserManager.ts
5. 回归 BUG-14、BUG-15、BUG-17、FISH-DAILY-1

## 实现顺序

1. **统一额度口径**：`daily_fishing(user_id, date_key, ms)` 是唯一持久化来源；`date_key` 必须是 Asia/Shanghai 日期。不要单独持久化“剩余时间”。
2. **进塘快照**：`buildSnapshot/enrichPondUser` 必须按当前 playerId + 当前上海日读取 DB；当前玩家首帧不得使用旧内存额度。
3. **修复永久加载**：真实模式的 `pond_snapshot` 处理器在成功应用当前快照后调用 `setSnapshotReady(true)`；必须校验 `isCurrent()` 和 `snapshot.pond.id`。
4. 连接新鱼塘/重连时清空旧 users、myUserId、messages、ecology 和 demo 状态；旧 Socket 事件不能更新当前鱼塘。
5. `pond_snapshot` 到达前显示加载态并禁用额度操作，但不能显示「今日已满」。
6. **收杆立即结算**：`stop_fishing` 触发幂等 `settleFishingSession(finalize)`，把 `fishingStartedAt` 到当前的 elapsed 写入当日 `daily_fishing`，再清空锚点。`stopping` 动画可以保留，但不能依赖延迟定时器作为唯一结算点。
7. **返回最终额度**：`stop_fishing` ack 至少返回 `todayFishingMs`、`todayRemainingMs`、`quotaDateKey`；同时广播最终 `pond_user_updated`。客户端优先应用 ack，不得以本地 0 覆盖服务端已用时长。
8. 状态机延迟收尾、重复 stop、断线兜底必须幂等；分段落账后只结算剩余段，禁止重复计时。
9. 删除真实运行下的静默 10 秒 `DEMO_USERS` 降级；演示模式仅由 `EXPO_PUBLIC_DEMO_MODE=1` 显式开启。
10. 添加测试：当日已用 2 小时重进显示约 6 小时；收杆后累计增加；重复结算不增加；上海 00:00 后使用新日期；快照延迟/旧事件不覆盖。

## 非目标

- 不改 8 小时额度规则。
- 不重做 BUG-14 的本地插值。
- 不实现 FISH-SPOT-1 手动选点。

## 验收

对照 spec §5；完成后按 planning-progress-sync Checklist B 回写 BUG-18，并运行 `npm run planning:master-xlsx`。

必须验证：

- 当日已有 2 小时记录，进入鱼塘显示约 6 小时。
- 本局钓鱼 30 秒后收杆，今日已用增加约 30 秒，剩余减少约 30 秒。
- 收杆后重新进入鱼塘仍保持该剩余值。
- 只有上海 00:00 后才切换到新日期额度。
- 不再永久显示「加载中」，也不再错误显示「今日已满」。

## 派发

```text
@docs/planning/prompts/bugfix-pond-entry-stale-state-dev.prompt.md 按此实现 BUG-18
```

建议角色：@frontend-dev（必要时 @backend-dev）
