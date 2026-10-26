# Bug / 架构：自己钓鱼表现由 Unity 本地推进（Overlay 变瘦）

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Unity OwnFishingPresenter：自己钓鱼表现权威 |
| 编号 | **BUG-54** |
| 类型 | **Bug修复**（含桌面架构收敛） |
| 负责人 | Unity 桌面（`fish-social-unity/Assets/Scripts/Desktop/`）+ WPF Overlay（`desktop-overlay/`） |
| 状态 | **已实现** |
| 目标版本 | hotfix |
| 优先级 | **P0** |
| 设计时间 | **2026-10-18** |
| 完成时间 | **2026-10-18** |
| 关联 | BUG-52（state 合并/限频）、BUG-53（Overlay 侧预测 hotfix，过渡方案）；`docs/策划/规格/缺陷/BUG修复-OverlayHUD布局与交互.md` |

计划表状态以 `scripts/策划/build-master-plan-xlsx.py` 的 `ENSURE_PLAN_ROWS` 为准。

---

## 1. 背景与目标

### 1.1 背景

Overlay 开始钓鱼 / 收杆 / 打窝 / 离席 / 上钩的手感问题，证据表明：

- Socket 回执曾因 Unity 主线程同步上下文排队 **1.8～8s**（BUG-53 已将收发移到线程池，RTT 可降到数十 ms）。
- 即便 RTT 正常，**装饵→抛竿→钓鱼中**、**收杆后按钮出现**仍不跟手，因为表现跟着服务端逐步 `pond_user_updated`，而按钮又依赖 `availableActions`。
- BUG-53 在 Overlay 做了本地预测与时间线，能救急，但形成 **Overlay / Unity / Server 三套节奏**，连点、中间态被清掉、按钮与动画不同步会反复出现。

已确认的终态方案：**自己的钓鱼表现由 Unity 本地推进；服务端只做校验与结算；Overlay 只渲染与转发点击。**

### 1.2 目标

- 点击后 **P50 ≤ 50ms / P95 ≤ 100ms** 出现正确动画与按钮态（本机、Overlay 已显示）。
- 装饵→抛竿→等待、收杆→入座由 Unity 按与服务端一致的时长本地走完，中间不卡顿。
- 每次有效操作仍发服务端；失败可回滚并提示。
- 拆掉 Overlay 侧猜 phase / 猜 800·600ms 时间线（BUG-53 过渡逻辑退役）。

### 1.3 非目标

- 不改 `mobile/`、`server/`、`shared/` 业务逻辑（本项 P0）。
- 不改服务端广播频率、不改咬钩随机与鱼获结算权威。
- 不做「不发服务器的纯单机钓鱼」。
- 不重做他人猫的同步模型（他人仍跟服务器）。
- 不把「提高帧率 / 重启进程」写成修复。

---

## 2. 现象与复现

| 环境 | 记录 |
|------|------|
| 平台 | Windows，Unity 桌面 + WPF Overlay named pipe |
| 构建 | 以当前工作区 Debug 包为准；验收前需重打 Unity 并重启 Overlay |
| 复现 | 落座后按钮慢；开始/收杆/打窝/离席可连点重播；装饵→钓鱼中间有空窗；收杆动画后按钮晚出现 — **稳定复现**（BUG-53 预测后仍存在架构层问题） |

| 操作 | 预期 | 实际（架构问题） |
|------|------|------------------|
| 落座 | 立刻出现开始钓鱼/打窝/离席 | 等 `AvailableActions` / OwnSpotId 回传 |
| 开始钓鱼 | 立刻装饵，再平滑抛竿→等待 | 等服务器逐步推 phase，或 Overlay 与服务器互相覆盖 |
| 收杆 | 立刻收线，结束后立刻回入座按钮 | 等 stopping→seated 回传 |
| 连点同一按钮 | 忽略第二次 | 可能重复播动画 / 重复发包 |

---

## 3. 时间戳证据（链路）

统一 UTC Unix ms；关联 `commandId` / socket ack id。

```text
Overlay command_sent
Unity overlay_command_received (ipcMs)
Unity local_presentation_applied   ← BUG-54 新增（表现立刻推 Overlay）
Socket event_sent
Socket ack_received
Unity reconcile (ok/fail)
Overlay state_applied (phase/visual/actions)
```

