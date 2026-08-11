<!-- 来源: docs/planning/specs/Admin-能力不足分析与补充方案.md（ADMIN-OBS-1.1） -->
<!-- 用途: 后端 + admin-web + 手机 Debug — Admin 排障增强 MVP -->

你是 Fish Social **全栈开发 Agent**（偏后端 + admin-web）。实现 **Admin 排障增强 v1.1 MVP（ADMIN-OBS-1.1）**，目标：策划/开发 **不翻 stdout、不找 AI** 也能确认「waiting + fishingStartedAt=null → 计时卡死；因 checkpoint_restore / 服务重启」。

## 必读

1. [`docs/planning/specs/Admin-能力不足分析与补充方案.md`](../specs/Admin-能力不足分析与补充方案.md) — §5–§7 权威设计与验收
2. 现有实现：
   - `server/src/admin.ts` — 路由挂载
   - `server/src/liveSessionInspector.ts` — **当前错误实现**（读 metrics 猜状态）
   - `server/src/fishingDebug.ts` — `activeFishers`（缺锚点字段）
   - `server/src/pondUserManager.ts` — `enrichPondUser` / `listUsersInPond` / `listHumansInPond`
   - `server/src/playerPondSession.ts` — `loadPlayerPondSession` / `applyCheckpointToUser`
   - `admin-web/src/pages/{PondsPage,BusinessHealthPage,FishingDebugPage,LiveInspectorPage,TimelinePage}.tsx`
   - `admin-web/src/api.ts`
   - `mobile/components/AdminPondFishDebugGrid.tsx`（可选同 PR 渲染 activeFishers）
3. 关联 SOP：[`排查-挂机断线诊断阶段2-4.md`](../specs/排查-挂机断线诊断阶段2-4.md)

## 本次范围 = MVP（必须）

| ID | 任务 |
|----|------|
| A | `GET /api/admin/players/:playerId/live-state` + diagnostics |
| B | 重写 Live Inspector（读内存，不猜 metrics） |
| C | 扩展 `activeFishers` 锚点字段；至少 admin-web 或 mobile 一处 UI |
| D | `server_start`（+ 优雅停机 `server_stop`）埋点 |
| E | 修复 admin-web Ponds / BusinessHealth / FishingDebug 字段映射 |
| BUG | checkpoint / 重连恢复时补 `fishingStartedAt`（与排障同因，一并修） |

## 明确不做（P1/P2）

- 一键 diag-pack、client-logs 手机上报、RBAC/Loki/Alert UI Tab
- 激活 `pushLiveSession` 推送（可保留轮询读 live-state）
- 改运营日报流水线

---

## 任务 0 — 并行 BUG：补计时锚点（P0）

文件：`playerPondSession.ts`（`applyCheckpointToUser`）· `fishingStateMachine.ts`（`resumeAfterReconnect` / `restoreDefaultDisconnectedPhase`）· 必要时 `syncStatus`

**规则**：任意路径使 `status === 'fishing'`（或 `isFishingActive(phase)`）且 `fishingStartedAt == null` 时：

```ts
user.fishingStartedAt = Date.now();
```

建议抽 `ensureFishingStartedAt(user: PondUser): void`，在 checkpoint 应用后与 reconnect 恢复后各调用一次。

**不要**在 `disconnected` / `idle` / `seated` 时误设锚点。

验收：模拟 checkpoint 恢复 waiting → live-state 中 `fishingStartedAt != null` 且 `sessionFishingMs` 随时间增长。

---

## 任务 1 — 进程生命周期埋点（D）

1. 在 `server/src/index.ts`（或独立 `serverLifecycle.ts`）记录：

```ts
export const SERVER_STARTED_AT = Date.now();
```

2. listen 成功后写入 metrics（与现有 `recordFishingMetric` / structured metric 一致；`playerId`/`pondId` 可空或用系统占位，**避免** metrics-validation 刷屏——按 schema 扩展或走 `logStructuredEvent` + 可选 metrics）：

| event | 时机 |
|-------|------|
| `server_start` | `server_ready` 时 |
| `server_stop` | SIGINT/SIGTERM 优雅停机路径（强制杀可能没有） |

3. `live-state` 的 `server` 块暴露 `{ startedAt, uptimeSec, pid }`。

若 `metrics-schema` 需登记新事件类型，一并更新 `shared/metrics-schema.ts` 与埋点表（若有脚本）。

---

## 任务 2 — `GET /api/admin/players/:playerId/live-state`（A）

挂到 `admin.ts`，`requireAdmin`（与现有一致；viewer 可读亦可，与 live-session 对齐用 `requireRole('viewer')`）。

**查找**：遍历 `PONDS` + `listUsersInPond`，匹配 `playerId` 且优先 `!isBot`。

**响应契约**（字段名保持与 spec §5.1 一致）：

```ts
{
  playerId: string;
  found: boolean;
  pondId: string | null;
  user: null | {
    userId: string;
    nickname: string;
    spotId: string | null;
    status: string;
    fishingPhase: string | null;
    fishingStartedAt: number | null;
    sessionFishingMs: number;
    todayFishingMs: number;
    disconnectedAt: number | null;
    phaseEndsAt: number | null;
    isBot: boolean;
  };
  checkpoint: null | {
    exists: boolean;
    fishingPhase: string | null;
    spotId: string | null;
    disconnectedAt: number | null;
    updatedAt: number;
  };
  socketBound: boolean; // 能否 resolve 到该 userId 的 socket
  diagnostics: Array<{ id: string; level: 'error' | 'warn' | 'info'; message: string }>;
  server: { startedAt: number; uptimeSec: number; pid: number };
}
```

