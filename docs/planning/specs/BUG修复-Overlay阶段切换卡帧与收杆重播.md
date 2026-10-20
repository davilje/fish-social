# BUG-56 Overlay 阶段切换卡帧与收杆动画重播

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 阶段切换卡帧与收杆动画重播 |
| 编号 | **BUG-56** |
| 负责人 | 策划 |
| 状态 | **已实现** |
| 目标版本 | hotfix |
| 关联全景章节 | Overlay 钓鱼操作；承接 BUG-52 / BUG-53 / BUG-54 |

---

## 1. 背景与目标

### 1.1 背景

BUG-54 之后，Overlay 命令到达 Unity 的管道耗时已经是数毫秒到十几毫秒，但换阶段时主线程仍会停 2～6 秒，点击感觉还是慢。同一轮钓鱼里，收杆已经回到坐下后，再点收杆会再播一遍收杆动画。

### 1.2 目标

- 自己的占位、开始钓鱼、收杆、离席、上钩，从 Overlay 发出到本地表现生效，P50 ≤ 100ms，P95 ≤ 250ms。
- 这些操作之后 2 秒内，不出现 `frame_stall` ≥ 250ms。
- 一次收杆只播一段收杆表现（`stopping` 或上钩时的 `resolving`，然后 `seated`）。未重新开始钓鱼之前，再点收杆不重播。

### 1.3 非目标

- 不改服务端钓鱼状态机和 `PHASE_MS`。
- 不恢复 BUG-53 的 Overlay 猜阶段时间线。
- 不把「关掉垂直同步 / 提高目标帧率 / 重启桌面端」写成修复。
- 不重做猫咪分层图集。

---

## 2. 现象与复现

构建：`Builds/Windows64-Debug`，日志 `Player-prev.log`（该进程 2026-10-19 13:08 结束）。时间均为 UTC Unix ms。

1. Overlay 占位 → 开始钓鱼 →（调试强制咬钩）→ 收杆。
2. 预期：点下去立刻换动作；收杆一次后停在坐下，再点收杆无新动画。
3. 实际：管道很快，但随后主线程卡数秒；收杆结束约 10 秒后再点，又从 `stopping` 播到 `seated`。

可稳定复现：同一次钓鱼里连续两次收杆。卡帧在每次自己的阶段切换后出现。

---

## 3. 时间戳证据

同一会话 commandId 1～10。`ipcMs` = Unity `overlay_command_received` 减 Overlay `sentAtMs`。

| commandId | 命令 | received atMs | ipcMs | 紧接着的本地表现 |
|-----------|------|---------------|-------|------------------|
| 1 | take_spot | 1792385808611 | 2 | seated |
| 2 | start_fishing | 1792385812613 | 12 | baiting |
| 5 | gameplay_debug force_bite | 1792385975109 | 11 | hooked |
| 6 | stop_fishing | 1792386064217 | 10 | resolving，随后 1792386070574 seated |
| 7 | stop_fishing | 1792386080374 | 9 | **再次** stopping，随后 1792386086845 seated |
| 8 | leave_spot | 1792386101350 | 4 | idle |
| 9 | take_spot | 1792386104324 | **332** | seated（夹在卡帧里） |
| 10 | start_fishing | 1792386108521 | 4 | baiting |

卡帧（`frame_stall`，主线程 `unscaledDeltaTime`）：

| atMs | ms | 落在哪次操作之后 |
|------|-----|------------------|
| 1792385980960 | 5833 | id=5 hooked 之后 |
| 1792386070578 / 1792386071157 | 3462 / 3461 | id=6 回到 seated 时 |
| 1792386086848 / 1792386086849 | 3490 / 2968 | id=7 再次收杆回到 seated 时 |
| 1792386104322 | 2365 | id=9 占位 ipc 被拉到 332ms 的同一刻 |
| 1792386110859 | 2320 | id=10 baiting 之后 |

本段日志里 **没有** 与这些 commandId 对齐的 `event_sent` / `ack_received` 行，服务端处理和 Socket RTT **待验证**。多秒空隙出现在 `overlay_command_received` 已经完成之后，量级与 `frame_stall` 一致，所以不是 Named Pipe。Overlay `applyMs` 在此前同类日志里是 1～2ms，**本段未单列，Overlay 渲染耗时待验证**，不作为这次数秒卡顿的原因。

堆栈反复出现 `OnPondUsersChanged` → `FlushNativeOverlayState` → `PublishState`。阶段切换会触发自己的宠物贴图 `FishingLayerArt.Open`（主线程读 PNG 并缩放）。现有预热用 256×256，`SpriteFramePetRenderer` 按真实槽位尺寸做缓存键，尺寸不一致就缓存未命中，换阶段那一帧重新解码。

