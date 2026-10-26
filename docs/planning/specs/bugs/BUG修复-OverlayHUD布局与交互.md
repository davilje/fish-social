# Bug：Overlay HUD 布局回退、聊天栏、Debug 菜单与操作卡顿

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | WPF 钓鱼 Overlay（`desktop-overlay`）HUD 与交互缺陷 |
| 编号 | **BUG-48** … **BUG-52**（分项状态以计划表为准） |
| 类型 | **Bug修复** |
| 负责人 | Overlay 客户端（`desktop-overlay/`）+ Unity 桌面编辑器导出（`fish-social-unity/Assets/Scripts/Desktop/Editor/`） |
| 状态 | **已实现**（分项；运行时验收项待 Unity 重打包后实机勾选） |
| 目标版本 | hotfix |
| 优先级 | P0（BUG-48 布局回退、BUG-52 卡顿）/ P1（其余） |
| 设计时间 | **2026-10-16** |
| 完成时间 | **2026-10-16** |
| 关联 | `docs/planning/specs/bugs/BUG修复-Cozy桌面页还原.md`（九宫格圆角分族）；`docs/archive/2026-10-26/superseded/design/ui/2026-10-14-wpf-overlay-layout-refinement.md` |

计划表状态以 `scripts/planning/build-master-plan-xlsx.py` 的 `ENSURE_PLAN_ROWS` 为准。

### 分组一览（每组可单独合并）

| 编号 | 组 | 包含的用户反馈 | 主要改动面 |
|------|----|----------------|------------|
| BUG-48 | HUD 导出把九宫格边距写回旧值，Overlay 回退旧 XAML 布局 | 「overlay 中的 UI 被改成这样了」：状态栏跑到左上、钓鱼按钮跑到右侧、右上出现「打开主界面/退出鱼塘」 | `OverlayHudExporter.cs`、`CozyTheme.cs`、回退提示 |
| BUG-49 | HUD 布局重排 | 移除左上设置与呼出菜单，只留右键菜单；状态栏放右下；开始钓鱼/打窝/离席回底部居中；聊天栏宽度减半放左下；Debug 按钮只改宽度；退出鱼塘移到右下；移除打开主界面 | `OverlayHud.prefab` → 导出 JSON、`OverlayHudWidgets.Required`、XAML 回退布局 |
| BUG-50 | 聊天栏 | 展开到 10 行；展开/折叠按钮没对齐；发送后旧消息仍在输入框 | `MainWindow.xaml.cs` 聊天段、`OverlayHudLayout` 聊天尺寸 |
| BUG-51 | Debug 菜单点击无反应 | 打开 Debug 菜单后点按钮没有可见结果 | `MainWindow.xaml.cs` Debug 段、Unity `ExecuteOverlayGameplayDebug` 回执 |
| BUG-52 | 操作延迟与卡顿 | 点击和拖动有明显延迟 | 状态消息合并、Unity 发布频率、延迟日志 |

合并顺序建议：**BUG-48 → BUG-49**（同一份 JSON / Prefab，必须先修导出），BUG-50、BUG-51、BUG-52 互不依赖，可并行。

---

## 1. 现象与复现

| 环境 | 记录 |
|------|------|
| 构建 | Unity `Fish Social → 打包` 产物 `fish-social-unity/Builds/Windows64-Debug/FishSocialOverlay/`，`FishSocialOverlay.dll` 2026-10-16 13:53:08，进程启动 13:53:25 |
| 平台 | Windows 10 19045，WPF netcoreapp3.1，设计画布 960×560 |
| 复现 | BUG-48 每次从 Unity 打包都复现（导出必定写回 10/20）；BUG-50 发送消息稳定复现；BUG-52 进入鱼塘有多只猫时持续出现 |
| 服务端进程 | 本批未取服务端时间戳，服务端相关结论均记为 **待验证** |

### 1.1 BUG-48 布局整体变样

| 操作 | 预期 | 实际 |
|------|------|------|
| 从 Unity 打包后启动 Overlay | 按 `overlay-hud.json`：状态卡在右下，钓鱼按钮在底部中间，图标菜单栏在右侧 | 状态栏在左上，「开始钓鱼/打窝/离席」竖排在右下，右上是带文字的「打开主界面」「退出鱼塘」「Debug」，左上多一个设置齿轮——这是 `MainWindow.xaml` 的旧回退布局 |

