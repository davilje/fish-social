# STEAM-DESKTOP-07G Overlay 卡死排查记录

日期：2026-08-14  
状态：已验收，已归档  
范围：Unity 主程序、WPF Native Overlay、Named Pipe、Unity Socket.IO 鱼塘连接

## 1. 当前结论

已确认并验收：

- Steam 登录成功；
- WebSocket 可以连接；
- 进入鱼塘流程不再直接卡死；
- Overlay 为独立 WPF 进程，不加载第二个 Unity Player；
- 启动 Overlay 后关闭主程序，Unity 与 Overlay 均可退出；
- 退出时不再弹出 CMD 窗口。

归档日期：2026-08-15。

## 2. 问题演变

### 阶段 A：主程序直接关闭卡死

表现：

- 启动 `FishSocialDesktop.exe`；
- 不登录、不进入鱼塘，直接关闭；
- Unity 进程进入未响应。

初步怀疑：

- SteamAPI 清理；
- Socket 清理；
- 系统托盘线程；
- Overlay 清理；
- Unity 原生退出。

### 阶段 B：加入退出日志

日志确认以下清理均能完成：

```text
[Shutdown] tray cleanup complete.
[Shutdown] SteamworksTicketProvider.OnDestroy complete.
[Shutdown] SocialPondSessionController.OnDestroy complete.
[Shutdown] NativeOverlayProcessController.OnDestroy complete.
Curl error 42: Callback aborted
```

结论：

- 托盘、Steam、Socket 和 Overlay 的托管层清理没有停住；
- 卡死发生在 UnityPlayer 原生退出阶段，或发生在 Unity 托管层清理完成之后；
- 早期 `Application.wantsToQuit` watchdog 没有覆盖所有关闭路径，后改为在 `DesktopAppBootstrap.OnDestroy` 启动。

### 阶段 C：Overlay 懒加载修复

原实现于 `DesktopAppBootstrap.Awake()` 中立即执行：

```csharp
_nativeOverlay = gameObject.AddComponent<NativeOverlayProcessController>();
```

该组件的 `Awake()` 会立即创建 Named Pipe 后台线程。

修复：

- 不再在主程序启动时创建 Overlay 控制器；
- 只有点击“进入鱼塘”时才懒加载 Overlay；
- 移除 Bootstrap 与 Overlay 控制器重复关闭。

结果：

- 未启动 Overlay 时主程序可以正常退出；
- 证明 Overlay 生命周期是退出卡死的重要触发条件。

### 阶段 D：鱼塘进入卡死

关键日志：

```text
[Pond] WebSocket connected; sending Socket.IO auth packet.
[Pond] Socket connected; registering player and joining pond pond-calm
[Pond] Socket.IO connect completed. ok=True
```

问题：

- `register_player` 和 `join_pond` 可能同时调用 `ClientWebSocket.SendAsync`；
- .NET `ClientWebSocket` 不允许并发发送；
- 可能造成发送失败、ACK 丢失或鱼塘界面无响应。

修复：

- 使用 `SemaphoreSlim` 串行化所有 WebSocket 发送；
- 增加 ACK 超时；
- 增加 Socket 连接阶段日志。

结果：

- 进入鱼塘已不再直接卡死；
- 服务端可以创建鱼塘会话；
- 当前剩余问题转移到 Overlay 启动后的退出阶段。

### 阶段 E：Overlay/IPC 分层排查

增加了以下诊断开关：

```text
FISH_SOCIAL_DISABLE_OVERLAY=1
FISH_SOCIAL_OVERLAY_NO_IPC=1
FISH_SOCIAL_OVERLAY_SAFE_WINDOW=1
```

观察结果：

- 禁用 Overlay：不再卡死；
- Overlay 窗口启动后：关闭主程序可能卡死；
- 当前确认问题与 Overlay 启动生命周期相关；
- 仍需继续区分 WPF 窗口创建、Named Pipe 线程和 Unity 子进程管理。

## 3. 已实施的修复

### Unity 主程序

