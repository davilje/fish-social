# BUG 修复：每日钓鱼额度单一口径重构

| 项 | 内容 |
|---|---|
| 功能名称 | 每日额度字段语义拆分、单一结算出口与客户端只读展示 |
| 状态 | **已实现** |
| 编号 | **BUG-19** |
| 设计时间 | **2026-08-10** |
| 完成时间 | **2026-08-10** |
| 优先级 | P0 |
| 目标版本 | hotfix |
| 关联 | FISH-DAILY-1 · BUG-14 · BUG-15 · BUG-16 · BUG-18 · BUG-20（展示回归） |
| 关联代码 | `server/src/pondUserManager.ts` · `server/src/fishingStateMachine.ts` · `server/src/socketPondHandlers.ts` · `server/src/serverLoops.ts` · `mobile/lib/fishingDuration.ts` · `mobile/lib/usePondSocket.ts` · `mobile/app/pond/[id].tsx` · `shared/types.ts` |

## 1. 背景与目标

### 1.1 背景

现场仍复现：

1. 开钓约 **30 秒**后「今日剩余」突然跳变 / 像归零；
2. **收杆**后剩余时间又像重置回接近 8 小时；
3. **离塘再进**偶发「今日已满」。

根因分析（定案，非再打局部补丁）：

| 时间 | 需求 | 作用 |
|------|------|------|
| 2026-07-15 | FISH-DAILY-1 | 引入上海日 `daily_fishing`（基线正确） |
| 2026-07-27 | **BUG-14** | 客户端假设 `fishingStartedAt` **整局不变**，用 `baseline + elapsed` 插值 |
| 2026-07-27 | **BUG-15** | 每 30s `syncHumanQuotaAndEmit`；`todayFishingMs` 同时当内存 / DB / 展示 |
| **2026-07-28** | **BUG-16** | 在钓 **30s 分段落账并前移锚点** → 与 BUG-14 假设直接冲突 |
| 2026-08-10 | BUG-18 | 在旧结构上叠加收杆立即结算 / ack / 清空状态，写入口更多 |

**主因是 BUG-16 分段落账与 BUG-14 客户端插值不兼容；放大器是 `todayFishingMs` 一词三义与多处 settle。** 局部修 merge / 反推基线无法收敛。

### 1.2 目标

- 额度只认一个持久化账本：`daily_fishing(user_id, Asia/Shanghai date_key, ms)`。
- 网络/内存只暴露 **正交字段**：已落账基线、本局展示锚点、本局已用、今日剩余。
- 结算只走 **一个出口**；分段落账不得破坏「本局展示锚点」。
- 客户端 **禁止** 用 `todayFishingMs - elapsed` 反推基线；只读服务端字段做展示插值。
- 收杆 / 离塘 / 重进后的剩余时间与 DB 一致，不再回弹 8h 或误显已满。

### 1.3 非目标

- 不改每日 **8 小时**上限与上海 **00:00** 换日规则（沿用 FISH-DAILY-1）。
- 不做断线期间离线挂机继续计时。
- 不重做钓点选择（FISH-SPOT-1）、离塘导航（BUG-17）、演示模式开关（BUG-18 已定）。
- 不借机大重构状态机相位表；仅改额度字段与结算/展示契约。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 玩家 | 当日已钓 2h，进塘 | 首帧（快照后）剩余 ≈ 6h，不显示「今日已满」 |
| 玩家 | 开钓连续 > 60s（跨过至少一次 30s 扫描） | 剩余平滑减少，不在 ~30s 跳回 8h / 归零 |
| 玩家 | 本局钓 30s 后收杆 | DB 今日已用 +≈30s；底栏立即显示对应剩余；不回弹 8h |
| 玩家 | 收杆后离塘再进 | 剩余与收杆后一致 |
| 玩家 | 钓鱼中断线再重连 | 已落账段保留；本局展示从重连锚点继续；不重复计断线期 |
| 运维 | 查库 / 日志 | 能区分 base / session / settle reason，无一次写满 8h 的脏路径 |

---

## 3. 功能范围

