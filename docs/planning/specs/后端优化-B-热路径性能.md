# 后端优化 B — 热路径性能

| 字段 | 内容 |
|------|------|
| 功能名称 | 生态广播瘦身 · 会话计时轻量事件 · 咬钩权重缓存 |
| 编号 | **BE-OPT-B** |
| 子项 | PERF-01～PERF-05 |
| 状态 | **已实现** |
| 设计时间 | **2026-07-12** |
| 完成时间 | **2026-07-12** |
| 优先级 | **P1** |
| 工期估 | 2～3 人天 |
| 前置 | 建议 BE-OPT-A 完成后；可与 A 尾部并行（文件冲突少） |
| 总表 | [`后端优化-问题汇总与分批计划.md`](./后端优化-问题汇总与分批计划.md) |
| 关联 | BUG-07 会话计时广播回归 · R2-1 增量广播 |
| 开发提示词 | [`backend-opt-b-perf-dev.prompt.md`](../prompts/backend-opt-b-perf-dev.prompt.md) |

---

## 1. 背景与目标

### 1.1 背景

R2-1「增量广播」文档标已实现，但代码仍：

- 生态 tick 调用全量 `buildSnapshot` 只取 `ecology`
- 会话计时每秒对在钓用户发完整 `pond_user_updated`（BUG-07 刻意保留全量用户对象以保证 `sessionFishingMs`）
- `consumeDirtyUsers` 闲置

### 1.2 目标

1. 生态更新 **零** 全量用户快照构建
2. 会话时长同步带宽显著下降（专用轻量事件或等价方案）
3. 不回归 BUG-07（waiting 阶段时长不卡死）
4. 咬钩路径减少重复 spot weight 查询

### 1.3 非目标

- 多机 / Redis
- 改咬钩公式
- 强行把 session timer 接回 `consumeDirtyUsers` 而不验证时长字段

---

## 2. 范围与验收

| # | 子项 | 改动要点 | 验收 |
|---|------|----------|------|
| 1 | PERF-01 | `serverLoops` 生态分支改为 `getPondEcologySummary(pondId)`（或等价）后 `emit('pond_ecology_updated', …)` | 生态 tick 路径无 `buildSnapshot`；塘内客户端生态条仍更新 |
| 2 | PERF-02 | 缩短事务：按塘或批量 UPDATE；避免单事务锁全库过久 | 同规模下 `ecology_tick_duration_ms` 不恶化或改善；无死锁 |
| 3 | PERF-03 | 新增专用事件（建议 `session_timer_tick`：`{ userId, sessionFishingMs, fishingPhase? }`）或压缩 payload；**客户端同步改订阅** | `verify:session-timer-broadcast` 绿；Modal 打开时长仍走 |
| 4 | PERF-04 | 文档化 dirty API 用途：非 timer 的用户字段变更走 dirty；timer 走轻量事件。删除死代码或接线，二选一写进 PR 说明 | 无「导出但零引用」残留（或注明 `@internal reserved`） |
| 5 | PERF-05 | 每塘 spot weight 内存缓存，生态 tick 刷新 | 咬钩路径同塘多次调用不重复 `all()` |

### 2.1 客户端契约

| 事件 | 变更 |
|------|------|
| `pond_ecology_updated` | payload 形状保持；仅服务端构造方式变 |
| `session_timer_tick`（新）或保留旧事件 | 若新事件：`mobile/` 必须合并 `sessionFishingMs` 到本地用户态；旧全量 `pond_user_updated` 可降频或仅用于状态变化 |

**硬约束**：不得只为省带宽而再次引入「dirty 合并用户对象却丢时长」的 BUG-07。

---

## 3. 技术影响

- `server/src/serverLoops.ts`
- `server/src/pondEcology.ts`
- `server/src/pondUserManager.ts`
- `server/src/fishingSession.ts`（或 bite 调用方）
- `shared/` 事件类型（若新事件）
- `mobile/` 鱼塘订阅
- `scripts/verify-session-timer-broadcast.ts`（扩展）

---

## 4. 验收清单

- [x] PERF-01～05 完成
- [x] `verify:session-timer-broadcast` · `verify:server-boot` 绿
- [ ] 手工：进塘钓鱼，时长 badge / 侧栏秒数连续走
- [x] 计划表 BE-OPT-B → 已实现

---

## 5. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-12 | 已实现：PERF-01 生态 summary emit；PERF-02 按塘事务；PERF-03 `session_timer_tick`；PERF-04 dirty 仅 drain；PERF-05 spot weight 缓存 |
| 2026-07-12 | 已确认；PERF-01～05 |