### 3.1 已采集基线（修 BUG-53 前后）

| 段 | 修前典型 | 线程池修复后典型 | 结论 |
|----|----------|------------------|------|
| Overlay→Unity ipc | 多数 &lt;20ms；偶发 0.7～2.3s | 同左，偶发 `frame_stall` | 偶发卡帧待 `frame_stall` 日志继续看 |
| Socket 回执（take/start/stop） | 1.8～8s | 多数 15～60ms | **主因曾是 Unity socket 读循环绑主线程** |
| 服务端处理 | 同步 handler，无刻意 sleep | 同左 | 非主因（本项不改 server） |
| 装饵→等待手感 | 依赖多次 `pond_user_updated` | Overlay 本地时间线补丁 | **终态应收到 Unity 表现层** |

日志：

- Unity：`%USERPROFILE%\AppData\LocalLow\DefaultCompany\fish-social-unity\Player.log`
- Overlay：`%TEMP%\FishSocialOverlay-latency.log`

### 3.2 根因分层

| 层 | 判断 | 证据 |
|----|------|------|
| 服务端逻辑/DB | 非主因（待验证细项） | handler 同步；聊天同连接约 0.3s |
| Socket 传输 | 曾是主因，已缓解 | 主线程 `ConnectLoop` → 每帧一包；改线程池后 RTT 正常 |
| Unity 主线程/队列 | 仍影响表现权威 | 表现跟 `CurrentPhase` 来自服务器更新；`availableActions` 同源 |
| Named Pipe | 非主因 | 正常 ipcMs 个位数～二十 ms |
| Overlay UI | 过渡预测引入双状态机 | BUG-53 本地时间线与服务器中间态冲突 |

---

## 4. 确认方案（终态）

### 4.1 一句话

**自己的钓鱼：Unity 表现权威 + 服务端校验结算；Overlay 只显示 Unity 下发的 phase / visual / can\***。

### 4.2 职责

| 层 | 负责 | 不负责 |
|----|------|--------|
| Overlay | 渲染、点击→command、按 `availableActions` 禁用连点 | 猜 phase、维护 800/600ms 时间线 |
| Unity `OwnFishingPresenter`（名称可调整） | 点击/命令到达后立刻推进自己的表现 phase、visual、actions；按 `PHASE_MS` 本地走完装饵/抛竿/收杆入座；ack 失败回滚；咬钩/跑鱼/结算以服务器事件为准 | 鱼获随机、背包、合法性最终裁决 |
| Server | 校验命令、咬钩/结算、同步**他人** | 驱动自己客户端每一帧手感（P0 不改 server；可选后续降噪中间广播） |

### 4.3 本地时长（与 server `PHASE_MS` 对齐，常量单处维护）

| 阶段 | 时长 |
|------|------|
| baiting | 800ms |
| casting | 600ms |
| stopping → seated（表现） | ≥200ms，可与收线 once 动画衔接 |

打窝时长以服务器/ack 或已有 castDuration 为准；无固定值时允许保持 groundbaiting 直至 `pond_user_updated` 回 seated，但**进入打窝的首帧**必须本地立刻切换。

### 4.4 与 BUG-53 关系

- BUG-53：**已实现**过渡 hotfix（Overlay 预测 + socket 线程池等）。
- BUG-54：把表现权威收到 Unity，**删除或大幅收缩** Overlay 侧 `PredictOwnAction` 时间线 / 乐观 `availableActions`。
- 实现期可短暂双轨，验收前 Overlay 不得再维护与服务器重复的 phase 时间线。

### 4.5 回滚

- 环境变量或编译开关：`FISH_SOCIAL_OWN_FISHING_PRESENTER=0` 时退回「纯跟服务器 phase」（可保留 socket 线程池等基础设施修复）。
- 不回滚 BUG-52 的 state 合并与日志缓冲。

---

## 5. 功能范围

### 5.1 P0