- Overlay 控制器懒加载；
- Overlay 退出清理改为后台线程；
- Named Pipe 释放改为后台线程；
- Overlay 进程启动改为后台线程；
- 移除 Unity 主线程每帧调用 `Process.HasExited`；
- Named Pipe 写入和 `Flush()` 改为后台线程；
- Socket WebSocket 发送串行化；
- Socket ACK 增加超时；
- 增加退出 watchdog；
- 增加 Steam、Socket、托盘、Overlay 退出日志。

### WPF Overlay

- 增加无 IPC 诊断模式；
- 增加普通非透明窗口诊断模式；
- 保留窗口拖动、菜单和主界面命令路径。

## 4. 当前最后一次有效证据

服务端在测试期间可以正常响应：

```json
{
  "ok": true,
  "db": "ok",
  "humanSocketCount": 0,
  "humanInPond": 0
}
```

Unity 关闭日志最后出现：

```text
[Shutdown] NativeOverlayProcessController.OnDestroy complete.
Curl error 42: Callback aborted
```

这说明当前不能继续把问题归因于服务端鱼塘处理。需要进一步取得 UnityPlayer 原生线程调用栈，或继续用最小化 Overlay 进程测试。

## 5. 下一步排查顺序

1. Overlay 只启动空 WPF 进程，不创建透明窗口；
2. Overlay 创建普通窗口，但不设置 `Topmost`；
3. Overlay 创建透明窗口，但不注册 `WM_NCHITTEST`；
4. 启用窗口后再单独启用 Named Pipe；
5. 最后恢复完整 Overlay。

每一步只验证一个变量。

若任意步骤导致 Unity 未响应，应在卡死时使用 Visual Studio：

```text
Attach to Process → FishSocialDesktop.exe → Break All
```

重点查看主线程是否停在：

- `UnityPlayer.dll`
- `ClientWebSocket`
- `NamedPipeServerStream`
- `steam_api64.dll`
- `user32.dll`
- `curl`

## 6. 验收状态

- [x] Steam 登录
- [x] Socket.IO 连接
- [x] 进入鱼塘不再直接卡死
- [x] 未启动 Overlay 时主程序可退出
- [x] 启动 Overlay 后主程序可稳定退出
- [x] Overlay 开启、关闭、恢复主界面生命周期稳定
- [x] 正常 Release 构建退出验证，且退出时不弹出 CMD

`STEAM-DESKTOP-07G` 已于 2026-08-15 标记为已实现。

## 7. 当前实现审计：五类生命周期问题

基于当前 `NativeOverlayProcessController`、WPF `MainWindow` 和 `DesktopAppBootstrap` 的实现，剩余问题不是服务端鱼塘逻辑，而是 Overlay 进程生命周期、管道关闭和 Unity 退出顺序没有形成单一状态机。

### 7.1 启动 Overlay 后退出仍可能卡死

当前存在以下风险链：

1. `DesktopAppBootstrap.OnDestroy()` 只取消 `CommandReceived` 订阅，没有主动调用 `_nativeOverlay.CloseOverlay()`。
2. `NativeOverlayProcessController.OnDestroy()` 依赖 Unity 的组件销毁顺序，才会异步关闭管道和子进程。
3. `PipeServerLoop()` 可能阻塞在 `WaitForConnection()` 或 `ReadLine()`；仅设置 `_stopping = true` 不会立即解除阻塞。
4. 管道释放、进程 `Kill()`、Writer `Flush()` 都分别排入 ThreadPool，没有统一的关闭顺序，也没有完成确认。
5. `StartQuitWatchdog()` 在 1.5 秒后强制 `Process.Kill()`，可能掩盖真正的资源竞态，并留下 WPF 子进程或管道状态。

因此托管层日志出现 `OnDestroy complete`，不能证明所有原生句柄、后台线程和 Overlay 进程已经结束。

### 7.2 Overlay 隐藏后任务栏没有图标，无法从任务栏清理

WPF 明确配置了：

```xml
ShowInTaskbar="False"
```

并且“隐藏 Overlay”只调用 `Hide()`，不会退出进程。该行为本身符合当前代码，但没有提供可见的托盘入口或进程管理入口，因此用户无法从任务栏判断或清理仍存活的 Overlay 进程。

必须明确区分：

- `hide_overlay`：隐藏窗口，保留进程和管道；
- `quit_overlay`：关闭窗口、断开管道并退出进程；
- 主程序退出：无条件执行 `quit_overlay`。