### 1.2 BUG-49 布局重排（新需求，以 JSON 布局为基准）

| 控件 | 现状（JSON 布局） | 目标 |
|------|-------------------|------|
| `btn_menu_toggle` + 6 个菜单按钮（地图/商店/好友/鱼获/排行/设置） | 右侧菜单栏，点齿轮展开 | **移除**。功能只走右键菜单（右键菜单已有这 6 项） |
| `btn_open_main` | 右侧菜单栏第二格 | **移除** |
| `btn_exit_pond` | 右侧菜单栏第三格 x=900 | 移到**右下角**，右、下各留 16px |
| `cap_status` | x=720 y=472 164×76 | 保持右下，整体左移到 `btn_exit_pond` 左侧，间距 8px |
| `dock_fishing`（打窝 / 开始钓鱼 / 离席） | x=363 y=508 | 保持**底部居中** |
| `dock_chat` | x=16 y=506 320×42 | 保持**左下**，宽度为回退布局截图宽度（约 712px）的一半；取 **320**，因为右边需要给 `dock_fishing`（x=363）留 ≥8px |
| `btn_debug_gameplay` | 38×36 图标，回退布局里是 208 宽的「Debug」条 | **只改宽度**：贴合图标 + 文字，约 88px；位置不动 |
| `btn_pan_left/right` | 28×40，「向左」「向右」两个字溢出底板 | 64×36 居中（当前工作区已在运行时改，需落到 Prefab） |

### 1.3 BUG-50 聊天栏

| 操作 | 预期 | 实际 |
|------|------|------|
| 点展开 | 显示最近 10 行 | `chat_log` 高 98px，12px 字 + 4px 行距约 20px/行，只能放 4～5 行 |
| 展开 / 收起 | 按钮在两种状态下都和所在行居中、和发送按钮同列 | `chat_toggle` 固定在面板坐标 y=7；面板向上长高后按钮跑到右上角，发送按钮在底行，两者不在同一行。图标是对话气泡旋转 180°，看不出是「收起」。回退布局里按钮是 28×24，JSON 里是 28×28 |
| 输入并发送 | 发送成功后输入框清空 | 文字留在输入框里 |

### 1.4 BUG-51 Debug 菜单

| 操作 | 预期 | 实际 |
|------|------|------|
| 打开 Debug，点「升级」「+100万金币」等 | 有执行中 / 成功 / 失败提示，数值变化 | 看不到任何变化 |

### 1.5 BUG-52 延迟与卡顿

| 操作 | 预期 | 实际 |
|------|------|------|
| 鱼塘里点按钮、拖动场景、悬停猫 | 立即响应 | 点击要等一下才有反馈，拖动和悬停不顺 |

---

## 2. 时间戳证据

日志：`%TEMP%\FishSocialOverlay-latency.log`（UTC Unix ms）。Unity `Player.log`（`AppData\LocalLow\DefaultCompany\fish-social-unity\Player.log`）本次没有 Debug 命令相关记录。

| 节点 | 本批证据 |
|------|----------|
| Overlay `command_sent` | 有：`overlay_command_sent id=… command=…`。最近 166 条中有 `send_pond_chat` ×6、`start_fishing`、`stop_fishing`、`take_spot` 等；**没有 `gameplay_debug`**，因为 `SendGameplayDebug` 不写日志 |
| Unity `overlay_command_received` | **缺**，待补 |
| Socket `event_sent` / 服务端 / ACK | **缺**，待验证 |
| Unity `state_queued` | **缺** |
| Overlay `state_received` | 有：`overlay_state_received sequence=… pond=…`，不带 commandId |

关键数据（日志末尾 30MB）：

- 最近 60 秒收到 **2048** 条 state，平均 **34.1 条/秒**；相邻间隔 P50 **7ms**、P95 **17ms**。日志里成对出现（例：`…658884` 和 `…658885`），即 Unity 大约**每帧发两次**整份状态。
- 日志文件 **335,383,760 字节**，没有轮转。每条 state 都在 UI 线程调 `File.AppendAllText`（`IpcProtocol.cs` `LatencyTrace.Write`），每次打开、追加、关闭文件。
- 构建产物与源码：13:53:10 源 `desktop-overlay/OverlayResources/hud/overlay-hud.json` 被改写（SHA256 `401CB55C…`），`components.button.spriteSlice=[10,10,10,10]`、`paper=[20,20,20,20]`；当前工作区 `CozyTheme.SpriteBorder=12`、`PaperBorder=22`。

