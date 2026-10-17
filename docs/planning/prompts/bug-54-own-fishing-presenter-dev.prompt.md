# 开发提示词：Unity 自己钓鱼表现权威（BUG-54）

你负责 Fish Social **Unity 桌面 + WPF Overlay**。只做 **BUG-54**。  
按已确认方案实现：**自己的钓鱼表现由 Unity 本地推进；服务端只校验/结算；Overlay 变瘦**。  
不改 `mobile/`、`server/`、`shared/`。不要把提高帧率、重启进程当成修复。

## 必读

1. `docs/planning/specs/BUG修复-Unity自己钓鱼表现权威.md`（**已确认** / **BUG-54**）全文，尤其 §4 确认方案、§6.3 量化指标、§7 验收
2. 关联：`docs/planning/specs/BUG修复-OverlayHUD布局与交互.md`（BUG-52）；BUG-53 为 Overlay 过渡预测（本项要退役其时间线）
3. 预估文件：
   - `fish-social-unity/Assets/Scripts/Desktop/`（新建 OwnFishingPresenter 或等价；`DesktopAppBootstrap`、`OverlayPondStateBuilder`、`SocialPondSessionController`、`PetStateController`）
   - `desktop-overlay/PondScenePresenter.cs`、`MainWindow.xaml.cs`（删除/收缩猜 phase 时间线与乐观按钮）

## 方案要点（勿走偏）

1. Overlay 点击 → Unity 收到 command **立刻**改自己的 presentation phase/visual/`availableActions` 并 `PublishNativeOverlayState(urgent)`。
2. 同时照常发 socket；**每次有效操作仍发服务器**。
3. 本地时间线对齐 server `PHASE_MS`：baiting 800ms → casting 600ms → waiting；stop 后尽快 seated 并亮按钮。
4. Reconcile：ack 失败回滚；`fish_bite` / `fish_miss` / settle 以服务器为准覆盖表现。
5. 他人猫仍纯跟服务器。
6. 环境变量或开关可退回「纯跟服务器 phase」。
7. 验收前 Overlay **不得**再维护与服务器重复的 bait/cast 本地时间线。

## 顺序

1. 落地 Unity `OwnFishingPresenter`（名称可调整），接管自己操作时的 phase/visual/actions。
2. 接到 overlay command / 会话 API 时先本地 apply 再发服务器；打延迟点 `local_presentation_applied`。
3. `FlushNativeOverlayState` / `OverlayPondStateBuilder` 对自己优先用 presentation 态。
4. 收缩 Overlay BUG-53 预测时间线；按钮与动画以 Unity 下发为准；保留连点禁用（跟 `availableActions`）。
5. 自检 §7；附一轮 Player.log + `%TEMP%\FishSocialOverlay-latency.log` 摘要。
6. 按 Skill `planning-progress-sync` 回写：**已实现** + 完成时间 + `npm run planning:master-xlsx`。

## 验收

对照 spec §7 与 §6.3：

- [ ] 点击→动画/按钮 P50 ≤50ms、P95 ≤100ms（本机）
- [ ] 装饵→等待无空窗；收杆后按钮即时
- [ ] 连点不重复；失败回滚有提示
- [ ] Overlay 无重复 phase 时间线（代码审查）
- [ ] 开关可回退；退出/重进鱼塘状态干净

## 派发

```text
@docs/planning/prompts/bug-54-own-fishing-presenter-dev.prompt.md 按此实现 BUG-54
```

建议角色：Unity 桌面为主 + Overlay 客户端（本仓库 Cursor 对话）。**不要**为本项改服务端。
