# BUG 修复：断线 / 离塘未结算，导致钓鱼时长丢失与残留占位

| 项 | 内容 |
|----|------|
| 功能名称 | 断线与离塘未结算本局时长；ghost 用户长期占位 |
| 状态 | **已实现** |
| 编号 | **BUG-16** |
| 设计时间 | **2026-07-28** |
| 完成时间 | **2026-07-28** |
| 优先级 | P0 |
| 目标版本 | hotfix |
| 关联 | BUG-15 · BUG-14 · BUG-06 · FISH-DAILY-1 · `daily_fishing` |
| 关联代码 | `server/src/fishingStateMachine.ts` · `pondUserManager.ts` · `pondSession.ts` · `socketLifecycle.ts` |

---

## 1. 现象

1. 钓了几分钟后收杆，底栏「今日剩余」几乎没有减少（例如仍显示 **7 小时 59 分 59 秒**），额度像**每次都重置**。
2. 期间发生过切页 / 断线重连的会话，**一分钟都没记进** `daily_fishing`。
3. 反向异常（历史脏数据）：某些账号当日 `daily_fishing.ms` 恰好等于 **28800000（正好 8h）**，进塘即「今日已满」。
4. 未显式离塘的会话在服务端长期停留在 `waiting`（ghost），继续占钓点。

非本单：客户端「返回地图无效 / 收杆按钮闪烁」见 **BUG-17**；跨日与内存脏读见 **BUG-15**（已实现）。

---

## 2. 根因（定案）

### 2.1 断线先清锚点、后无结算

`handleDisconnect` 走 `transitionPhase(user, pondId, 'disconnected', ...)`，而 `syncStatus` 对非活跃相位会把状态与锚点一起清掉：

```ts
user.status = isFishingActive(user.fishingPhase) ? 'fishing' : 'idle';
if (user.status === 'idle') user.fishingStartedAt = null;
```

`'disconnected'` 不属于 `isFishingActive`，于是 `status='idle'` + `fishingStartedAt=null`，且**整个断线路径没有任何 flush**，本局已钓时长直接蒸发。

### 2.2 60s 兜底条件恒不成立

`removeDisconnectedUser` 的入账前置是 `status === 'fishing' && fishingStartedAt !== null`；经 2.1 后两者都为假，**宽限期结束也不会补记**。

### 2.3 重连重新锚定

`resumeAfterReconnect` → `restoreDefaultDisconnectedPhase` → `ensureFishingStartedAt` 把锚点设为 `now`，秒表与额度都从 0 重新开始，用户观感就是「时长永远不累计」。

### 2.4 单次结算粒度过粗（与 BUG-15 写入防护叠加）

当前只在 stop / leave / 相位收尾时一次性结算整段会话：

- 旧实现把过旧锚点夹成 `now−8h` → 一次写满 8h（现象 3 的来源）
- BUG-15 修复后 `safeFishingElapsedMs` 对 **> 8h 的未入账段记 0** → ghost 长会话变成「一秒不记」

两种极端都源于**缺少过程中的分段落账**。

---

## 3. 修复方案（定案）

**统一出口 + 断线前结算 + 分段落账。**

| # | 改动 | 说明 |
|---|------|------|
| 1 | **统一结算函数** | 抽 `settleFishingSession(user, atMs, reason)`：`sanitize → safeFishingElapsedMs → addTodayFishingMs → 锚点处理`，供 stop / leave / disconnect / 相位收尾复用 |
| 2 | **断线前结算** | `handleDisconnect` 在 `transitionPhase(...,'disconnected')` **之前**结算本局；确保锚点被清空前时长已入库 |
| 3 | **宽限期兜底** | `removeDisconnectedUser` 保留防御式结算：即使 2 已结算也不重复计（幂等：结算后锚点置空） |
| 4 | **分段落账** | 在钓会话按固定周期（复用 30s 配额扫描）把 `now − 锚点` 的增量落库并把锚点前移，使单段永远 ≪ 8h |
| 5 | **重连不重复计** | 重连恢复相位时锚点取 `now`（分段落账已保证历史时长已入库），禁止把断线期计入 |
| 6 | **ghost 清理可观测** | 结算写结构化日志（reason / creditedMs / phase），便于排查「时长去哪了」 |

### 非目标

- 不改 8 小时上限常量
- 不改客户端（BUG-17 单独处理）
- 不做断线期间「离线挂机继续计时」

---

## 4. 验收标准

- [x] 钓 N 分钟后收杆，`daily_fishing` 今日 ms **增加 ≈ N 分钟**，底栏剩余同步减少
- [x] 钓鱼中断线（直接杀掉客户端连接）：断线瞬间已钓时长**已入库**，不因宽限期/重连丢失
- [x] 断线后 60s 宽限期结束移除用户：**不重复计**，不产生负值或超额
- [x] 断线→重连→继续钓→收杆：总入库时长 ≈ 实际在钓时长（允许断线期不计）
- [x] 长会话（> 8 小时未主动收杆）：分段落账使当日累计正常增长，**不会出现「一秒不记」**
- [x] 单日累计仍受 `MAX_DAILY_FISHING_MS` 封顶；不再出现「一次写入正好 8h」
- [x] 回归：BUG-15 闲置对齐 DB / 跨日恢复；BUG-14 底栏插值；BUG-13 头顶秒表
- [x] `npm run verify:fish-daily-shanghai` 通过，并新增断线结算 / 分段落账断言

---

## 5. 涉及文件（预估）

- `server/src/fishingStateMachine.ts`（`handleDisconnect` / `syncStatus` 前置结算）
- `server/src/pondUserManager.ts`（`settleFishingSession` / `removeDisconnectedUser` / 分段落账）
- `server/src/pondSession.ts`（leave / stop 改调统一出口）
- `server/src/serverLoops.ts`（分段落账挂到 30s 扫描）
- `scripts/verify-fish-daily-shanghai-rollover.ts`（扩展断言）

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 分段落账与一次性结算重复计 | 结算后立即前移/清空锚点，统一走 `settleFishingSession` |
| 断线瞬间写库增加 IO | 仅人类用户、单行 upsert；bot 不落库 |
| 与 BUG-15 写入防护冲突 | 分段落账保证单段 ≪ 8h，`safeFishingElapsedMs` 不会再返回 0 |

---

## 7. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-28 | 初稿：**BUG-16** 已确认；断线不结算 + 兜底失效 + 缺分段落账 |
| 2026-07-28 | **已实现**：`settleFishingSession` · 断线前 finalize · 30s advance 分段落账 · verify 扩展 |