---

## 3. 根因分层

### BUG-48

| 层 | 判断 |
|----|------|
| Unity 编辑器导出 | **主因**。`DesktopBuildMenu.ExportOverlayHudForBuild` 打包时调用 `OverlayHudExporter.Export`，从 `OverlayHud.prefab` 重写 `desktop-overlay/OverlayResources/hud/overlay-hud.json`。导出器写死按钮 10、纸张 20（`OverlayHudExporter.cs` 约 913/924 行默认值，942～944 行校验，1035～1036 行契约检查） |
| Overlay 加载 | `OverlayHudLayout.CompleteCozyComponents` 要求组件边距等于 `CozyTheme`（工作区已改为按钮 12、大纸 22、小纸 10，配合新圆角 PNG），不等就抛 `InvalidDataException`，`TryApply` 返回 false，退回 XAML 旧布局 |
| 触发 | 上一轮圆角分族把 Overlay 侧边距改成 12/22（**工作区未提交**），没有同步改 Unity 导出器。两边各有一份常量 |
| 提示被吞 | 回退时写 `ErrorText.Text = "HUD 未加载：…"`，但每条 state 都执行 `ErrorText.Text = message.ErrorMessage`（`MainWindow.xaml.cs` 522 行），约 30ms 后提示就没了 |
| 服务端 / Socket / Pipe | 不适用 |

### BUG-49

布局唯一来源是 `OverlayHud.prefab`，打包时导出覆盖 JSON。只改 JSON 或只改 `ApplyCozyLayoutRefinement` 会在下次打包被冲掉（运行时 `FitPanButton` 也属于这一类，需迁到 Prefab）。`OverlayHudWidgets.Required` 把 `btn_menu_toggle`、6 个菜单按钮、`btn_open_main` 列为必需，直接删 Prefab 节点会触发「缺少必需控件」回退。

### BUG-50

| 子项 | 根因 |
|------|------|
| 10 行 | `chat_log` 高 98，`ComputeChatDockExpandedHeight` 按子控件最低点算展开高度，没有行数概念 |
| 按钮对齐 | `ApplyHudChatDockLayout` 只改面板高度并保持底边，子控件坐标不变，`chat_toggle` 留在 y=7 |
| 输入未清空 | 确认发送只靠 `ApplyPondChat` 里 `ContainsText(RecentChats, _pendingChatText)` 文本完全相等。等待期间只要来一条 `ErrorMessage` 非空的 state 就 `FinishChatAck(false)`、保留文字。Unity `ExecuteOverlayCommand` 在执行前会清空错误再发布，但 34 条/秒里已在管道中的旧 state 先到；服务端若裁剪/过滤文字也永远对不上，8 秒后超时保留文字。具体是哪一条 **待验证**（需带 commandId 的回执） |

### BUG-51

| 层 | 判断 |
|----|------|
| Overlay 反馈 | **已确认的问题**：「正在执行 Debug…」和 Unity 回写的结果都只写 `ErrorText`，下一条 state 就覆盖；回退布局里 `ErrorText` 在状态卡内，被 Z500 的 Debug 弹窗和遮罩盖住。所以即使执行成功，用户也看不到 |
| Overlay 发送 | 待验证：`SendGameplayDebug` 不写 `LatencyTrace`，无法证明命令发出 |
| Unity | 待验证：`ExecuteOverlayGameplayDebug` 在 `GameplayDebugGate.IsClientEnabled()` 为 false 时只写错误文字；其它分支结果是否回写 state 需加日志 |
| 服务端 | 待验证 |

### BUG-52

| 层 | 判断 |
|----|------|
| Unity 主线程发布 | **主因之一**：约每帧两次整份 state（34 条/秒，成对间隔 1～2ms）。`DesktopAppBootstrap` 有 51 处 `PublishNativeOverlayState()` 直接发送，没有合并 |
| Named Pipe | 读线程每条消息 `Dispatcher.BeginInvoke(HandleMessage)`，默认 Normal 优先级高于 Input。积压的 state 全部逐条执行，点击输入排在后面 |
| Overlay UI 线程 | **主因之一**：每条 state 先同步 `File.AppendAllText`（335MB 文件），再 `_scene.Apply` 全量刷新。鼠标移动时的 `WM_NCHITTEST` 命中测试和悬停重算也在 UI 线程（工作区已加 24ms/4px 命中缓存和 50ms 悬停节流，未提交） |
| Socket / 服务端 | 待验证：没有命令到 state 的 commandId 关联，无法拆出往返耗时 |