**diagnostics 最少规则**：

| id | 条件 |
|----|------|
| `missing_fishing_started_at` | `status===fishing`（或 active phase）且 `fishingStartedAt==null` → **error** |
| `player_not_in_pond` | `found===false` → info |
| `recent_server_start` | `uptimeSec < 300` → warn（可选） |

`sessionFishingMs` / `todayFishingMs` 必须经 `enrichPondUser` 计算。

---

## 任务 3 — 重写 Live Inspector（B）

文件：`server/src/liveSessionInspector.ts` · `admin-web/src/pages/LiveInspectorPage.tsx`

### 3.1 服务端 SSE payload

每秒（或保持现间隔）组装：

```ts
{
  type: 'tick',
  live: <live-state 同构或直接嵌套>,
  recentEvents: Array<{ eventType, createdAt, pondId, payloadSummary }>, // 最近 10 条关键事件
  timestamp: number
}
```

- **禁止**再用 `now - lastEvent.createdAt` 冒充 `fishingMs`
- `phase` / `fishingMs` 必须来自内存 live-state
- `recentEvents` 可仍查 timeline，但仅作辅证；过滤优先：`disconnect*` / `reconnect` / `leave_pond` / `join_pond_*` / `server_start` / `fishing_start|stop`

### 3.2 admin-web UI

结构化卡片（不要只刷原始 JSON 行）：

- Phase · SessionMs · StartedAt（**null 时红字**）
- Checkpoint 摘要
- diagnostics 列表
- 最近事件表（`joinKind=checkpoint_restore` 高亮）

保留 playerId 输入 + 连接/断开。

---

## 任务 4 — Fishing Debug `activeFishers`（C）

文件：`server/src/fishingDebug.ts` · shared 类型（若有 `PondFishingDebugResponse`）

扩展每项：

```ts
{
  userId: string;
  playerId?: string;
  nickname?: string;
  isBot: boolean;
  spotId: string;
  fishingPhase: string;
  phaseEndsAt?: number | null;
  fishingStartedAt: number | null;
  sessionFishingMs: number;
  disconnectedAt?: number | null;
  equippedBaitId: string;
  equippedTackleId: string;
}
```

**UI（至少一个）**：

- `admin-web` FishingDebugPage：表格渲染 `activeFishers`（同时修类型 stub）
- 和/或 `AdminPondFishDebugGrid`：增加「在钓玩家」区块

---

## 任务 5 — 修复 admin-web 映射（E）

| 页 | 修复 |
|----|------|
| `PondsPage.tsx` | 使用 `pondId`、`summary.fishCount` / `maxPopulation`、`humanCount`、`botCount`、`botRatio` |
| `BusinessHealthPage.tsx` | 使用 `dateKey`、`totalCatch`、`disconnectRate`、`avgPopulation`（对照 `businessHealth.ts` 实际返回） |
| `FishingDebugPage.tsx` | 类型改为真实 `PondFishingDebugResponse`（从 shared 或本地对齐），渲染 spots + activeFishers |
| `api.ts` | 删除错误 stub；增加 `liveState(playerId)` 方法 |

手测：打开 `http://localhost:5173/admin-web/`（或静态 `/admin-web/`），鱼塘数字非空白。

---

## 任务 6 — Timeline 最小高亮（MVP 可选但推荐）

`TimelinePage.tsx`：对 `eventType` 含 `disconnect` / `reconnect` / `disconnect_timeout` / `leave_pond` / `server_start`，或 payload `joinKind===checkpoint_restore`，加 CSS 色条。完整 SOP 着色可留 P1。

---

## 任务 7 — 验收脚本

新增 `scripts/verify-admin-observability-v1.ts` +：

```json
"verify:admin-observability": "npm run build:shared && npx tsx scripts/verify-admin-observability-v1.ts"
```

**断言**：

1. `liveSessionInspector.ts` **不再**用 `lastEvent.createdAt` 计算 fishingMs（源码扫描或行为测）
2. 存在路由 `/api/admin/players/:playerId/live-state`（读 `admin.ts`）
3. `fishingDebug.ts` activeFishers 含 `fishingStartedAt` / `sessionFishingMs`
4. `applyCheckpointToUser` 或 `ensureFishingStartedAt` 存在且在恢复路径被调用
5. admin-web `PondsPage` 含 `summary.fishCount` 或 `humanCount` 字符串
6. （可选）启内存用户：fishing + null startedAt → diagnostics 含 `missing_fishing_started_at`；调用 ensure 后锚点非空

---

## 回归

```bash
npm run build:shared
npm run verify:admin-observability
npm run verify:session-timer-broadcast
npm run verify:session-checkpoint
npm run verify:disconnect-reconnect
```

手动：

1. 登录 admin-web → 鱼塘概览有数字
2. Live Inspector 填真实 `playerId` → phase/sessionMs 与游戏内一致
3. 杀进程重启服务 → timeline/metrics 可见 `server_start`；若未点停钓，恢复后计时应走动（BUG 修复）

---

## 完成后

1. 更新 spec §7 MVP 勾选；状态可保持「已确认」直至策划 `planning:accept`
2. CHANGELOG **实现** 小节
3. 回复：改动文件列表 + verify 摘要

## commit 建议

```text
feat(admin): live-state API, real Live Inspector, and checkpoint timer anchor

Expose in-memory fishingStartedAt/sessionFishingMs for triage, fix admin-web
field mappings, emit server_start, and restore timer anchor after checkpoint.
```