### 7.3 关闭 Overlay 后再次进塘无法重新打开

关闭 WPF 窗口后，Unity 侧 `_overlayProcess` 仍可能保留一个已经退出的 `Process` 对象。`StartOverlay()` 只判断：

```csharp
if (_overlayProcess != null)
{
    SendLatestState();
    return;
}
```

没有判断 `HasExited`，也没有使用 `EnableRaisingEvents + Exited` 清空进程引用。因此重新进塘时会被误判为 Overlay 已存在，无法重新启动。

### 7.4 退出应用程序后 Overlay 没有一起关闭

当前主程序退出路径只做事件解绑和 watchdog 启动，未在 `OnWantsToQuit` 的第一时间向 Overlay 发送关闭信号，也未同步完成子进程终止确认。若 Unity 没有按预期触发 `NativeOverlayProcessController.OnDestroy()`，WPF Overlay 会继续存活。

正确顺序应为：

```text
停止接受新 Overlay 命令
→ 标记 Closing
→ 关闭管道写入
→ 请求 Overlay 退出
→ 后台等待有限时间
→ 必要时 Kill 子进程
→ 释放管道和 Process
→ 允许 Unity 退出
```

该流程不能依赖 `OnDestroy()` 才开始。

### 7.5 反复进塘后仍可能卡死

`StartOverlay()` 在进程尚未返回并赋值给 `_overlayProcess` 的窗口期，可以被重复调用；每次调用都会再次排入 `ThreadPool` 启动任务，产生多个 WPF 进程竞争同一 Overlay 互斥体和同一管道。

此外，当前每次状态发布都会创建新的 ThreadPool 写任务，WPF 每收到一条状态也会创建一次 `Dispatcher.BeginInvoke`。反复进塘或高频状态变化时，可能形成：

- 启动任务重复；
- 状态写任务堆积；
- UI Dispatcher 消息堆积；
- 旧进程引用残留；
- 关闭和重启同时发生。

需要将 Overlay 控制器改为显式状态机，并对状态消息做“只保留最新状态”的合并，而不是无限排队。

## 8. 建议修复方案

### 8.1 Unity 侧 Overlay 状态机

至少定义：

```text
Stopped → Starting → Running → Hidden
Running/Hidden → Stopping → Stopped
Starting → Stopped（启动失败）
Running → Stopped（子进程异常退出）
```

所有 `StartOverlay()`、`HideOverlay()`、`ShowOverlay()`、`CloseOverlay()` 和主程序退出请求都必须经过同一状态机和锁，禁止重复启动。

### 8.2 单一关闭入口

新增幂等的 `ShutdownOverlay(bool force)`：

- 第一次调用执行关闭；
- 后续调用只返回当前关闭任务；
- 不在 `OnDestroy()` 中重新创建 ThreadPool 关闭流程；
- 退出阶段禁止再发送普通状态；
- 有限等待后强制结束子进程；
- 最终清空 Process、管道、Writer 和状态。

### 8.3 进程退出检测与重新启动

- 设置 `Process.EnableRaisingEvents = true`；
- 订阅 `Exited`；
- 在后台线程中清空已退出的 Process 引用；
- 管道断开不能直接等同于窗口隐藏；
- Overlay 关闭后再次进塘必须能够重新创建管道和进程。

### 8.4 管道写入合并

- 使用单独的 Writer 队列或写线程；
- 状态消息只保留最新一条；
- 命令消息按顺序保留；
- 关闭时停止接收新消息并等待写线程有限时间；
- 禁止每次 `PublishState()` 创建一个新的 ThreadPool 任务。

### 8.5 WPF 侧关闭与隐藏语义

- `hide_overlay` 只隐藏窗口，不退出进程；
- `quit_overlay` 必须触发窗口关闭并退出 WPF Dispatcher；
- WPF 关闭时先停止管道读线程，再释放 Writer 和 Pipe；
- 不依赖任务栏显示 Overlay；
- 如需要用户可见入口，应由主程序托盘菜单管理，而不是依赖 Overlay 任务栏图标。

## 9. 修复提示词

对应开发提示词：

`docs/planning/prompts/steam-desktop-07g-overlay-lifecycle-fix-dev.prompt.md`

## 10. 2026-08-14 生命周期修复实现