---

## 4. 修复与验收

### 4.1 BUG-48 HUD 导出与 Overlay 九宫格边距统一（P0）

范围：

- Unity 导出器的组件默认值、`ValidateComponentBorder`、契约检查与 Overlay `CozyTheme` 一致：按钮 12、大纸 22、小纸（输入框、聊天栏）10。只保留一处常量来源，或加测试比较两边数值。
- 每个 `kind=button` 控件导出的 `spriteSlice` 与组件一致；`cap_status` 22；`dock_chat`、`chat_input`、`chat_scroll` 10。
- 回退时的「HUD 未加载」提示改为独立控件或状态，不被 `ErrorMessage` 覆盖；同时写一行 Overlay 日志。
- 重新导出 JSON 后更新 `desktop-overlay/tests/cozy_source_contract.json` 的 `final_hud_json_sha256` 与控件顺序。

非目标：不改圆角 PNG；不改服务端。

验收：

- [ ] Unity 打包后，源 `overlay-hud.json` 与构建目录副本组件边距为 12/22/10，Overlay 进入 JSON 布局（状态卡右下、钓鱼按钮底部居中）
- [ ] 人为把 JSON 改成 10/20，Overlay 回退时屏幕上持续显示原因，日志有记录
- [x] `PYTHONUTF8=1` 下 `test_cozy_ui_source`、`test_overlay_layout_refinement` 中与本组相关的用例通过；Unity `Validate Overlay HUD Export Contract` 通过

### 4.2 BUG-49 HUD 布局重排（P1，依赖 BUG-48）

范围（改 `OverlayHud.prefab`，再导出）：

- 删除 `btn_menu_toggle`、`btn_menu_map/shop/friends/catch/leaderboard/settings`、`btn_open_main`，同步改 `OverlayHudWidgets.Required`、`MenuRailOrder`、`ApplyCozyLayoutRefinement` 菜单栏段、`MainWindow` 绑定。`menu_rail` 可删或只剩退出按钮。
- 右键菜单保留现有条目。「打开主界面」的入口改由右键菜单提供：确认「当前鱼塘」（`menu_pond`）能唤出主界面，否则在右键菜单加「打开主界面」（`open_main`）。
- `btn_exit_pond` 38×40，放 x=906 y=504（右、下各 16）；`cap_status` 右边缘 ≤ 898；`dock_fishing` 水平居中；`dock_chat` 16,506,320×42；`btn_debug_gameplay` 宽约 88，显示图标 + 「Debug」，位置不动；平移按钮 64×36 居中，右侧按钮不压右下控件。
- XAML 回退布局 `LegacyLeftHudStack` / `LegacyRightTopHudStack` / `LegacyRightBottomHudStack` 同步成相同排布，避免回退时又变样。

非目标：不新增菜单项功能；不改玩家头像右键菜单。

验收：

- [ ] 屏幕上没有左上齿轮、没有菜单栏图标、没有「打开主界面」按钮；右键空白处菜单可打开地图、商店、好友、鱼获、排行、设置、主界面
- [ ] 右下依次为状态卡、退出鱼塘；底部中间为打窝 / 开始钓鱼 / 离席；左下为 320 宽聊天栏；三组之间 ≥8px 不重叠
- [ ] Debug 按钮只变宽度，文字不溢出
- [x] 从 Unity 再打包一次，布局不变

### 4.3 BUG-50 聊天栏（P1）

范围：

- 展开高度按行数算：日志区可见 **10 行**（按 `chat_log` 字号与行距计算，约 200px），面板仍从底边向上长；在小视口预设下不超出画布顶部（超出时按可用高度截行并保留滚动）。
- 折叠按钮固定在面板底行右侧，展开前后屏幕位置不变（点同一处即可收起）；展开时发送按钮在它左侧，二者同一行垂直居中，误差 ≤1px。收起图标换成能看出方向的箭头，或给气泡图标加状态区分。
- 发送：Unity 对 `send_pond_chat` 回一条带 `commandId` 的结果（成功 / 失败 + 文案），Overlay 按 commandId 结束等待；成功清空输入框，失败保留文字并提示。不再用 `RecentChats` 文本比对或任意 `ErrorMessage` 判定。若暂不改 IPC，则发送时先清空并暂存，失败或超时再还原。

