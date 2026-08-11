# 会话计时 tick 仅必要字段

| 项 | 内容 |
|----|------|
| 状态 | **已实现** |
| 优先级 | P0 |
| 编号 | PERF-03b |
| 设计时间 | 2026-07-13 |
| 完成时间 | 2026-07-13 |
| 关联 | PERF-03 · BUG-07 · `session_timer_tick` · BE-OPT-B |

## 背景

`session_timer_tick` 每秒对每个在钓用户广播一次，用于 UI 秒表 `sessionFishingMs`。  
当前 payload 还带 `fishingPhase`、`fishingStartedAt`，在 waiting/hooked 可持续数十秒不变，**重复度极高**；千人时放大带宽与 socket_tap 噪音。

相位变化本就有权威通道：`pond_user_updated`（及 bite/miss 等）。

## 方案（分阶）

### A. 瘦字段（本需求 · 已做）

| 保留 | 删除 |
|------|------|
| `userId` | `fishingPhase` |
| `sessionFishingMs` | `fishingStartedAt` |

契约：

```ts
interface SessionTimerTickPayload {
  userId: string;
  sessionFishingMs: number;
}
```

- 服务端 `serverLoops.ts` 只 emit 上述两字段  
- 客户端 `usePondSocket` 只合并 `sessionFishingMs`  
- 相位 / 开钓时刻仍以 `pond_user_updated` 为准  
- 本地插值（BUG-08）继续可在 tick 间隙走动

### B. 按塘批打包（下一刀 · 未做）

现状：N 个在钓用户 → N 次 `io.to(pond).emit`。  
改为每塘每秒 **1 次**：

```ts
{ ticks: Array<{ userId: string; sessionFishingMs: number }> }
```

收益：房间内 socket 处理次数从 O(N) 降到 O(1) 次事件（payload 仍 O(N)）。  
验收：改 shared 事件形状 + 客户端一次 map 合并 + `verify:session-timer-broadcast`。

### C. 降频（可选）

间隔 1s → 2s，更依赖客户端插值；弱网校准仍靠 tick。需手测 Modal 秒表手感。

### D. 不做

- 砍掉 `session_timer_tick` 只靠纯本地计时（重连/时钟漂易漂）  
- 把秒表重新塞回全量 `pond_user_updated`（PERF-03 / BUG-07 回归）

## 验收

- [x] `SessionTimerTickPayload` 仅 `userId` + `sessionFishingMs`
- [x] `npm run verify:session-timer-broadcast` 绿
- [x] 服务端 emit 不含 phase / startedAt

## 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-13 | 定稿并实现 A；B/C 留作后续 |
