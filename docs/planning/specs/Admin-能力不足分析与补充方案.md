# Admin 能力不足分析与补充方案

| 状态 | **已实现（MVP）** | 目标版本 v1.1 |
|------|--------------------|---------------|
| 编号 | **ADMIN-OBS-1.1** | |
| 优先级 | P0 排障可用性（本文件）· P1/P2 见续作 | |
| 范围 | Admin API · admin-web · 手机 Debug 面板 · Live Inspector / Fishing Debug |
| 触发 | 2026-07-12 垂钓时间卡死排查：现有 Admin **无法自助定位** `fishingStartedAt=null` |
| 续作 | [`Admin-排障增强-v1.2.md`](./Admin-排障增强-v1.2.md)（**ADMIN-OBS-1.2** · 已确认） |
| 关联 | [`排查-挂机断线诊断阶段2-4.md`](./排查-挂机断线诊断阶段2-4.md) · [`三层数据体系-可观测性补充-v0.6.md`](./三层数据体系-可观测性补充-v0.6.md) · D-L2-13/14 · BUG checkpoint 计时锚点 |

---

## 0. 文档目的

当前 Admin **接口不少（40+）**，但 **排障闭环不完整**：关键运行时字段未暴露、部分页面数据映射错误、Live Inspector 名不副实。策划/开发遇到「已连接但计时卡死」类问题时，只能翻服务端 stdout 或找 AI 挖日志。

本文档：

1. 盘点现状与不足（按严重度）
2. 给出分阶段补充方案与验收标准
3. 作为 `@frontend-dev` / `@backend-dev` 开发交接依据

---

## 1. 现状一览

| 层 | 路径 | 现状 |
|----|------|------|
| API | `server/src/admin.ts` | 路由齐全：鱼塘、metrics、timeline、config、privacy、SSE… |
| 桌面 | `admin-web/` | 5 Tab；**多处字段映射错误**；大半 API 无 UI |
| 手机 | `mobile/app/admin.tsx` | 能力更全（config / metrics / fish grid / 危险操作） |
| Fishing Debug | `fishingDebug.ts` | **读内存**，咬钩概率准；缺计时锚点字段 |
| Live Inspector | `liveSessionInspector.ts` | **读 metrics 猜状态**，与内存脱节；`pushLiveSession` 未使用 |

---

## 2. 不足分析（按严重度）

### P0 — 阻碍自助排障（本次事故直接暴露）

| ID | 不足 | 影响 | 证据 |
|----|------|------|------|
| A-01 | **Live Inspector 不是 Live** | 显示 `phase=null` / `fishingMs=0`，误导判断「没在钓」 | `liveSessionInspector.ts` 每秒 `getPlayerFishingTimeline`；`fishingMs = now - lastEvent.createdAt` |
| A-02 | **内存用户态无 Admin 读口** | 看不到 `fishingStartedAt` / `sessionFishingMs` / `disconnectedAt` / checkpoint | `PondUser` 仅在 socket snapshot；Fishing Debug 的 `activeFishers` 缺锚点字段 |
| A-03 | **无「服务进程重启」事件** | 无法区分「玩家断网」vs「开发杀进程 / tsx watch」 | shutdown 有日志，无 `fishing_metrics` / Admin 可见标记 |
| A-04 | **checkpoint 恢复不可见** | timeline 有 `joinKind` 但 UI 不强调；无「当前是否从 checkpoint 恢复、锚点是否为空」卡片 | 本次：`joinKind=checkpoint_restore` + `fishingStartedAt=null` |

### P1 — 工具半残 / 易踩坑

| ID | 不足 | 影响 |
|----|------|------|
| A-05 | admin-web **PondsPage 字段错位** | API 为 `{ pondId, summary.fishCount }`，UI 读 `name`/`fishCount` → 空白 |
| A-06 | admin-web **BusinessHealth 字段错位** | UI `date`/`catchCount`/`population` vs API `dateKey`/`totalCatch`/`avgPopulation` |
| A-07 | Fishing Debug **`activeFishers` 未渲染**（手机 grid / web） | 已有内存列表却看不到谁在钓、什么 phase |
| A-08 | **Bot 污染观感** | `/ponds` 有 `humanCount`/`botCount`，手机端未展示；Live Inspector `pondUsers` 含 Bot |
| A-09 | Timeline **缺高亮规则** | `disconnect`/`reconnect`/`disconnect_timeout`/`leave_pond`/`checkpoint_restore` 未按 SOP 着色归类 |
| A-10 | 大半 API **无 UI**：config（web）、logs、traces、client-logs、debug-sample、gray、privacy、daily |

### P2 — 闭环与体验

