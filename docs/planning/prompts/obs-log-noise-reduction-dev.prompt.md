# 开发提示词：服务端日志降噪（OBS-LOG-1）

你是 Fish Social **后端开发 Agent**。按策划规格实现日志分层降噪。

## 必读

1. `docs/planning/specs/服务端日志降噪与分层输出.md`（**已确认** / **OBS-LOG-1**）
2. 锚点：`serverLoops.ts` · `pondEcology.ts` · `socketEventTap.ts` · `fishingStateMachine.ts`（reconnect 埋点）· `fishingMetrics.ts`（validation）

## 实现清单（方案 A）

1. **fanout**：生态 tick 路径不再每轮 info 打 `socket_broadcast_fanout`；指标保留；`FANOUT_LOG_INFO=1` 可恢复。  
2. **perf info**：Histogram 保留；info 仅 `durationMs >= PERF_LOG_SLOW_MS`（默认 50）或 `PERF_LOG_INFO=1` 全开抽样。  
3. **ecology console**：`[fish|pond supplement]` / `[pond migration]` → `ECOLOGY_VERBOSE=1` 才输出。  
4. **socket_tap**：默认忽略 `session_timer_tick`、`pond_ecology_updated`（可用 env 覆盖）。  
5. **reconnect 校验**：`validateMetricPayload` 合并顶层 `playerId`/`pondId`，消除假告警。

## 验收

- 单人重连：业务日志在，fanout/补鱼/tap tick 默认不刷  
- 相关 verify 绿  
- 完成后：spec → **已实现**；`planning:master-xlsx`；若改埋点表说明则 `metrics-catalog-sync`

## 派发

```text
@docs/planning/prompts/obs-log-noise-reduction-dev.prompt.md 按此实现 OBS-LOG-1
```
