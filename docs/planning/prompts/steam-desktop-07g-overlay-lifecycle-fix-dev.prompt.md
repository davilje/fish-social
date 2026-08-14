# STEAM-DESKTOP-07G：Overlay 生命周期与重复启动修复提示词

## 任务

根据以下排查记录，修复 Native WPF Overlay 的生命周期问题：

`docs/planning/reports/STEAM-DESKTOP-07G-Overlay卡死排查记录-2026-08-14.md`

需要解决：

1. 启动 Overlay 后关闭主程序仍可能卡死；
2. Overlay 隐藏后没有任务栏入口，进程无法明确清理；
3. 关闭 Overlay 后再次进塘无法重新打开；
4. 主程序退出时 Overlay 没有一起退出；
5. 反复进塘或重复点击后启动多个 Overlay、状态堆积并再次卡死。

## 必读代码

- `fish-social-unity/Assets/Scripts/Desktop/NativeOverlayProcessController.cs`
- `fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShellUi.cs`
- `desktop-overlay/MainWindow.xaml`
- `desktop-overlay/MainWindow.xaml.cs`
- `desktop-overlay/App.xaml.cs`
- `desktop-overlay/IpcProtocol.cs`

## 强制约束

- Overlay 仍是独立 WPF 进程，不得改回第二个 Unity Player。
- Unity 主程序仍是 Steam、Socket、鱼塘和库存状态唯一权威。
- 不得在 Unity 主线程执行阻塞式 `WaitForExit()`、管道 `Connect()`、管道 `ReadLine()` 或长时间 `Flush()`。
- 所有关闭接口必须幂等，可被 `OnWantsToQuit`、`OnDestroy`、托盘退出和 Overlay 异常退出重复调用。
- 不得使用固定 watchdog 直接作为正常关闭机制；强制 Kill 只能作为有限超时后的兜底。
- 不得把“隐藏窗口”当成“进程已退出”。

## Unity 侧实现要求

### 1. 显式生命周期状态机

至少实现：

```text
Stopped
Starting
Running
Hidden
Stopping
```

状态转换必须串行化：

```text
Stopped -> Starting -> Running
Running -> Hidden
Hidden -> Running
Running/Hidden -> Stopping -> Stopped
Starting -> Stopped
Running -> Stopped（进程异常退出）
```

重复进入鱼塘时：

- `Starting` 状态不得再次启动进程；
- `Running` 状态只发送最新状态；
- `Hidden` 状态发送 `show_overlay`；
- `Stopping` 状态不得接受新的启动请求，关闭完成后再允许启动。

### 2. 进程退出检测

- 设置 `Process.EnableRaisingEvents = true`；
- 订阅 `Process.Exited`；
- 进程退出后在后台安全清空引用；
- 清理旧进程、旧 Writer 和旧管道后，允许下一次重新启动；
- 不要在 Unity `Update()` 中每帧访问 `Process.HasExited`。

### 3. 单一关闭入口

实现类似：

```csharp
Task ShutdownOverlayAsync(bool forceAfterTimeout)
```

要求：

- 多次调用返回同一个关闭任务；
- 停止接收新的状态发送；
- 先发送 `quit_overlay`；
- 关闭管道写端和服务器；
- 后台等待 Overlay 进程退出；
- 超时后才 Kill；
- 最终 Dispose Process、Pipe、Writer；
- Unity 主线程只发起关闭，不等待阻塞结果。

主程序必须在 `OnWantsToQuit` 中立即调用关闭入口，不能只依赖 `OnDestroy()`。

### 4. 启动竞态

`StartOverlay()` 必须防止以下情况：

- 进程尚未赋值时重复点击；
- `Starting` 状态重复排入 ThreadPool；
- 关闭与启动同时执行；
- 已退出的 Process 对象被误认为仍在运行。

可以使用锁、`Task`、`Interlocked` 或明确的启动任务引用，但必须保证同一时刻最多一个启动任务。

### 5. IPC 写入背压

- 使用单一写线程或单一异步写循环；
- 状态消息只保留最新一条；
- 命令消息按序发送；
- 禁止每次 `PublishState()` 创建一个 ThreadPool 写任务；
- 断管道时清空或失效旧 Writer；
- 关闭后禁止继续排入状态写入。

## WPF 侧实现要求

### 隐藏

`hide_overlay`：

- 只隐藏窗口；
- 保留进程和管道；
- 后续 `show_overlay` 可以恢复；
- 不要求出现在 Windows 任务栏。

### 退出

`quit_overlay`：

- 停止接收新消息；
- 关闭管道读写；
- 触发窗口关闭；
- 退出 WPF Dispatcher；
- 确保进程最终结束。

WPF 关闭事件不得无限等待 Unity 或 Named Pipe。

### 任务栏语义

当前 `ShowInTaskbar=False` 可以保留，但必须：

- 在主程序托盘菜单提供“显示 Overlay”和“退出 Overlay”；
- 明确区分“隐藏窗口”和“终止进程”；
- 不把任务栏图标作为唯一清理入口。

## 验收测试

### A. 启动与退出

1. 启动 Unity，不启动 Overlay，直接退出；
2. 进入鱼塘启动 Overlay，退出主程序；
3. 检查 Unity 和 WPF 进程均退出；
4. 重复执行 20 次，不能出现未响应。

### B. 隐藏与恢复

1. 启动 Overlay；
2. 选择“隐藏 Overlay”；
3. 确认进程仍存在但窗口隐藏；
4. 从 Unity 托盘执行“显示 Overlay”；
5. 确认同一进程恢复，不创建第二个 Overlay。

### C. 关闭与重新启动

1. 关闭 Overlay 窗口；
2. 再次点击进入鱼塘；
3. 确认可以重新启动新的 Overlay；
4. 确认旧 Pipe 和旧 Process 引用已清理。

### D. 重复进入鱼塘

1. 连续点击进入/恢复鱼塘 20 次；
2. 反复离塘、进塘 20 次；
3. 确认系统中最多一个 `FishSocialOverlay.exe`；
4. 确认 Unity 主窗口始终可点击、拖动和缩放；
5. 确认 CPU、内存和 Pipe 线程数量不会持续增长。

### E. 异常退出

1. 强制结束 WPF Overlay；
2. 确认 Unity 不冻结；
3. 再次启动 Overlay；
4. 强制结束 Unity；
5. 确认 Overlay 不残留。

## 交付物

- Unity Overlay 生命周期控制器；
- WPF Overlay 关闭/隐藏修复；
- Named Pipe 写入背压与断线处理；
- 退出顺序日志；
- 进程数量、CPU、内存测试记录；
- 更新后的 07G 排查记录和验收结果。
