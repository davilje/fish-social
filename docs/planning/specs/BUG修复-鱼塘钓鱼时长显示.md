# BUG 修复：鱼塘钓鱼时长显示与计时

| 状态 | **已实现** | 目标版本 v0.2.5 |
|------|------------|-----------------|
| 优先级 | P0 | 体验阻断 |
| 关联 | [`状态机需求描述.md`](./状态机需求描述.md) · [`数值重构v2-成长咬钩与文案.md`](./数值重构v2-成长咬钩与文案.md) §4.6 |

---

## 1. 问题概述

玩家在鱼塘场景中，角色头顶的钓鱼状态条存在三类问题：

1. **收杆后再次钓鱼，时长未归零**（用户主诉）
2. **收杆倒计时是否准确**（需产品定义 + 技术对齐）
3. **时间文字框过窄**，无法完整展示最长格式（如 `23小时59分59秒`）

---

## 2. 现状与根因（供开发对照）

### 2.1 两套「时长」混用

当前系统同时存在两种时长语义，但 **UI 未区分**：

| 语义 | 字段 / 计算 | 用途 | 当前展示位置 |
|------|-------------|------|--------------|
| **今日累计钓鱼时长** | `todayFishingMs`（持久化 `daily_fishing` 表） | 每日 8 小时上限 (`MAX_DAILY_FISHING_MS`) | 角色头顶 badge **误用**；底栏「今日剩余」正确 |
| **本次垂钓会话时长** | `fishingStartedAt` → `now - fishingStartedAt`（未单独字段） | 玩家感知「我这轮钓了多久」 | **未展示**；应用作头顶 badge |

**角色头顶**（`PondCharacter.tsx`）当前文案：

```text
{阶段标签} · {formatDuration(user.todayFishingMs)}
```

即展示的是 **今日累计**，而非本次会话。玩家「收起鱼竿 → 再次开始钓鱼」后，今日累计必然 **不应归零**（否则破坏每日上限），因此用户感到「没收杆归零」——实为 **展示字段选错**。

### 2.2 「收杆重新钓鱼」的两种理解

| 场景 | 用户预期 | 当前行为 |
|------|----------|----------|
| A. 点击「收起鱼竿」→ 回到 `seated` → 再点「开始钓鱼」 | 头顶时长从 **0 秒** 重新计 | 仍显示今日累计，不归零 |
| B. 一轮 `resolving`（收杆动画）结束 → 自动 `baiting` 循环下一杆 | 部分玩家期望每杆单独计时 | `fishingStartedAt` 在首次 `beginFishingSequence` 时设定，**循环装饵不重置** |

**产品决策（本 spec）**：采用 **会话级** 计时（场景 A），**不**按每杆 resolving 归零（场景 B）。头顶 badge 表示「自本次点击开始钓鱼起，至收起鱼竿止」的连续时长。

### 2.3 收杆倒计时（`hooked` 阶段）

当前 `hooked` 展示：

```text
上钩 · 预计收杆 {formatDuration(hookRemainingMs)}
```

- `hookRemainingMs = phaseEndsAt - now`，客户端每秒 `setInterval` 刷新
- 服务端在咬钩时：`phaseEndsAt = now + hookDurationMs`（`fishingStateMachine` / `calcHookDurationMs`）
- 满尺寸鱼：`hookDurationMs = 7_200_000`（2 小时）；非满尺寸：2s ~ 10min（A0-v2）

**潜在不准因素**（需修复或验收说明）：

| 项 | 说明 |
|----|------|
| 客户端 1s 步进 | 显示最多落后服务端约 1 秒，可接受；应用 `phaseEndsAt` 校正，不用本地累加 |
| `formatDuration` 丢秒 | `h > 0` 时仅输出 `X时Y分`，**不含秒**；与产品要求的完整格式不符 |
| 广播覆盖不全 | 服务端每秒广播时长仅包含 `waiting` / `hooked` 等部分 phase，`baiting` / `casting` / `resolving` 期间头顶会话计时可能 **停跳**（`index.ts` 过滤条件过窄） |
| 停止钓鱼未落库 | `handleStopFishing` → `stopping` → `seated` **未调用** `addTodayFishingMs`，可能导致今日累计 **少计**，进而「今日剩余」偏多（与头顶展示无关，但属同时长相关缺陷） |

