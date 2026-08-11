# BUG 修复：垂钓中头顶反复显示「0秒」

| 字段 | 内容 |
|------|------|
| 状态 | **已实现** |
| 编号 | **BUG-13** |
| 优先级 | **P0** |
| 目标版本 | hotfix / v0.6.x |
| 设计时间 | **2026-07-15** |
| 范围 | 服务端 Bot 广播 · 客户端 `pond_user_updated` 合并 · 会话计时插值条件 |
| 关联 | [`BUG修复-会话计时广播回归.md`](./BUG修复-会话计时广播回归.md)（BUG-07）· [`会话计时tick仅必要字段.md`](./会话计时tick仅必要字段.md)（PERF-03b）· [`BUG修复-鱼塘钓鱼时长显示.md`](./BUG修复-鱼塘钓鱼时长显示.md)（BUG-03） |
| 来源 Agent | 策划 |
| 目标开发 Agent | 前端开发 + 后端开发 |

---

## 1. 问题记录

### 1.1 现象

鱼塘内钓鱼过程中，**每个角色**（含真人与 Bot）头顶状态条反复刷新，文案长期为：

```text
垂钓中 · 0秒
```

计时不随真实垂钓时间递增；有时在「0秒」与短暂正确秒数之间闪烁。

### 1.2 复现

1. 进入任意鱼塘，开始钓鱼进入 `waiting`（或观察塘内已在钓的 Bot）  
2. 注视角色头顶 badge 约 5～10 秒  
3. **预期**：`垂钓中 · 1秒` → `2秒` → … 递增  
4. **实际**：反复刷新且多为 `垂钓中 · 0秒`

### 1.3 影响

- 会话时长展示阻断（BUG-03 产品语义回退）  
- 塘内多人/多 Bot 时观感像「全员计时坏了」  
- 与 PERF-03b / BUG-07 修复后的秒表契约冲突，易被误判为服务端又卡死

---

## 2. 根因分析

### 2.1 展示链路

| 层 | 行为 |
|----|------|
| UI | `PondCharacter`：`fishingPhase===waiting` → 文案「垂钓中」；时长用 `sessionFishingMs ?? 0` |
| 格式化 | `formatFishingDuration`：`floor(ms/1000)` → **0～999ms 一律「0秒」** |
| 刷新 | 每秒 `session_timer_tick` + 客户端插值 `setUsers` → badge **每秒重绘**（故称「反复刷新」） |

因此：**秒表权威值长期 &lt;1s 或被反复冲成 0/缺失**，就会稳定复现本 bug。

### 2.2 服务端秒表公式

```ts
// pondUserManager.computeSessionFishingMs
status === 'fishing' && fishingStartedAt != null
  ? now - fishingStartedAt
  : 0
```

`session_timer_tick` 每秒广播该值。若 `fishingStartedAt === null` 或 `status !== 'fishing'`，tick 恒为 **0**。  
UI「垂钓中」只看 **phase**，不看 status/锚点 → **相位对、秒表 0**。

### 2.3 客户端整对象替换（BUG-07 同类面）

```ts
// usePondSocket.ts
socket.on('pond_user_updated', (user) => {
  setUsers((prev) => prev.map((u) => (u.id === user.id ? user : u))); // 整份替换
});
```

若 payload **缺 `sessionFishingMs`**（或为 0），头顶立刻变「0秒」。下一秒 tick 若带回正确值 → **闪烁**；若 tick 也是 0 → **钉死 0秒 + 每秒刷新**。

PERF-03b 后 tick **不再携带 `fishingStartedAt`**，本地插值完全依赖本地是否还留着锚点；一旦被整份替换冲掉，无法从 tick 恢复锚点。

### 2.4 Bot 广播未 enrich（已定位代码）

```ts
// bots.ts 开钓
io.to(pond.id).emit('pond_user_updated', {
  ...result.user,
  todayFishingMs: result.user.todayFishingMs,
}); // 无 enrichPondUser → 常无 sessionFishingMs
```

`stopBotFishing` 路径同样可能直接 emit 原始 `user`。塘内 Bot 多时放大「全员 0秒」观感。

### 2.5 条件不一致（加重）

| 用途 | 条件 |
|------|------|
| Badge 显示「垂钓中」 | `fishingPhase ∈ SESSION_PHASES` |
| 服务端/插值算秒表 | `status==='fishing' && fishingStartedAt!=null`（插值另要求 phase∈列表） |

phase 与 status/锚点短暂不一致时，会出现「垂钓中 · 0秒」。

---

## 3. 修复方案

### 3.1 原则