### 3.1 字段契约（P0，权威）

在 `PondUser`（及快照 / `pond_user_updated` / 必要 ack）中固定语义：

| 字段 | 含义 | 谁写 | 客户端可否改 |
|------|------|------|--------------|
| `todayFishingBaseMs` | 当前上海日 **已写入** `daily_fishing` 的 ms | 仅服务端，来自 DB | 否 |
| `sessionStartedAt` | **本局展示锚点**（开钓时设定；收杆/离塘清空） | 仅服务端 | 否 |
| `sessionFishingMs` | `now - sessionStartedAt`（在钓时） | 服务端 enrich / tick | 否 |
| `todayRemainingMs` | `max(0, MAX - base - sessionElapsed)` | 服务端 enrich 推荐带上 | 可本地用同公式复核 |
| `fishingStartedAt` | **废弃作额度锚点**；兼容期内可与 `sessionStartedAt` 同步，但 **禁止** 因分段落账被前移 | 过渡期 | 否 |
| `todayFishingMs` | **兼容只读**：对外等于 `base + sessionElapsed`（展示总量）；**禁止**再当作「仅 DB」读写 | 服务端派生 | 否 |

规则：

1. **分段落账**只增加 `daily_fishing` / `todayFishingBaseMs`，并维护内部「已落账到时刻」checkpoint；**不得**修改 `sessionStartedAt`。
2. **展示已用**始终：`base + max(0, now - sessionStartedAt)`（未在钓则 session=0）。
3. **禁止**客户端 `stored - elapsed` 反推基线；删除或停用该路径。

### 3.2 单一结算出口（P0）

保留并收紧 `settleFishingSession`：

| mode | 用途 | 对 `sessionStartedAt` | 对 DB |
|------|------|----------------------|-------|
| `checkpoint`（原 advance 语义重命名） | 30s / 长会话防 >8h 单段 | **不变** | `+=` 自上次 checkpoint 的增量 |
| `finalize` | stop / leave / disconnect / 相位真正结束 | **清空** | 结算剩余未落账段 |

要求：

- stop / leave / disconnect / 宽限期兜底 / 相位收尾 **全部**只调此出口；删除平行的「手写 todayFishingMs +=」路径（bot 内存展示除外且不写人类 DB）。
- `stop_fishing`：**先 finalize，再**进入/保持 stopping 动画；ack 必须带 `todayFishingBaseMs`、`todayRemainingMs`、`quotaDateKey`；广播的用户对象与 ack **同口径**。
- 幂等：`sessionStartedAt == null` 且无 pending checkpoint → finalize 记 0。
- 重复 stop、状态机延迟收尾、断线兜底不得双计。

### 3.3 30s 扫描职责拆分（P0）

`syncHumanQuotaAndEmit`（`QUOTA_DAY_SYNC_MS = 30_000`）：

- **闲置人类**：跨日 rollover + 内存 base 对齐 DB（BUG-15 保留）。
- **在钓人类**：仅 `checkpoint` 落账 + 推送更新后的 `todayFishingBaseMs` / `todayRemainingMs`；**禁止**前移展示锚点。
- 推送不得导致客户端把剩余重算回 8h。

### 3.4 客户端展示（P0）

- 快照未就绪：显示「加载中」，禁用开钓；**禁止**「今日已满」。
- 快照后：`剩余 = todayRemainingMs`（或 `MAX - base - sessionElapsed`，字段以服务端为准）。
- 钓鱼中允许 250ms 本地插值，但 **base 只信** `todayFishingBaseMs`；`sessionStartedAt` 变化仅当服务端明确开新局。
- 收杆 ack 到达后：立即采用 ack 的 base/remaining，清空本地 session 插值状态。
- `mergePondUserUpdated`：不得在 stopping/finalize 后把已清空的锚点「补回来」（仅允许明确的 legacy waiting 兼容策略，且不得影响额度字段）。

### 3.5 脏数据治理（P0）

