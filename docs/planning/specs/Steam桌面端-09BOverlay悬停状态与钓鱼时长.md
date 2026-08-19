# STEAM-DESKTOP-09B：Overlay 悬停状态与钓鱼时长

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-09B` |
| 类型 | 功能 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P1 |
| 设计时间 | **2026-08-19** |
| 完成时间 | **2026-08-20** |
| 依赖 | 07C（同塘玩家）、PERF-03b（`session_timer_tick`） |
| 前置参考 | [`鱼塘场景与社交列表UI优化.md`](./鱼塘场景与社交列表UI优化.md)（**FEAT-UI-1** · Web 悬停气泡） |

---

## 1. 背景与目标

### 1.1 背景

Steam Overlay（07C）当前对同塘玩家在昵称下**常驻** `_stateLabel` 文字，且无 `sessionFishingMs` / 上钩倒计时字段。

**2026-08-20 产品修订（`STEAM-DESKTOP-09D`）**：Steam Overlay **不**沿用 Web FEAT-UI-1「他人默认隐藏状态」策略。状态 icon 与上钩圆环由 **09D 默认展示**；**本需求（09B）仅负责 IPC 时长字段 + 悬停 Tooltip 显示钓鱼时长**。

### 1.2 目标

| # | 目标 |
|---|------|
| G1 | Unity → Overlay DTO 携带 `fishingPhase` · `sessionFishingMs` · `hookDeadlineMs` |
| G2 | 鼠标悬停塘内玩家 ≥300ms 显示 Tooltip：**仅**本局钓鱼时长或收杆剩余时长 |
| G3 | 悬停 **不**重复展示状态 icon/圆环（默认表现见 09D） |
| G4 | 时长与 Unity 权威一致，随 `session_timer_tick` 更新 |

### 1.3 非目标

- 默认状态 icon / 上钩圆环 UI（见 **STEAM-DESKTOP-09D**）
- 不改服务端计时逻辑或 PERF-03b 广播字段
- 不做触屏长按（Steam 桌面首版仅鼠标悬停）
- 不在 Overlay 显示「今日累计 8h」额度（仍主窗口/左上状态胶囊）

---

## 2. 用户与场景

| 角色 | 场景 | 期望 |
|------|------|------|
| 挂机玩家 | 看同塘多人 | 默认见昵称+状态+圆环（09D）；悬停才见时长 |
| 挂机玩家 | 鼠标移到玩家上 | 300ms 后出现「本局 12:34」或「收杆 0:08」 |
| 挂机玩家 | 移出热区 | Tooltip 立即消失 |

---

## 3. 功能范围

### 3.1 展示规则

**悬停 Tooltip（全体塘内用户，含自己与他人）**

- 默认：状态/圆环由 **09D** 渲染；本需求 **不**在 Tooltip 重复状态 icon。
- 悬停 ≥300ms：Tooltip **仅**时长文案
  - **上钩**：剩余收杆倒计时（`hookDeadlineMs`）
  - **钓鱼中**：本局 `sessionFishingMs` 格式化
  - **idle / 未钓**：不显示 Tooltip 或「未在钓」
- 移出：Tooltip 消失

### 3.2 IPC 状态扩展（Unity → Overlay）

扩展 `NativeOverlayActorDto`（及 own 字段若需）：

| 字段 | 类型 | 说明 |
|------|------|------|
| `fishingPhase` | string | 权威相位：`idle`/`waiting`/`hooked`/… |
| `sessionFishingMs` | long | 本局累计垂钓毫秒（PERF-03b tick 对齐） |
| `hookDeadlineMs` | long? | 上钩收杆截止 UTC ms；无则 0 |

Overlay **只渲染**，本地不算时长；收到新 `sequence` 全量覆盖。

### 3.3 Overlay 实现要点

- `OverlayPetActor`：`MouseEnter`/`MouseLeave` + 300ms `DispatcherTimer`
- Tooltip：WPF `ToolTip` 或自绘 Popup，Z-order 高于角色层
- 自己与他人分支渲染；拖动场景时不误触 Tooltip

---

## 4. 技术影响

### 4.1 涉及文件（预估）

- `fish-social-unity/.../NativeOverlayStateDto.cs`
- `fish-social-unity/.../OverlayPondStateBuilder.cs` — 从 `PondUserDto` / tick 填时长
- `desktop-overlay/OverlayPetActor.cs`
- `desktop-overlay/PondScenePresenter.cs`
- `desktop-overlay/IpcProtocol.cs` — 反序列化新字段

### 4.2 API / Socket

无新 REST。复用：

- `pond_snapshot` / `pond_user_updated` 中的 `fishingPhase`
- `session_timer_tick` 的 `sessionFishingMs`（Unity 侧合并进 DTO）

---

## 5. 验收标准

- [x] DTO 含 `fishingPhase` · `sessionFishingMs` · `hookDeadlineMs`（own + users）
- [x] 悬停 ≥300ms **仅**显示时长/收杆剩余；移出消失
- [x] Tooltip 不重复 09D 默认状态 icon/圆环
- [x] 时长随 tick 更新，不出现 BUG-13 式「0 秒闪烁」（`PondUserMerge` + tick 守卫）
- [x] 高人数鱼塘（≥10）悬停仍流畅，无 Tooltip 残留（Popup + 拖动时 `CancelTooltip`）

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| tick 与 snapshot 不同步 | Unity 合并层以 snapshot 相位为准，tick 只更新 ms |
| Tooltip 被角色遮挡 | 统一最高层 Popup |
| IPC 字段增多 | 可选字段默认值；旧 Overlay exe 忽略 |

---

## 7. 开发交接

**提示词**：[`docs/planning/prompts/steam-desktop-09b-overlay-hover-status-dev.prompt.md`](../prompts/steam-desktop-09b-overlay-hover-status-dev.prompt.md)

---

## 8. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-08-19 | 初稿：Overlay 他人悬停 Tooltip；IPC 扩展 session/phase；对齐 FEAT-UI-1 |
| 2026-08-20 | 修订：默认状态/圆环移交 09D；09B 收窄为 IPC + 悬停仅时长 |
| 2026-08-20 | 用户验收通过 → **已实现**；修复悬停双卡片残留与 Bot 钓鱼时长计时异常 |