| ID | 不足 | 影响 |
|----|------|------|
| A-11 | `POST /api/client-logs` 有，**手机无上报器** | 客户端环缓冲无法进 Admin |
| A-12 | `pushLiveSession` 死代码 | SSE 只能轮询 DB，延迟/失真 |
| A-13 | 无「一键诊断包」 | 排障需手点多个 Tab + curl |
| A-14 | RBAC / Loki / Alert 仅 API | 运维入口缺失 |
| A-15 | correlation 时间线「前后 5 条」注释未实现 | 链路追踪弱 |

---

## 3. 目标用户与场景

| 角色 | 自助问题 | 期望入口 |
|------|----------|----------|
| 开发 | 计时为什么不走？刚是断网还是重启？ | Live 内存态 + Timeline 事件带 |
| 策划 | 昨日/近 7 日产量健康吗？ | Business Health（修好映射）+ 日报 |
| 运维 | 某玩家导出/擦除、告警测试 | Privacy + Alert（P2） |

**验收北极星**：策划/开发在 **不翻 stdout、不找 AI** 的前提下，能在 Admin 内确认：

> 「玩家在 waiting，但 `fishingStartedAt` 为空 → 计时卡死；因 `checkpoint_restore`（服务曾重启）」

---

## 4. 补充方案总览

```text
Admin v1.1 排障增强
├── A. 内存会话 API（权威 Live）
├── B. 重写 Live Inspector（读内存 + 辅 metrics）
├── C. Fishing Debug / activeFishers 补字段 + UI
├── D. Timeline 诊断着色 + 进程重启埋点
├── E. 修复 admin-web 映射错误
└── F.（P2）诊断包 / client-logs / 剩余 API 面板
```

---

## 5. 详细设计

### 5.1 A — 内存会话 API（P0）

**新接口**：

```http
GET /api/admin/players/:playerId/live-state
```

**响应（示意）**：

```json
{
  "playerId": "p_…",
  "found": true,
  "pondId": "pond-calm",
  "user": {
    "userId": "…",
    "nickname": "…",
    "spotId": "calm-spot-1",
    "status": "fishing",
    "fishingPhase": "waiting",
    "fishingStartedAt": null,
    "sessionFishingMs": 0,
    "todayFishingMs": 12345,
    "disconnectedAt": null,
    "phaseEndsAt": null,
    "isBot": false
  },
  "checkpoint": {
    "exists": true,
    "fishingPhase": "waiting",
    "spotId": "calm-spot-1",
    "disconnectedAt": null,
    "updatedAt": 1783831840046
  },
  "socketBound": true,
  "diagnostics": [
    {
      "id": "missing_fishing_started_at",
      "level": "error",
      "message": "status=fishing 但 fishingStartedAt=null，会话计时会卡死"
    }
  ],
  "server": {
    "startedAt": 1783831409875,
    "uptimeSec": 600,
    "pid": 12345
  }
}
```

**规则**：

- 在所有鱼塘 `listUsersInPond` 中按 `playerId` 查找（非 bot 优先）
- `diagnostics` 由服务端规则引擎生成（见 §5.4）
- `server.startedAt` 进程启动时写入全局（`index.ts`），用于对照「是否刚重启」

### 5.2 B — Live Inspector 重写（P0）

| 项 | 现状 | 目标 |
|----|------|------|
| 数据源 | `fishing_metrics` 1h 窗口 | **主：内存 live-state**；辅：最近 N 条 timeline |
| `phase` | lastEvent.payload | `user.fishingPhase` |
| `fishingMs` | now − lastEvent | `sessionFishingMs`（或 `now - fishingStartedAt`） |
| `pondUsers` | 全服含 Bot | **当前塘** human/bot 分计 |
| 推送 | 仅轮询 | 保留 1s 轮询读内存即可；可选在 `enrichPondUser` 广播时 `pushLiveSession` |

**admin-web UI**：结构化卡片，禁止只丢原始 JSON 行：

- 大字：Phase / SessionMs / StartedAt（空则红字告警）
- Checkpoint 摘要
- 最近 10 条关键事件（disconnect / reconnect / joinKind）

### 5.3 C — Fishing Debug 增强（P0/P1）

扩展 `activeFishers[]` 每项：

```ts
{
  userId, playerId?, nickname?, isBot,
  spotId, fishingPhase, phaseEndsAt,
  fishingStartedAt, sessionFishingMs, disconnectedAt,
  equippedBaitId, equippedTackleId
}
```

- **手机** `AdminPondFishDebugGrid`：增加「在钓玩家」表
- **admin-web** Fishing Debug：解析真实 `PondFishingDebugResponse`，渲染 spots + activeFishers（修 A-06 类型桩）

### 5.4 D — Timeline 诊断 + 重启埋点（P0）

**1）进程生命周期埋点**（写入 `fishing_metrics` 或独立 `server_events` 表，优先 metrics 以便进 timeline）：

| event_type | 时机 | payload |
|------------|------|---------|
| `server_start` | listen 成功 | `{ pid, startedAt, reason?: 'manual'\|'watch'\|'unknown' }` |
| `server_stop` | SIGINT/SIGTERM 优雅停机 | `{ pid, uptimeSec, reason }` |