非目标：不改服务端聊天校验规则、不改气泡。

验收：

- [ ] 展开后能同时看到 10 条一行的消息
- [ ] 展开 / 收起各 10 次，按钮位置不跳，和发送按钮同一行
- [ ] 连续发送 5 条（含与历史重复的文字）：每次成功后输入框为空；断开 Socket 后发送，文字保留并提示
- [ ] 打窝成功提示（`ErrorMessage` 非空）显示期间发送消息，输入框仍能清空

### 4.4 BUG-51 Debug 菜单无反应（P1）

范围：

- `SendGameplayDebug` 写 `overlay_command_sent id=… command=gameplay_debug action=…`；Unity 收到与执行完成各写一行，带同一 commandId。
- Debug 弹窗内加一行状态（执行中 / 成功 / 失败原因），结果按 commandId 回写，不依赖被 state 覆盖的 `ErrorText`。
- 全局 `ErrorText` 的临时提示至少停留 3 秒，或只在 `ErrorMessage` 变化时更新，不被每条 state 清空（同时解决 BUG-48 回退提示被吞）。
- 依据日志定位剩余原因（客户端未开启 Debug、命令未到 Unity、服务端拒绝），在本 spec 补结论。

非目标：不新增 Debug 动作。

验收：

- [ ] 弹窗中逐个点 17 个按钮，每个都在 1 秒内显示执行中，并显示成功或失败原因
- [ ] 日志中每个点击都有 Overlay 发送、Unity 收到、Unity 完成三行，commandId 一致
- [ ] 「升级」「+100万金币」后主界面数值变化

### 4.5 BUG-52 操作延迟与卡顿（P0）

范围：

- Overlay：管道读线程只保留最新一条 state，UI 线程没有待处理的 state 时才投递一次（合并）；命令结果、错误、连接状态不被合并丢弃。state 投递优先级低于输入。
- Unity：同一帧内多次 `PublishNativeOverlayState()` 合并为一次（脏标记，帧末发送）；无变化时不发，空闲时 ≤10 条/秒；命令结果立即发送。
- `LatencyTrace`：后台缓冲写；state 只在带 commandId 回执或每秒抽样一条时记录；单文件 10MB 轮转。补 `overlay_state_applied`（含处理耗时 ms）和命令往返的 commandId。
- 保留工作区已加的 `WM_NCHITTEST` 命中缓存与悬停节流，并验证没有出现漏点。

非目标：不靠调高 Unity 帧率或重启进程当作修复；不改服务端广播频率（服务端证据待补）。

量化验收：

- [ ] 鱼塘 8 只猫、钓鱼中：Overlay 收到 state ≤15 条/秒（空闲 ≤10），UI 线程待处理 state ≤1
- [ ] 点击「开始钓鱼」到按钮出现按下 / 禁用反馈：P50 ≤50ms，P95 ≤100ms（本地 UI，不含服务端）
- [ ] 命令发出到对应 state 回到 Overlay：记录 P50/P95；>2s 记为超时并提示；服务端耗时单独列出
- [ ] 连续拖动场景 10 秒无明显掉帧；悬停卡片能正常出现与消失
- [ ] 运行 1 小时日志 ≤10MB（含轮转）
- [ ] 首次点击、连续点击、重复点击；退出鱼塘、切换鱼塘、重新进入；Socket 断线后命令有失败提示

### 4.6 回滚

- BUG-48、BUG-49 只涉及导出器、Prefab、JSON、Overlay 布局代码，回滚对应提交并重新导出即可。若只回滚 Overlay 侧 `CozyTheme` 到 10/20，必须同时把 Overlay 圆角 PNG 回滚到旧版，否则圆角被拉伸。
- BUG-50、BUG-51 若新增 IPC 结果消息，Overlay 需兼容没有该消息的旧 Unity 构建（退回超时逻辑）。
- BUG-52 的合并与节流可用环境变量关闭，便于对比。

---

## 5. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-10-16 | 登记 BUG-48～52，状态 **已确认**。依据：用户截图、latency 日志、构建目录 JSON 比对 |