1. **在钓相位**下，`fishingStartedAt` 必须有值（缺则 `ensureFishingStartedAt`）  
2. 一切对外 `pond_user_updated` **必须** `enrichPondUser`（含 Bot）  
3. 客户端对 `pond_user_updated` **合并**，禁止在钓时用「缺字段」冲掉秒表/锚点  
4. 不恢复每秒全量 `pond_user_updated` 秒表（维持 PERF-03b）；秒表仍以 `session_timer_tick` + 插值为主  

### 3.2 服务端（P0）

| # | 改动 | 文件 |
|---|------|------|
| S1 | Bot 开钓/停钓等所有 `pond_user_updated` 改为 `enrichPondUser(user)` | `bots.ts` |
| S2 | 审计：禁止裸 emit 未 enrich 的 `PondUser`（可抽 `emitPondUserUpdated(io, pondId, user)`） | `bots.ts` / 可选 `pondUserManager` |
| S3 | `waiting`（及 SESSION_TIMER_PHASES）进入/`session_timer` 广播前：若在钓相位则 `ensureFishingStartedAt` | `fishingStateMachine` / `serverLoops` enrich 前 |
| S4 | （可选）`computeSessionFishingMs` 对「phase 在钓但 status 未同步」先 `syncStatus`/`ensureFishingStartedAt` 再算 | `pondUserManager.ts` |

### 3.3 客户端（P0）

| # | 改动 | 文件 |
|---|------|------|
| C1 | `pond_user_updated`：**按字段合并**。若新包 `sessionFishingMs == null` 且本地在钓相位，**保留旧 `sessionFishingMs`**；若新包缺 `fishingStartedAt`（`undefined`）且本地有锚点且仍在钓，**保留旧锚点**；显式 `null` 且 phase 已非在钓则接受 | `usePondSocket.ts` |
| C2 | 插值条件与 badge 对齐：用 `isFishingActive(fishingPhase)` + `fishingStartedAt != null`，不要仅依赖 `status==='fishing'` | `usePondSocket.ts` |
| C3 | （可选）`session_timer_tick` 合并时若 `sessionFishingMs` 递增而本地锚点缺失，可用 `now - sessionFishingMs` **反推锚点**（仅校准，不替代服务端权威） | `usePondSocket.ts` |

### 3.4 验证（P0）

扩展 `verify:session-timer-broadcast` 或新增 `verify:session-timer-zero-flash`：

1. 注入 waiting 用户，`enrich` 两次间隔 ≥1s，`sessionFishingMs` 严格递增  
2. 源码守卫：`bots.ts` 中 `pond_user_updated` 必须伴随 `enrichPondUser`  
3. 源码守卫：`usePondSocket` 中 `pond_user_updated` 不得再是单纯 `? user : u` 整替换（须含 merge/保留逻辑）  
4. 手测：真人 waiting 10s 头顶 ≥9s；塘内 Bot 头顶非长期「0秒」

### 3.5 非目标

- 不改每日 8h `todayFishingMs` 口径  
- 不把秒表重新塞回每秒全量 `pond_user_updated`（PERF-03b 回退）  
- 不改咬钩/状态机玩法  

---

## 4. 验收标准

- [x] 真人进入 `waiting` 后 10s 内，头顶从 `0秒` 递增到至少 `9秒`（允许 ±1s）  
- [x] 同塘 Bot 在钓时头顶不为长期「垂钓中 · 0秒」  
- [x] 相位切换（baiting→casting→waiting）不出现秒表被打回 0 后卡住  
- [x] `npm run verify:session-timer-broadcast`（或扩展脚本）通过  
- [x] 无恢复「每秒全量 pond_user_updated 刷秒表」

---

## 5. 风险与回归

| 风险 | 缓解 |
|------|------|
| 合并策略误保留已收竿用户的旧秒表 | 仅当 `isFishingActive(新 phase)` 时保留；收竿 phase 接受 0/null |
| 反推锚点与服务器时钟漂 | C3 可选；默认以 enrich + tick 为准 |
| Bot enrich 增加计算 | 仅事件路径，非 1s 全塘；可接受 |

---

## 6. 开发交接

**提示词**：[`docs/planning/prompts/bugfix-session-timer-zero-flash-dev.prompt.md`](../prompts/bugfix-session-timer-zero-flash-dev.prompt.md)

**建议顺序**：S1/S2 → C1/C2 → S3 → 扩展 verify → 手测。

---

## 7. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-15 | 初稿：现象 / 根因（整替换 · Bot 未 enrich · PERF-03b · 0秒格式化）· 修复方案 BUG-13 |
| 2026-07-15 | **已实现**：`emitPondUserUpdated` · Bot enrich · enrich 补锚点 · 客户端 merge/插值 · `verify:session-timer-broadcast` |