收杆重播：id=6 已到 `seated` 之后，id=7 仍进入 `stopping`。`BeginStopFishing` 在本地 phase 已是 seated 时应直接返回；能再次进入，说明当时 `EffectivePhase` 没有停在 seated（表现器被 `Clear` 后回退到服务端仍处在可收杆阶段，或本地 phase 被改回可收杆阶段）。具体分支实现时用同一条日志补一行「拒绝收杆 / 接受收杆」确认，不得靠猜。

---

## 4. 根因分层

| 层 | 判断 |
|----|------|
| 服务端逻辑 / 数据库 | 待验证。本段没有对应 ACK 时间戳 |
| Socket 传输与 ACK | 待验证。同上 |
| Unity 主线程 | **卡帧根因方向已证实**。`frame_stall` 2.3～5.8s，与阶段切换同时出现；预热尺寸与真实开图尺寸不一致 |
| Named Pipe | **不是主因**。除被卡帧挡住的 id=9 外，ipcMs 为 2～12 |
| Overlay UI 线程 | 待验证。不解释 Unity 侧 `frame_stall` |
| 收杆重播 | **已证实现象**。id=6 seated 之后 id=7 又应用 stopping。守卫没有罩住「表现器已结束 / 被清空」之后的第二次点击 |

---

## 5. 确认方案

1. **贴图解码不得占用户操作那一帧。** `FishingLayerArt.Open` / `Warmup` 的缓存键必须覆盖英雄猫真实槽位尺寸（与 `SpriteFramePetRenderer` 使用的 draw 宽高相同）。预热在进塘后分帧完成；单帧解码预算 ≤ 50ms。未预热完时，操作帧只切换逻辑 phase 并推送 Overlay，贴图下一帧再补，禁止在 `FlushNativeOverlayState` 里同步读 PNG。
2. **收杆只播一次。** 本地已经进入或结束 `stopping` / `resolving` / `seated`（且这次坐下来自收杆，而不是新的开始钓鱼）时，后续 `stop_fishing` 直接忽略，直到下一次成功的 `start_fishing`。`ReconcileFromServer` / `Clear` 不得把「已经本地收杆结束」退回成可再次开播收杆的服务端旧 phase。
3. 拒绝第二次收杆时打日志：`commandId`、当时本地 phase、服务端 phase、是否忽略。便于和 `local_presentation_applied` 对账。
4. 仍每次把有效操作发给服务器。失败 ACK 的回滚保持 BUG-54。

### 回滚

`FISH_SOCIAL_OWN_FISHING_PRESENTER=0` 关闭本地表现器（已有）。贴图预热失败时允许退回按需加载，但不得重新堵在点击帧上；若做不到，整段预热改动可关，收杆忽略逻辑单独保留。

---

## 6. 验收标准

在 Debug 包、本机服务端、鱼塘内至少各测 10 次：

- [ ] 占位、开始钓鱼、收杆、离席、强制咬钩：`overlay_command_sent` → `local_presentation_applied` 的 P50 ≤ 100ms，P95 ≤ 250ms。代码已进 Debug 包 DLL，本轮没有新的 10 次实机日志。
- [ ] 上述操作之后 2 秒内 `frame_stall` ≥ 250ms 的次数为 0。预热改为真实槽位、每帧一张图；操作帧只 `TryGetCached`。实机 `frame_stall` 未复测。
- [x] 连续点两次开始钓鱼，只出现一组 baiting → casting → waiting。`BeginStartFishing` 在 baiting/casting/waiting 且已有进行中的 start 时直接返回。
- [x] 收杆一次后 phase 到 seated；2 秒内再点收杆，不再出现新的 `local_presentation_applied phase=stopping|resolving`。`_stopLatched` 在 `Clear` 之后仍在，第二次只打 `stop_fishing_ignored`。
- [x] 上钩后再收杆，只播一段 resolving → seated，不接着再播 stopping。上钩收杆、脱钩、钩子超时都会锁住，直到下一次开始钓鱼。
- [x] 离席、重新占位、再开始钓鱼，按钮与动画跟新的一轮，不沿用上一轮的忽略标记。占位、开始钓鱼、打窝、离席会清掉锁。
- [x] 服务端 ACK 失败时仍回滚，不把失败操作留在本地 seated。`stop_fishing` 回滚会清锁，失败后可以再收一次。

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-10-19 | 策划 | 初稿。已确认：卡帧修贴图解码时机与尺寸；收杆结束后忽略重复收杆 |
| 2026-10-19 | Unity | 已实现。真实槽位分帧预热；收杆锁在 Clear 后仍忽略第二次收杆。P50 / frame_stall 待 Debug 包实机复测 |