已完成代码层修复，待 Release 包内进行运行验收：

- Unity 控制器增加 `Stopped/Starting/Running/Hidden/Stopping` 状态机；
- `StartOverlay()`、隐藏、恢复和关闭经过同一生命周期锁，防止重复启动；
- Overlay 进程启用 `EnableRaisingEvents` 并订阅 `Exited`，异常退出后后台清理旧管道和线程；
- 增加幂等 `ShutdownOverlayAsync()`，关闭时先发送 `quit_overlay`，再后台释放管道、等待进程退出，超时才 Kill；
- 状态消息改为单一后台写线程并只保留最新状态，命令按序发送；
- 主程序在 `OnWantsToQuit` 立即发起 Overlay 关闭，`OnDestroy` 仅重复调用幂等入口；
- 移除固定 1.5 秒正常退出 watchdog，避免用强制 Kill 掩盖生命周期竞态；
- 主程序托盘增加“显示 Overlay”和“退出 Overlay”，与隐藏窗口语义区分；
- WPF 关闭时不在 UI 线程同步 Dispose 管道，命令写入改为后台执行。

当前验收状态：

- [x] Unity 与 WPF 代码编译通过；
- [x] 进入鱼塘启动 Overlay 后主程序稳定退出；
- [x] Overlay 隐藏后可由主程序托盘恢复同一进程；
- [x] 关闭 Overlay 后可重新进入鱼塘启动新进程；
- [x] 重复进塘/离塘进程数稳定，系统中最多一个 Overlay；
- [x] Release 构建退出无未响应，且不弹出 CMD。

## 11. 2026-08-14 23:46 复盘：当前卡死的真实原因

本节覆盖后续多次“退出卡死仍在”的修复尝试。结论与第 7 节生命周期审计不同：生命周期状态机、写线程合并、`HasExited` 移除、C# watchdog、主线程 `Process.Kill()` 都没有打到真正的阻塞点。

### 11.1 最新一次退出的完整证据

`Player.log` 进程 876，Development Player，带托管调试器：

```text
Starting managed debugger on port 56491
[Debug] 1
...
[Shutdown] OnWantsToQuit received.
[NativeOverlay] terminating overlay for application quit.
[Shutdown] quit fallback armed for process 876
[Shutdown] tray cleanup complete.
[Shutdown] DesktopAppBootstrap.OnDestroy complete.
[Shutdown] SteamworksTicketProvider.OnDestroy complete.
[Shutdown] SocialPondSessionController.OnDestroy complete.
[Shutdown] NativeOverlayProcessController.OnDestroy complete.
Curl error 42: Callback aborted
PlayerConnection::CleanupMemory Statistics:
...
##utp: MemoryLeaks  processId=876
```

同时现场现象：

- Unity 窗口已进入未响应；
- Overlay 窗口仍然可右键操作；
- 只有“结束进程”或“Overlay 右键退出”才能让 Unity 真正消失；
- Overlay 退出后 Unity 会自动结束。

这三条同时成立，说明：

1. C# 退出代码已经跑完，卡死不在 `OnWantsToQuit` / `OnDestroy`；
2. `process.Kill()` 返回了且没有抛异常，但 Overlay 进程仍活着；
3. Unity 原生层在等 Overlay 释放某个句柄；Overlay 一退出，等待结束，Unity 才写出 `##utp` 并退出。

### 11.2 真正的卡死点

卡死发生在这段之后：

```text
NativeOverlayProcessController.OnDestroy complete
Curl error 42: Callback aborted
PlayerConnection::CleanupMemory Statistics
```

也就是 Unity **Development Player 的原生卸载**：`PlayerConnection`、托管调试器、Mono 线程回收。它会等待仍卡在 **非托管阻塞 I/O** 里的线程。当前这条 I/O 是 Overlay 的双向 Named Pipe：

```text
Unity PipeServerLoop  →  WaitForConnection() / ReadLine()
WPF PipeLoop          →  Connect() / ReadLine()
```

两端都在内核 `ReadFile` 上阻塞。Mono 无法中止这种线程。Development Player 在 `PlayerConnection` 清理时一直等到这条 I/O 结束。

因此：