- 实现后提供一次性运维/脚本或 verify 步骤：检测当日 `daily_fishing.ms == MAX` 且无合理会话的异常行；文档说明如何人工校正（不自动清空全服）。
- `safeFishingElapsedMs` / 封顶逻辑保留；**禁止**恢复「夹成 now−8h 再记满一天」。

### 3.6 可观测性（P1）

结算日志至少：`playerId, userId, dateKey, reason, mode, creditedMs, baseAfterMs, sessionStartedAt, checkpointAt`。  
若新增 `eventType`，按 metrics-catalog-sync 更新埋点表。

---

## 4. 技术影响

### 4.1 数据模型

- DB：继续只用 `daily_fishing`；可不加表。
- 内存：人类用户增加/规范 `todayFishingBaseMs`、`sessionStartedAt`、内部 `quotaCheckpointAt`（可不上网）。
- Shared：更新 `PondUser` 与 `stop_fishing` ack 类型。

### 4.2 API / Socket

| 类型 | 名称 | 说明 |
|------|------|------|
| Socket 出 | `pond_snapshot` / `pond_user_updated` | 带齐 base / sessionStartedAt / remaining |
| Socket 入 | `stop_fishing` ack | 返回最终 base + remaining + dateKey |
| Socket 入 | `start_fishing` | 校验 seated + 当日剩余 > 0；设置 `sessionStartedAt=now` |

### 4.3 涉及文件（预估）

- `shared/types.ts`（及必要导出）
- `server/src/pondUserManager.ts` · `fishingStateMachine.ts` · `socketPondHandlers.ts` · `pondSession.ts` · `serverLoops.ts`
- `mobile/lib/fishingDuration.ts` · `usePondSocket.ts` · `mobile/app/pond/[id].tsx`
- `scripts/verify-fish-daily-shanghai-rollover.ts`（扩展 BUG-19 断言；可新增 `scripts/verify-bug19-quota-contract.ts`）

---

## 5. 验收标准

- [x] 字段契约落地：`todayFishingBaseMs` 仅表示 DB；展示不再依赖反推基线。
- [x] 开钓跨过至少一次 30s 扫描：剩余单调不增（允许 1s 级抖动），**不**跳回满额或归零。
- [x] 本局钓约 30s 收杆：`daily_fishing` +≈30s；UI 立即显示对应剩余；不回弹 8h。
- [x] 收杆后离塘再进：剩余与收杆后一致（同上海日）。
- [x] 当日已用 2h 进塘：快照后剩余 ≈ 6h，不误显「今日已满」。
- [x] 重复 stop / 断线 finalize / 宽限期：不双计；日志可核对。
- [x] 分段落账（checkpoint）不修改 `sessionStartedAt`。
- [x] `stop_fishing` 先 finalize 再动画广播；ack 与广播口径一致。
- [x] 回归：FISH-DAILY-1 跨日、BUG-15 闲置对齐、BUG-17 导航、BUG-18 无静默 DEMO。
- [x] `npm run verify:fish-daily-shanghai`（及本单新增 verify）通过。

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 兼容旧客户端仍读 `todayFishingMs` | 保留派生填充；新逻辑以 base/session 为准 |
| 分段落账改语义后长会话再次 >8h 单段 | checkpoint 保证单段 ≪ 8h，且不碰展示锚点 |
| 历史已写满 8h 的脏账号 | 运维校正说明 + verify 检测，不自动全清 |
| 与 BUG-18 收杆 ack 重叠 | 本单收编为唯一契约，废弃冲突写法 |

---

## 7. 变更记录

| 日期 | 说明 |
|---|---|
| 2026-08-10 | 初稿：**BUG-19** 已确认；定责 BUG-14/15/16 结构冲突；方案为单一口径重构，禁止再打局部补丁 |
| 2026-08-10 | 实现：base/session/remaining 契约；checkpoint 不前移展示锚点；stop 先 finalize；客户端禁反推；`verify:bug19-quota` |
| 2026-08-10 | 后续展示回归拆单 **BUG-20**：钓鱼中剩余不走 / 未选钓点误显 8h（见 `BUG修复-进塘与钓鱼剩余展示回归.md`） |
