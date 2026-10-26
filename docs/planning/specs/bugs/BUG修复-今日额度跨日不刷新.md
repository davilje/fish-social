# BUG 修复：今日钓鱼额度跨日不刷新 / 误显已满

| 项 | 内容 |
|----|------|
| 功能名称 | 今日额度与上海日库表脱节；未钓鱼却显示「今日已满」 |
| 状态 | **已实现** |
| 编号 | **BUG-15** |
| 设计时间 | **2026-07-27** |
| 完成时间 | **2026-07-27** |
| 优先级 | P0 |
| 目标版本 | hotfix |
| 关联 | FISH-DAILY-1 · BUG-14 · `daily_fishing` · `ensureFishingDayRollover` |
| 关联代码 | `server/src/pondUserManager.ts` · `pondSession.ts` · `mobile/app/pond/[id].tsx` |

---

## 1. 现象

1. 玩家**未在钓鱼**时，鱼塘底栏显示 **「今日剩余：0秒」**，开始按钮为 **「今日已满」**。  
2. 跨过上海自然日 0 点后，额度**不会自动恢复**为接近满额（或需重启服务/重进才偶发正常）。  
3. 与「库里的当日已用」不一致：排查时上海日 `daily_fishing` **可无今日行**（已用应为 0），但 UI 仍显示已满。

非本单：钓鱼中剩余秒数不走（见 **BUG-14**，已实现）。

---

## 2. 根因（定案）

### 2.1 内存 `todayFishingMs` 与 DB 不同步

`ensureFishingDayRollover` 在 `fishingDayKey === 今日` 时 **直接 return**，不再 `getTodayFishingMs`。  
内存一旦被抬高（重复 flush、异常锚点、历史超计），**当天会一直「已满」**，即使 `daily_fishing` 当日行为空或更小。

### 2.2 闲置跨日无推送

闲置玩家几乎无周期性 `enrich`；上海 0 点后若无 join / 开停钓，**客户端一直拿旧 `todayFishingMs`**，表现为「跨日不刷新」。

### 2.3 账本曾被错误写入（旁证）

同账号历史可见：单日累计 **>8h**、以及 `date_key=1970-01-01` 行（疑似 `atMs≈0` / 非法锚点）。说明 `addTodayFishingMs` 缺封顶与非法时间防护，会放大「误满」与排障噪声。

---

## 3. 修复方案（定案）

**以后端为准（主），客户端只消费推送。**

| # | 改动 | 说明 |
|---|------|------|
| 1 | **未在钓时强制对齐 DB** | `enrichPondUser` / join / checkpoint 恢复：若非活跃钓鱼，`user.todayFishingMs = getTodayFishingMs(playerId)`（可 `min` 到 `MAX_DAILY`） |
| 2 | **同日也允许「降噪对齐」** | `fishingDayKey === today` 时：未在钓仍可读库覆盖内存；在钓仅校正 baseline（勿用 enrich 展示值写回内存当基线） |
| 3 | **跨日推送** | 日切检测到 key 变化后，对塘内该用户 `emitPondUserUpdated`（或轻量日切 tick：扫描在塘人类并 rollover+emit） |
| 4 | **写入防护** | `addTodayFishingMs`：`delta` 合理上限；累计 `min(..., MAX_DAILY)`；拒绝 `fishingStartedAt` 过旧/非法再计时；可选清理/忽略 `1970-01-01` 脏行（运维脚本可另做，非必须进主路径） |
| 5 | **开钓校验** | `start_fishing` 上限继续用 **当日上海日 DB + 本局未 flush 段**，与 UI 同源 |

### 非目标

- 不改 8 小时常量数值本身  
- 不重做 BUG-14 底栏插值语义  
- 不在本单做 Admin 清额度 UI（可后续 OPS）

---

## 4. 验收标准

- [x] 未钓鱼且当日 `daily_fishing` 无行或 ms=0 时，底栏剩余 ≈ **8 小时**，按钮为「开始钓鱼」  
- [x] 故意把内存 `todayFishingMs` 抬高后，一次 enrich/join 后与 DB 对齐（未在钓）  
- [x] 上海日跨日（可用测时钟）：闲置在塘玩家在合理时间内（≤1 分钟或下次 enrich）剩余恢复接近满额，且 **不必重启进程**  
- [x] 跨日仍在钓：不打断相位；剩余按 FISH-DAILY-1（日界后段）刷新  
- [x] `addTodayFishingMs` 单日累计不超过 `MAX_DAILY_FISHING_MS`（写入侧封顶）  
- [x] 回归：BUG-14 钓鱼中剩余仍逐秒下降；BUG-13 头顶秒表不闪 0  
- [x] 有 verify 脚本或扩展既有 `verify-fish-daily-shanghai-rollover` 覆盖「同日内存脏数据被 DB 纠正」

---

## 5. 涉及文件（预估）

- `server/src/pondUserManager.ts`（rollover / enrich / addToday）  
- `server/src/serverLoops.ts` 或等价（可选日切扫描）  
- `server/src/pondSession.ts`（开钓校验保持同源）  
- `scripts/verify-fish-daily-*.ts`（扩展）  
- 客户端通常 **无需改**；若需在跨日主动拉快照再评估

---

## 6. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-27 | 初稿：**BUG-15** 已确认；内存/DB 脱节 + 跨日无推送 + 写入封顶 |
| 2026-07-27 | **已实现**：enrich/同日读库对齐 · `syncHumanQuotaAndEmit` 30s · addToday 封顶 · verify 扩展 |
| 2026-07-28 | **回归修复**：`sanitize` 禁止夹成 now−8h；`safeFishingElapsedMs`（>8h 未入账段记 0）；leave/flush/disconnect 统一 |