| # | 功能点 | 说明 |
|---|--------|------|
| 1 | `OwnFishingPresenter`（或等价） | 管理自己的 presentation phase/visual/actions |
| 2 | 命令到达立刻推 Overlay | `take_spot` / `start_fishing` / `stop_fishing` / `groundbait_start` / `leave_spot` / 咬钩相关 |
| 3 | 本地时间线 | start：bait→cast→waiting；stop：stopping/resolving 后 seated + 按钮 |
| 4 | Reconcile | ack 失败回滚；服务器咬钩/跑鱼/结算覆盖表现 |
| 5 | Overlay 变瘦 | 去掉猜阶段时间线；按钮以 Unity `availableActions` + phase 为准 |
| 6 | 延迟日志 | `local_presentation_applied`；保留 commandId 关联 |

### 5.2 P1（可选，本项可不做）

- 服务端对自己中间 phase 广播降噪。
- 常量与 server 共享生成（不改 shared 行为，仅文档对齐亦可）。

---

## 6. 技术影响

### 6.1 数据 / API

- 复用现有 overlay command + socket 事件；**不新增** REST。
- `NativeOverlayStateDto` 的 `fishingPhase` / `petVisualState` / `availableActions` 在自己操作期间由 Presenter 填充。

### 6.2 涉及文件（预估）

- `fish-social-unity/Assets/Scripts/Desktop/`：新建 Presenter；改 `DesktopAppBootstrap`、`SocialPondSessionController`（乐观 phase 仅表现层）、`OverlayPondStateBuilder`、`PetStateController` 衔接
- `desktop-overlay/`：收缩 `PondScenePresenter` / `MainWindow` 中 BUG-53 预测时间线；保留 seat 点击反馈若仍需要可改为纯跟 state
- **禁止本项改**：`mobile/`、`server/`、`shared/`

### 6.3 量化验收指标

| 指标 | 目标 |
|------|------|
| 点击→本地动画/按钮（Overlay 已显示） | P50 ≤50ms，P95 ≤100ms |
| 装饵→等待视觉空窗 | 无可感停顿（本地连续推进） |
| 收杆→入座按钮可见 | 本地 stopping 结束后 ≤100ms（不含网络） |
| 连点同一动作 | 不重复播、不重复有效发包 |
| 命令失败 | 回滚表现 + 可见错误 |
| Socket 往返 | 继续记录；>2s 提示超时（基础设施已有） |

---

## 7. 验收标准

- [x] 落座后立刻出现开始钓鱼 / 打窝 / 离席（Unity `BeginTakeSpot` 推 availableActions）
- [x] 开始钓鱼：立刻装饵，再平滑抛竿→钓鱼中（Unity 本地 800+600ms 时间线）
- [x] 收杆：立刻收线；约 250ms 后入座按钮（Unity `StopFishingTimeline`）
- [x] 打窝 / 离席 / 上钩（自然或 Debug 强制）本地立刻切换；到期由 Unity `Tick` 收线
- [x] 连点：Overlay `_pendingActionName` 禁用；Unity 忽略重复 start/stop
- [x] ack 失败：`OwnFishingPresenter.OnCommandFinished` 回滚 + Overlay 提示
- [x] Overlay 已去掉 bait/cast 时间线（仅保留落座一帧预测）
- [x] 延迟日志：`local_presentation_applied` / `local_presentation_rollback`（commandId）
- [x] 开关：`FISH_SOCIAL_OWN_FISHING_PRESENTER=0` 关闭本地表现
- [ ] 实机：重打 Unity Debug 包 + 重启 Overlay 后勾选运行时 P50/P95（待用户验收）

---

## 8. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 表现与服务器 phase 短暂不一致 | 仅自己；以 ack/bite/miss/settle 和解；超时回滚 |
| 与 BUG-53 代码冲突 | 实现时明确删除 Overlay 时间线，避免双轨长期并存 |
| 打窝时长不固定 | 首帧本地切换；结束跟服务器 |

依赖：BUG-52 state 合并、BUG-53 socket 线程池应保留。

---

## 9. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-10-18 | 策划 | 初稿；方案已确认：Unity 表现权威 + 服务端校验；登记 BUG-54 |
| 2026-10-18 | 开发 | 已实现：`OwnFishingPresenter` + Overlay 瘦身；待打包实机验收 |