### 2.4 展示宽度

- `PondCharacter` 容器 `width: 72`，`fishingBadge` 无 `minWidth`，`fishingText` 字号 **8px**
- 最长文案示例：`上钩 · 预计收杆 23小时59分59秒`（约 18+ 字符）
- 当前极易换行、截断或挤成不可读

---

## 3. 产品需求

### 3.1 时长语义拆分（必须）

| 位置 | 展示内容 | 格式 | 归零规则 |
|------|----------|------|----------|
| **角色头顶 badge**（钓鱼进行中） | **本次会话垂钓时长** | 见 §3.3 | 进入 `seated` / `idle` 后 **隐藏**；下次 `beginFishingSequence` 从 **0** 起计 |
| **角色头顶 badge**（`hooked`） | **预计收杆剩余** | 见 §3.3 | 咬钩瞬间 = `hookDurationMs`；随 `phaseEndsAt` 递减至 0 |
| **鱼塘底栏**（`pond/[id].tsx`） | **今日剩余可钓时长** | 可与头顶相同格式化函数 | 每日 UTC+8 0 点重置（沿用现有 `daily_fishing`） |

**禁止**：头顶 badge 再直接绑定 `todayFishingMs`。

### 3.2 会话时长计算规则

**服务端权威**，客户端仅展示：

```text
sessionFishingMs =
  status === 'fishing' && fishingStartedAt != null
    ? now - fishingStartedAt
    : 0
```

- `fishingStartedAt` 在 `beginFishingSequence` 时设为 `now()`（已有）
- 进入 `seated` / `idle` 时置 `null`（`syncStatus` 已有）
- **循环装饵**（`resolving → baiting`）不重置 `fishingStartedAt`（一会话多杆）
- 广播 `pond_user_updated` 时，建议下发 **`sessionFishingMs`**（或在 snapshot 内统一用 `computeSessionFishingMs`），避免客户端自行累加漂移

**今日累计落库**（修复缺陷，与展示拆分并行）：

在以下时机将 `now - fishingStartedAt` 累加进 `addTodayFishingMs` 并重置锚点（或等价逻辑）：

- `stopping` → `seated` 完成时
- `hooked` 阶段 `stop_fishing` 视为脱钩并回到 `seated` 时
- `leavePond` / 断线超时移除（已有部分逻辑，需对齐）

确保底栏「今日剩余」与数据库一致。

### 3.3 时间格式化（必须）

统一函数（建议 `formatFishingDuration(ms: number): string`），**全项目**钓鱼相关展示复用（头顶、底栏、上钩倒计时、生态恢复倒计时等可评估是否同函数）。

| 条件 | 输出示例 |
|------|----------|
| 仅秒（&lt; 1 分钟） | `45秒` |
| 分秒（&lt; 1 小时） | `12分34秒` |
| 时分秒（≥ 1 小时） | `2小时15分30秒` |
| 边界最大值 | `23小时59分59秒`（UI 必须完整容纳） |

规则：

- 始终包含 **秒**（除非产品后续明确要求长时长隐藏秒——**当前需求为含秒**）
- 不使用 `2时0分` 这种缺秒简写
- 单数字不加前导零（`2小时5分3秒`，非 `02:05:03`）

### 3.4 角色头顶 UI（必须）

**布局**：

- badge **最小宽度**能单行容纳 `23小时59分59秒` 及前缀（见下）
- 建议 `minWidth` ≥ **148px**（Web 实测可调），或改为 **气泡在角色上方居中、宽度自适应内容**，不受角色 `72px` 躯干宽度限制
- 字号不低于 **10px**（当前 8px 过小）；行高保证可读
- 超长时 **优先扩宽 badge**，不要 `numberOfLines={1}` 截断核心数字

**文案模板**：

| 阶段 | 模板 |
|------|------|
| `baiting` / `casting` / `waiting` / `resolving` / `stopping` | `{阶段短标签} · {sessionFishingMs}` |
| `hooked` | `上钩 · 预计收杆 {hookRemainingMs}` |