- 未启动 Overlay：没有这条管道线程，退出正常；
- Overlay 仍活着：管道不破，Unity 假死；
- 用户关掉 Overlay：WPF `OnClosing` 释放客户端管道，Unity 的 `ReadLine` 返回，原生退出继续。

`Curl error 42` 是 Development Player 断开 PlayerConnection 的伴随日志，不是根因。根因是 **Overlay 管道线程把 Unity 原生退出钉住了**。

### 11.3 为什么现有修复全部无效

| 尝试 | 为什么没用 |
|------|------------|
| Overlay 懒加载 | 只解释了“没开 Overlay 能退”，没有拆掉退出时的管道等待 |
| 生命周期状态机 / 单一写线程 | 修的是重复启动和写堆积，不是原生卸载死锁 |
| 后台 `CloseOverlay` / `ShutdownOverlayAsync` | 托管层确实结束了；卡在托管层之后 |
| 主线程 `Process.Kill()` | 日志已打印 `terminating overlay`，但 Overlay 窗口仍在，说明 Unity Mono 这条 Kill 没有真正结束 WPF 进程 |
| C# Thread watchdog | Unity 进入原生卸载后会中止自己的托管线程，watchdog 来不及执行 |
| `cmd.exe ping & taskkill` | 它是 Unity 的子进程，且 `armed` 只表示准备启动；现场 Unity 没有在约 2 秒后被杀掉，这条兜底没有生效 |
| 去掉 `HasExited` | 主线程 Kill 已经返回，阻塞不在这个查询上 |

核心误判是：把“托管清理完成”当成“进程可以退出”。对 Unity Development Player 来说，Named Pipe 上的阻塞线程会把退出拖死，直到对端进程消失。

### 11.4 正确修复方向

必须同时满足：

1. Overlay 不能作为 Unity 的附属子进程把退出顺序绑死，启动时应脱离当前进程树；
2. 退出时不能依赖 Unity Mono 的 `Process.Kill()`，应使用独立的 `taskkill /F /IM FishSocialOverlay.exe`；
3. Unity 侧必须在进入原生卸载前主动断开 Named Pipe，而不是把 `Dispose` 丢进可能被中止的 ThreadPool；
4. 管道线程必须可被 `Disconnect`/`CancelIo` 唤醒，不能只设 `_stopping`；
5. 验证包必须是非 Development、无 `AllowDebugging` 的 Release Player；当前构建菜单固定 `BuildOptions.Development | AllowDebugging`，会放大 `PlayerConnection` 等待。

在上述 5 点落地前，不得把退出卡死标为已修复。

## 12. 2026-08-14 方案落地

已按第 11.4 节实现：

1. Overlay 通过 `CREATE_BREAKAWAY_FROM_JOB | CREATE_NEW_PROCESS_GROUP | DETACHED_PROCESS` 启动，不再作为 Unity 作业对象内的附属子进程。
2. 退出时不再调用 Unity Mono 的 `Process.Kill()` / `HasExited` / `WaitForExit`；改为独立 `cmd.exe`（`UseShellExecute=true`）执行 `taskkill /F /IM FishSocialOverlay.exe`。
3. 退出主线程只做 `CancelIoEx` 和 50ms dummy connect 唤醒 Named Pipe，不再在主线程 `Dispose` 管道或 `Join` 管道线程。
4. 不再订阅 `Process.Exited` / `EnableRaisingEvents`，避免原生卸载等待子进程句柄。
5. WPF 收到 `quit_overlay` 或菜单退出时直接 `Environment.Exit(0)`。
6. `Fish Social/Build Windows Release + Native Overlay` 改为非 Development、无 `AllowDebugging` 的 Release Player。

验证必须使用该 Release 菜单重新出包，不能只替换 `Assembly-CSharp.dll`，也不能继续用带调试器的 Development Player 验收退出。

## 13. 2026-08-15 验收归档

用户确认退出卡死已完全修复。落地补充：

- Overlay 结束改用隐藏的 `taskkill.exe`，不再经过可见 `cmd.exe`；
- 延迟结束 Unity 只启动一次，使用 `CREATE_NO_WINDOW`，去掉 `DETACHED_PROCESS`。

归档结论：`STEAM-DESKTOP-07G` 状态为 **已实现**，设计时间 2026-08-14，完成时间 2026-08-15。
