# BUG 修复：进塘与钓鱼剩余展示回归

| 项 | 内容 |
|---|---|
| 功能名称 | BUG-19 后「今日剩余」不走动 / 未选钓点误显满额 8h |
| 状态 | **已实现** |
| 编号 | **BUG-20** |
| 设计时间 | **2026-08-10** |
| 完成时间 | **2026-08-10** |
| 优先级 | P0 |
| 目标版本 | hotfix |
| 关联 | BUG-19 · BUG-14 · BUG-18 · FISH-SPOT-1 |
| 关联代码 | `mobile/lib/fishingDuration.ts` · `mobile/lib/usePondSocket.ts` · `mobile/app/pond/[id].tsx` · `server/src/socketPondHandlers.ts` · `shared/types.ts` |

## 1. 背景与目标

### 1.1 背景

**BUG-19** 落地单一口径（`todayFishingBaseMs` / `sessionStartedAt` / checkpoint）后，现场仍复现两类展示回归：

1. **开钓后「今日剩余」不随墙钟下降**（像卡住）：客户端曾用 `todayRemainingMs - (localSession - serverSession)`；`session_timer_tick` 把 `sessionFishingMs` 对齐后差值为 0，剩余冻结。
2. **进入鱼塘、尚未选择钓点时，「今日剩余」仍显示满额 8 小时**：join 早期 `me` 缺少 base/remaining（ack 与 snapshot 竞态；`pond_user_updated` 在列表为空时被丢掉），闲置路径按已用=0 计算。

本单是 BUG-19 契约上的**展示与进塘时序补丁**，不改 DB 账本与 checkpoint 语义。

### 1.2 目标

- 钓鱼中：剩余 = `MAX - (开钓冻结 base + 本局墙钟 elapsed)`，每秒可感下降；checkpoint 抬高 DB base 时本地冻结基线不得上调（防双计）。
- 未选钓点（`spotId == null` / `fishingPhase: idle`）：进塘后应立即反映当日已用，不得误显满额 8h。
- `join_pond` ack 带回 `todayFishingBaseMs` / `todayRemainingMs` / `quotaDateKey`；客户端种子与 snapshot / joined / updated 合并。

### 1.3 非目标

- 不改每日 8h / 上海 00:00 换日（FISH-DAILY-1）。
- 不重做 BUG-19 结算出口与 checkpoint 不前移锚点。
- 不重做钓点选择交互（FISH-SPOT-1）。
- 不做断线离线挂机继续计时。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 玩家 | 当日已钓一段时间，进塘未点钓点 | 底栏「今日剩余」&lt; 8h，与 DB 一致（允许加载中，禁止满额误导） |
| 玩家 | 开钓后连续观察 ≥ 5s | 剩余逐秒（或 250ms 插值）下降，不被 session tick 卡死 |
| 玩家 | 开钓跨过 30s checkpoint | 剩余继续单调不增，不因 base 上涨而「跳回」或双计加速 |
| 玩家 | rejoin / 重连后未选点 | 与首次进塘同口径，额度种子仍生效 |

---

## 3. 功能范围

### 3.1 钓鱼中剩余插值（P0）

- 开钓时冻结 `todayFishingBaseMs`（或等价基线）到本地；展示已用 = 冻结基线 + `(now - sessionStartedAt)`。
- **禁止**用 `localSessionFishingMs - serverSessionFishingMs` 做差扣减 remaining（会被 tick 对消）。
- checkpoint 后服务端 base 上涨：本地基线只允许因日切下降，禁止随 enrich 上调。

### 3.2 进塘额度种子（P0）

| 环节 | 要求 |
|------|------|
| `join_pond` ack | 返回 `todayFishingBaseMs`、`todayRemainingMs`、`quotaDateKey` |
| 服务端 fresh join | enrich 后对本人推送 `pond_user_updated`（未选点也能带额度） |
| 客户端 | ack 写入 join quota seed；快照未到时可种本人 stub；snapshot / joined / updated 与种子合并（取同日较大已用） |
| `pond_user_updated` | 本人不在列表时 **upsert**，不得静默丢弃 |
| 底栏就绪 | ack 已带回额度字段即可显示，不必死等完整 snapshot（仍禁止「今日已满」误伤） |

### 3.3 闲置展示公式（P0）

未在钓时，`effectiveTodayUsedMs` 取下列可用信号的 **最大值**：

- `todayFishingBaseMs`
- `todayFishingMs`
- `MAX - todayRemainingMs`（若有 remaining）

避免某一字段为 0 / 缺失时误显满额。

---

## 4. 技术影响

### 4.1 API / Socket

| 类型 | 名称 | 说明 |
|------|------|------|
| Socket ack | `join_pond` | 增加/确认 base、remaining、quotaDateKey |
| Socket 出 | `pond_user_updated` | fresh join 对本 socket 推送 enrich 后用户 |
| Socket 出 | `pond_snapshot` | 用户列表带齐 base/remaining（既有） |

### 4.2 涉及文件

- `server/src/socketPondHandlers.ts`
- `mobile/lib/fishingDuration.ts` · `usePondSocket.ts` · `mobile/app/pond/[id].tsx`
- `shared/types.ts`（join ack 字段）
- `scripts/verify-bug14-daily-remaining.ts` · `scripts/verify-bug19-quota-contract.ts`

---

## 5. 验收标准

- [x] 未选钓点：当日已用 &gt; 0 时，进塘后剩余 ≠ 满额 8h（与 join ack / DB 一致）。
- [x] 开钓后剩余随墙钟下降；`session_timer_tick` 同步后仍下降。
- [x] checkpoint 后不因 base 上涨双计；本地冻结基线不被上调。
- [x] rejoin 同样应用 join ack 额度种子。
- [x] `npm run verify:bug19-quota` 与 `npx tsx scripts/verify-bug14-daily-remaining.ts` 通过。
- [x] 回归：BUG-19 结算口径、BUG-18 无静默 DEMO、FISH-SPOT-1 先选点再开钓。

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| ack 与 snapshot 乱序 | seed + merge 取 max；updated upsert |
| 服务端未重启仍跑旧 ack | 运维重启；verify 契约含 join ack 字段 |
| 与 BUG-19「只信服务端」冲突 | 种子来自服务端 ack；合并不反推跨日脏值 |

---

## 7. 变更记录

| 日期 | 说明 |
|---|---|
| 2026-08-10 | 初稿：**BUG-20**；定责 BUG-19 后插值对消 + 进塘未选点缺额度字段 |
| 2026-08-10 | 实现：冻结基线+墙钟；join ack 种子/合并/upsert；闲置取 max；verify 增补；状态 → **已实现** |