阶段短标签与现有一致：装饵中 / 抛竿 / 垂钓中 / 收竿 等。

### 3.5 收杆倒计时准确性（验收标准）

| # | 验收项 |
|---|--------|
| H1 | 咬钩瞬间，头顶显示剩余时间 = `hookDurationMs` 格式化值（误差 &lt; 1s） |
| H2 | 倒计时每秒递减，与 `phaseEndsAt` 一致；到 0 时进入 `resolving`，不卡最后一秒 |
| H3 | 满尺寸鱼（2h）显示 `2小时0分0秒` 或 `1小时59分59秒` 量级正确，**不被截断** |
| H4 | 弱网下重连后，以服务端 `phaseEndsAt` 校正，不出现倒计时回跳 &gt; 3s |
| H5 | `hooked` 期间点击「收起鱼竿」→ 按状态机视为脱钩，倒计时停止，不继续走表 |

### 3.6 会话计时广播（必须）

每秒广播（或 snapshot 推送）须覆盖 **所有计入会话的 phase**：

`baiting` · `casting` · `waiting` · `hooked` · `resolving` · `stopping`

避免装饵/收杆动画阶段头顶时间 **冻结**。

---

## 4. 涉及文件（开发参考）

| 层级 | 文件 |
|------|------|
| 展示 | `mobile/components/PondCharacter.tsx` |
| 格式化 | `mobile/lib/config.ts`（或抽 `formatFishingDuration.ts`） |
| 底栏 | `mobile/app/pond/[id].tsx` |
| 客户端状态 | `mobile/lib/usePondSocket.ts`（demo 模式会话计时对齐） |
| 类型 | `shared/types.ts`（可选增加 `sessionFishingMs` 只读字段） |
| 服务端 | `server/src/gameState.ts`（`computeFishingDuration` 拆分会话/今日） |
| 服务端 | `server/src/fishingStateMachine.ts`（停止时落库） |
| 服务端 | `server/src/index.ts`（每秒广播 phase 过滤条件） |

---

## 5. 不做

- 不改每日 8 小时上限规则
- 不顺带改在线栏 / 聊天栏（已在 `BUG修复-资料与鱼塘UI.md` 处理）
- 不改 `hookDurationMs` 数值公式（属 A0-v2，另单）
- 不做独立「钓鱼统计」页

---

## 6. 验收清单

- [x] 收起鱼竿回到 `seated` 后，头顶 badge **消失**或不再显示会话时长
- [x] 再次「开始钓鱼」，头顶从 **0 秒**（或 `1秒`）递增，**不**承接上次会话读数
- [x] 同一会话内循环装饵（中鱼/空杆后自动下一杆），会话时长 **连续累计**，不归零
- [x] 底栏「今日剩余」仍反映 **今日累计**，且收起鱼竿后今日剩余 **减少**（落库正确）
- [x] `hooked` 倒计时与收杆窗口一致，满 2h 鱼种格式完整可读
- [x] 头顶 badge 在最长文案下 **单行完整可见**（Web + 移动端各测一档窄屏）
- [x] 他人视角同塘可见同一会话时长/倒计时（服务端广播一致）

---

## 7. 开发交接摘要

```
修复鱼塘钓鱼时长三类问题（v0.2.5）：
1. 头顶 badge 改展示 sessionFishingMs（本次会话），勿用 todayFishingMs
2. 停止钓鱼时 addTodayFishingMs 落库；广播覆盖 baiting~stopping 全阶段
3. formatFishingDuration 支持 X小时Y分Z秒；PondCharacter badge 加宽/自适应

详见 docs/planning/specs/BUG修复-鱼塘钓鱼时长显示.md
```

---

*策划完成，请交开发 Agent 实现，策划不直接改代码。*

| 日期 | 变更 |
|------|------|
| 2026-07-03 | 开发实现：sessionFishingMs、formatFishingDuration、落库与全阶段广播 |
| 2026-07-03 | 初稿：会话/今日拆分、格式化、UI 宽度、收杆倒计时验收 |
