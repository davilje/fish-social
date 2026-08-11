# BUG 修复：进塘首帧状态错误与误切演示模式

| 项 | 内容 |
|---|---|
| 功能名称 | 每日额度进塘快照错误、收杆丢失结算与演示降级 |
| 状态 | **已实现** |
| 编号 | **BUG-18** |
| 设计时间 | **2026-08-10** |
| 完成时间 | **2026-08-10** |
| 优先级 | P0 |
| 目标版本 | hotfix |
| 关联 | BUG-15 · BUG-17 · FISH-SPOT-1 |
| 关联代码 | `mobile/lib/usePondSocket.ts` · `mobile/app/pond/[id].tsx` · `server/src/socketPondHandlers.ts` · `server/src/pondUserManager.ts` · `server/src/fishingStateMachine.ts` |

## 1. 现象

每次进入鱼塘时，底栏可能显示错误的「今日已满」；收杆后今日剩余又恢复为 8 小时。正确规则应是：每日额度只在上海时间 00:00 刷新，收杆只增加当日已用时长。

## 2. 根因

1. usePondSocket 切换鱼塘或重新连接时没有立即清空 users、myUserId、消息和生态状态。快照到达前，页面继续使用上一个鱼塘的 PondUser。
2. Socket 未连接时存在约 10 秒的 DEMO_USERS 自动降级。真实数据未明确区分连接失败与演示模式，导致用户看到一次突兀的状态替换。
3. 页面在收到有效 pond_snapshot 前仍渲染额度和开始按钮，未建立「数据未就绪」门禁。
4. 真实快照、pond_user_updated、演示数据三种来源缺少统一的数据代际/连接状态保护，旧事件可能覆盖新鱼塘状态。
5. 收杆的最终结算依赖 `stopping` 状态定时器；`stop_fishing` ack 没有返回最终额度，若最终更新丢失，客户端无法确认本次结算。
6. `todayFishingMs` 同时承担数据库基线和 enrich 后展示值，前端无法区分“今日已落库”与“本局尚未结算 elapsed”。

## 3. 修复方案

### 3.1 客户端状态生命周期

- 新鱼塘连接开始时清空 users、myUserId、messages、ecology、error 和 demo 状态。
- 为连接/快照增加 snapshotReady 与连接代际标记；旧 Socket 的事件不得更新当前页面。
- pond_snapshot 到达前显示连接中占位，额度显示为「加载中」，开始按钮禁用。
- 只有快照确认属于当前 pondId / 当前连接代际后，才允许渲染个人额度。

### 3.2 演示模式降级

- 生产/真实运行默认禁止 10 秒后静默切换 DEMO_USERS。
- 演示模式必须由显式开发开关 EXPO_PUBLIC_DEMO_MODE=1 启用。
- Socket 连接失败时显示连接失败信息，不得把真实用户状态替换为 demo 用户。

### 3.3 服务端与可观测性

- 额度唯一持久化口径是 `daily_fishing(user_id, date_key, ms)`；`date_key` 使用 Asia/Shanghai。
- 进入鱼塘的 `pond_snapshot` 必须按当前 `playerId + 当前上海日` 从数据库读取额度，不能使用旧内存用户值。
- 收杆时执行幂等 `settleFishingSession(finalize)`，把本局 elapsed 写入当日 `daily_fishing`，然后清空 `fishingStartedAt`。
- 收杆 ack 返回最终 `todayFishingMs`、`todayRemainingMs`、`quotaDateKey`；随后仍发送最终 `pond_user_updated` 做广播一致性。
- `stopping` 动画可以保留，但不能成为唯一结算触发点；状态机重复结算必须幂等。
- 在钓鱼中允许分段落账；分段落账后前移锚点，收杆只结算剩余段，禁止重复计时。
- 日切只在上海 00:00 换 `date_key`；收杆不得清零今日累计。
- 记录 `fishing_session_settled`，至少包含 playerId、userId、dateKey、elapsedMs、creditedMs、todayFishingMs、reason。

### 3.4 客户端额度展示

- `snapshotReady` 只在当前连接、当前鱼塘的 `pond_snapshot` 成功应用后设为 true。
- 快照前显示「加载中」，但不能显示「今日已满」。
- 快照后使用当前 `myUserId` 对应的用户显示服务端额度。
- 钓鱼中：`今日剩余 = 8h - (今日已用基线 + 本局 elapsed)`。
- 收杆成功后优先应用 ack 的最终额度，不能用本地 `todayFishingMs = 0` 覆盖服务端结果。

## 4. 非目标

- 不改每日 8 小时额度规则。
- 不重做 BUG-14 的钓鱼中本地插值。
- 不在本 Bug 内实现 FISH-SPOT-1 的手动选点。

## 5. 验收标准

- [x] 从地图进入鱼塘，快照到达前不显示上一个鱼塘的额度或「今日已满」。
- [x] 正常连接后首个额度直接来自当前上海日数据库记录。
- [x] 当日已用 2 小时，重进鱼塘显示约 6 小时，而不是 8 小时或「今日已满」。
- [x] 收杆后本局 elapsed 写入当日 `daily_fishing`，剩余时间减少，不恢复为 8 小时。
- [x] 收杆重复触发、状态机延迟触发、断线兜底不会重复计时。
- [x] 只有上海时间 00:00 换日；跨日后额度恢复为新日期的记录。
- [x] `stop_fishing` ack 返回最终额度，客户端收到后立即显示正确值。
- [x] Socket 连接失败不会静默替换为 DEMO_USERS；演示模式有明确标识且只能显式开启。
- [x] 快速切换鱼塘时旧 Socket 事件不会覆盖当前鱼塘。
- [x] 回归 BUG-14、BUG-15、BUG-17。

## 6. 变更记录

| 日期 | 说明 |
|---|---|
| 2026-08-10 | 初稿：BUG-18；登记进塘首帧旧状态、收杆结算丢失、额度重置和演示降级问题 |
| 2026-08-10 | 实现连接代际与快照门禁、显式演示开关；收杆立即幂等结算并通过 ack 返回上海日最终额度；补充 stopping 重复请求保护 |