强制杀进程可能没有 `server_stop` —— timeline 上出现「事件空洞 + 随后 `server_start` + `checkpoint_restore`」即判定重启。

**2）Timeline UI 规则（与挂机 SOP 对齐）**：

| 模式 | 条件 | 展示 |
|------|------|------|
| 短暂断线 | disconnect + reconnect，无 timeout | 黄 |
| 超时清场 | disconnect_timeout / spot_release | 红 |
| 主动离塘 | leave_pond | 灰 |
| 进程恢复 | server_start 后 checkpoint_restore | **紫/橙醒目** |
| 计时风险 | live-state diagnostics 命中 | 红 banner |

### 5.5 E — 修复 admin-web 映射（P0，半日）

| 页 | 修复 |
|----|------|
| PondsPage | 用 `pondId`、`summary.fishCount`、`humanCount`/`botCount`/`botRatio` |
| BusinessHealthPage | 用 `dateKey`、`totalCatch`、`avgPopulation`、`disconnectRate` |
| FishingDebugPage | 类型对齐 `PondFishingDebugResponse`，表格化 spots / activeFishers |
| api.ts | 删除错误 stub 类型，从 `@fish-social/shared` 引用 |

### 5.6 F — P2 扩展

| 项 | 说明 |
|----|------|
| 一键诊断包 | `GET /api/admin/players/:id/diag-pack` → zip/json：live-state + timeline(24h) + checkpoint + 最近 error_logs |
| client-logs 上报 | mobile `clientLogger` 在 `__DEV__` 或持有 admin key 时批量 POST |
| admin-web 新 Tab | Logs / Config / Gray / Privacy（只读优先） |
| 激活 `pushLiveSession` | 在 sessionTimer / phase 变更时推送，降低 SSE 轮询负载 |

---

## 6. 分期与工期

| 阶段 | 范围 | 交付 | 估时 |
|------|------|------|------|
| **MVP** | A-01~A-05 + E + C 字段 | live-state API · Inspector 重写 · Debug 补字段 · web 映射修复 · server_start 埋点 | 2~3d |
| **P1** | A-07~A-10 部分 | activeFishers UI · Timeline 着色 · Bot 分列 | 1~2d |
| **P2** | A-11~A-15 | 诊断包 · client-logs · 更多 Tab | 3~5d |

**并行建议**：后端 A/B/D 与前端 E/C UI 可并行；先修映射（E）当天见效。

---

## 7. 验收标准

### MVP

- [x] `GET /api/admin/players/:id/live-state` 返回真实内存 `fishingStartedAt` / `sessionFishingMs`
- [x] 复现「checkpoint 恢复后锚点为空」时，`diagnostics` 含 `missing_fishing_started_at`
- [x] Live Inspector 卡片与 live-state 一致（不再出现「在钓却 phase=null」误报）
- [x] Fishing Debug `activeFishers` 含锚点字段；至少一处 UI（web 或 mobile）可见
- [x] admin-web 鱼塘概览 / 业务健康数字正确显示
- [x] 进程启动后 timeline 可查到 `server_start`

### P1

- [x] Timeline 按 SOP 高亮 disconnect / checkpoint_restore
- [x] 鱼塘概览区分 human / bot

### P2

- [ ] 一键 diag-pack 下载
- [ ] 手机可上报 client-logs 并在 Admin 查询

---

## 8. 非目标（本方案不做）

- 替换运营日报（仍走 `docs/analytics/daily/`）
- 实时 Grafana 大盘（D-L2-05+）
- 修改游戏玩法逻辑（**计时锚点 bug 修复**另开 BUG 单，可与本方案同迭代）

**建议并行 BUG**：`applyCheckpointToUser` / `resumeAfterReconnect` 在 `status=fishing && fishingStartedAt==null` 时补锚点（见 2026-07-12 事故结论）。

---

## 9. 开发交接摘要

**开发提示词**：[`docs/planning/prompts/admin-observability-v1.1-dev.prompt.md`](../prompts/admin-observability-v1.1-dev.prompt.md)

**推荐顺序**：

1. 全局 `SERVER_STARTED_AT` + `server_start` 埋点  
2. `GET .../live-state` + diagnostics  
3. 重写 `liveSessionInspector` 读 live-state  
4. 扩展 `fishingDebug.activeFishers`  
5. 修复 admin-web 三页映射  
6. Timeline / Debug UI 渲染  
7. **并行 BUG**：checkpoint 恢复补 `fishingStartedAt`

**提示词**：已就绪，见上链。

---

## 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-12 | MVP 实现：live-state · Inspector 读内存 · server_start/stop · 锚点修复 · admin-web 映射 · `verify:admin-observability` |
| 2026-07-12 | 初稿：基于垂钓计时卡死排查 + Admin 全量盘点 |
