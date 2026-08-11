# BUG 修复：钓鱼中「今日剩余」时长不刷新



| 项 | 内容 |

|----|------|

| 功能名称 | 今日剩余钓鱼时长不随垂钓流逝刷新 |

| 状态 | **已实现** |

| 编号 | **BUG-14** |

| 设计时间 | **2026-07-27** |

| 完成时间 | **2026-07-27** |

| 优先级 | P0 |

| 目标版本 | hotfix |

| 关联 | BUG-13（头顶会话秒表）· FISH-DAILY-1 · PERF-03b `session_timer_tick` |

| 关联代码 | `mobile/app/pond/[id].tsx` · `usePondSocket.ts` · `server/.../pondUserManager.ts`（`computeFishingDuration` / `enrichPondUser`） |



---



## 1. 现象



鱼塘底栏文案 **「今日剩余：…」** 在玩家**正在钓鱼**时长时间不变；停钓、换相位或重进塘后才突然跳变。



头顶「钓鱼中 · xx秒」通常仍会走（BUG-13 已修会话锚点），与底栏脱节，易被当成「计时坏了」。



---



## 2. 根因



### 2.1 客户端只减库存字段



[`pond/[id].tsx`](../../mobile/app/pond/[id].tsx)：



```ts

remainingMs = max(0, MAX_DAILY_FISHING_MS - me.todayFishingMs)

```



未计入**本局已进行时长** `(now - fishingStartedAt)`。



### 2.2 Tick 不推今日累计



服务端 `session_timer_tick`（约 1s）只带 `userId` + `sessionFishingMs`，客户端合并时**不更新** `todayFishingMs`。



`todayFishingMs` 主要来自快照 / `pond_user_updated`（`enrichPondUser` 里才会用 `computeFishingDuration` 把本局 elapsed 加进今日）。



故钓鱼过程中底栏几乎拿不到新的「已用今日时长」。



### 2.3 与服务端口径对照



服务端已有正确合成：



```ts

// pondUserManager.computeFishingDuration

// fishing 中: todayFishingMs + (now - fishingStartedAt)

// enrichPondUser 写出的 todayFishingMs 即「展示用今日已用」

```



客户端底栏未复用该口径，也未在本地用锚点插值。



---



## 3. 修复方案（定案）



**以客户端插值为主（推荐，改动小、与头顶秒表一致）：**



在鱼塘页（或抽 `lib/fishingDuration.ts`）：



```ts

function effectiveTodayUsedMs(user: PondUser, now: number): number {

  const base = user.todayFishingMs ?? 0;

  if (isFishingActive(user.fishingPhase) && user.fishingStartedAt != null) {

    // 注意：若 todayFishingMs 已是 enrich 含本局的值，勿重复加 elapsed

    // 定案：约定客户端收到的 todayFishingMs 为「已落库/已结算基线」

    //       （快照/update 若带来 enrich 含本局的值，合并时需剥离或改用基线字段）

    return base + Math.max(0, now - user.fishingStartedAt);

  }

  return base;

}



remainingMs = max(0, MAX_DAILY_FISHING_MS - effectiveTodayUsedMs(me, now))

```



**合并规则（避免双计）：**



| 来源 | `todayFishingMs` 含义 | 客户端 |

|------|----------------------|--------|

| 理想 | 仅持久化基线（不含本局未 flush） | `base + elapsed` |

| 现状 enrich | 常含本局 elapsed | 插值时若 `base` 已 ≥ `elapsed` 且接近 `base' + elapsed`，用 `max(base, elapsed)` 或 **改 emit：tick/展示用基线** |



**推荐实现顺序：**



1. **P0 客户端**：底栏用 `now` 本地定时刷新（与 `PondScene` 同频或 250ms～1s）；  

   `remaining = 8h - max(todayFishingMs, 本局插值)` 的稳妥式：  

   `used = isFishingActive && startedAt ? max(todayFishingMs, todayFishingMs_baseline + (now-startedAt))`  

   更简单稳妥：**显示剩余时**  

   `usedDisplay = isFishingActive && startedAt != null  

     ? (todayFishingMs_at_session_start + (now - startedAt))  

     : todayFishingMs`  

   在 `start_fishing` 成功 / 收到带新 `fishingStartedAt` 时缓存 `todayBaseline = todayFishingMs`（若当时 enrich 已含 0 本局，baseline 即库值）。



2. **可选服务端**：`session_timer_tick` 增加 `todayFishingMs`（enrich 后）供对齐；非必须。



3. **验收**：钓鱼中底栏每秒下降；停钓后与服务端今日剩余一致；不超过 8h；不破坏 BUG-13 头顶秒表。



---



## 4. 非目标



- 改 8h 上限数值、上海日切规则（FISH-DAILY-1）  

- 重做头顶会话秒表语义（BUG-13）  

- 改 Socket 事件名  



---



## 5. 验收标准



- [x] 开始钓鱼后，「今日剩余」随时间减少（肉眼可见秒/分变化）  

- [x] 停钓后剩余与再次进塘/快照一致（误差 ≤ 2s）  

- [x] 不会出现剩余突然加回（双计）或跳到负数  

- [x] 头顶「钓鱼中 · 会话时长」行为不回归 BUG-13  

- [x] 未钓鱼时仍只按 `todayFishingMs` 显示  



---



## 6. 风险



| 风险 | 缓解 |

|------|------|

| enrich 的 `todayFishingMs` 已含本局导致双计 | 开钓时记下 baseline；或 tick 带 baseline |

| 仅改底栏忘刷新 `now` | 鱼塘页增加轻量 interval 或复用场景 tick |



---



## 7. 变更记录



| 日期 | 说明 |

|------|------|

| 2026-07-27 | 初稿：**BUG-14** 已确认；根因 + 客户端插值修复方案 |

| 2026-07-27 | **已实现**：`fishingDuration.ts` 基线+本局插值；鱼塘页 250ms 刷新底栏 |

