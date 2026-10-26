# STEAM-DESKTOP-08I：鱼塘退出与跨塘切换优化

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-08I` |
| 类型 | Bug 修复 / 功能优化 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P0 |
| 设计时间 | **2026-08-18** |
| 依赖 | 08A、08G |

## 1. 背景与目标

当前进入鱼塘后，Overlay 缺少可靠的退出鱼塘和跨塘切换流程；点击场景钓位只会选中钓位，必须再点击底部按钮；Overlay 操作经过 Named Pipe、Unity 主线程、Socket ACK 和状态回传，反馈延迟明显。

本需求统一鱼塘会话、钓位和 Overlay 生命周期，确保退出、离席、跨塘切换具有明确的服务端确认，并降低操作反馈延迟。

## 2. 功能范围

### 2.1 Overlay 交互

- 点击场景中的可用钓位后，直接发送 `take_spot`，成功后角色移动到该钓位。
- 鱼塘满员、真人钓位已占用等拒绝规则继续由服务端决定，不在客户端绕过。
- 底部操作栏新增「离席」按钮。
- 底部操作栏新增「退出鱼塘」按钮。
- 「离席」只释放当前钓位，不退出鱼塘。
- 「退出鱼塘」关闭 Overlay 并返回主窗口主页。

### 2.2 退出鱼塘事务

退出鱼塘必须按服务端结果顺序执行：

```text
收杆（若正在钓鱼）
→ 领取待领取鱼获（若存在）
→ 离席（若仍在钓位）
→ leave_pond
→ 关闭 Overlay
→ 显示主窗口主页
```

任何步骤失败时停止后续步骤，保留当前会话并显示错误；按钮必须防重复点击。

### 2.3 跨鱼塘切换事务

从世界地图选择其他鱼塘时：

```text
收杆 → 领取鱼获 → 离席 → 离开当前鱼塘
→ 关闭旧 Overlay
→ 加入目标鱼塘
→ 打开新 Overlay
```

不得直接对已连接 Socket 重复调用 `ConnectAndJoin`，不得重复创建 Socket 或 Overlay 进程。

### 2.4 反馈延迟优化

- Overlay 命令优先于状态消息写入 Named Pipe。
- 移除 Writer Loop 的 250ms 轮询等待，改为事件驱动发送。
- 点击后立即显示「处理中」，操作完成或失败后更新结果。
- 对状态消息做节流和合并，避免每次事件发送完整场景状态。
- Socket 接收正确处理 WebSocket 分片。
- Overlay 显示期间不得因为主窗口隐藏而降至 5 FPS；Overlay 关闭或隐藏后才恢复后台帧率节流。
- Socket ACK、连接状态和错误消息必须优先于普通鱼塘广播进入 Unity 主线程队列。
- 状态序号由 Overlay 控制器持久递增；接收端丢弃重复/过期状态，避免重复渲染。
- 记录点击、Pipe 收到、Unity 执行、服务端 ACK、状态回传和 Overlay 渲染时间戳，便于验证延迟。
- 延迟日志统一使用 UTC Unix milliseconds，并可关联 `commandId`、`ackId` 和 `sequence`。

### 2.5 延迟证据与目标

必须分别记录以下节点：

```text
Overlay command_sent
Unity overlay_command_received
Socket event_sent
Server event_received / state_changed
Socket ack_received
Unity state_queued
Overlay state_received
```

验收时区分 IPC、Unity 帧/队列、Socket/服务端和 Overlay 渲染耗时；不得将所有等待归因于服务端。正常网络下按钮反馈目标为 P50 ≤ 100ms、P95 ≤ 300ms，ACK 超时率为 0；服务端阶段性 200ms 状态切换不计入按钮 ACK 延迟。

## 3. 技术影响

### 3.1 Socket / 服务端

| 事件 | 说明 |
|---|---|
| `leave_spot` | 释放钓位，`seated → idle`，清空 `spotId`，广播用户状态 |
| `leave_pond` | 退出当前鱼塘；沿用现有服务端事件并补齐 Unity 客户端调用 |

`leave_spot` 不得绕过服务端容量和状态校验。正在钓鱼时应先完成收杆。

### 3.2 预估涉及文件

- `server/src/socketPondHandlers.ts`
- `server/src/pondSession.ts`
- `server/src/fishingStateMachine.ts`
- `fish-social-unity/Assets/Scripts/Desktop/Auth/SocialSocketClient.cs`
- `fish-social-unity/Assets/Scripts/Desktop/Auth/SocialPondSessionController.cs`
- `fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopWorldMapPanel.cs`
- `fish-social-unity/Assets/Scripts/Desktop/NativeOverlayProcessController.cs`
- `desktop-overlay/MainWindow.xaml`
- `desktop-overlay/MainWindow.xaml.cs`
- `desktop-overlay/PondScenePresenter.cs`

## 4. 非目标

- 不修改鱼塘容量、真人与机器人钓位竞争规则。
- 不修改钓鱼概率、每日时长和鱼获结算规则。
- 不在 Overlay 内实现商店、世界地图或其他完整页面。
- 不重复创建 Unity Player、Socket 或 Overlay 进程。
- 不通过客户端本地状态伪造离席、收杆或鱼获领取成功。

## 5. 验收标准

- [x] Overlay 点击可用钓位后直接入座，不需要再次点击底部选择按钮。
- [x] 鱼塘满员或钓位不可用时仍由服务端拒绝，并显示错误。
- [x] 「离席」可释放钓位，角色回到 idle，其他玩家可使用该钓位。
- [x] 「退出鱼塘」能完成收杆、领取鱼获、离席、leave_pond、关闭 Overlay、返回主页。
- [x] 在钓位中打开世界地图并选择其他鱼塘时，旧鱼塘完成清理后才能进入新鱼塘。
- [x] 跨塘切换不重复创建 Socket/Overlay，且新 Overlay 显示目标鱼塘。
- [x] 所有按钮有立即的处理中反馈，不会因重复点击产生重复命令。
- [x] 正常网络下 Overlay 操作反馈 P50 ≤ 100ms、P95 ≤ 300ms，不再出现固定 250ms 级等待。
- [x] Overlay 显示时 Unity 保持交互帧率，主窗口隐藏不会导致命令 ACK 排队。
- [x] 状态 `sequence` 持久递增，重复或过期状态不会触发重复渲染。
- [x] 日志可关联 Overlay `commandId`、Socket `ackId` 和状态 `sequence`。
- [x] 网络失败、ACK 超时和服务端拒绝不会造成卡死或错误关闭当前会话。

## 6. 变更记录

| 日期 | 作者 | 变更 |
|---|---|---|
| 2026-08-18 | 策划 | 新增鱼塘退出、离席、跨塘切换和 Overlay 延迟优化需求 |
| 2026-08-18 | 开发交接 | 根据 Player.log 与 Overlay 延迟日志补充 5 FPS 后台节流、ACK 队列优先级、状态序号持久化和 P50/P95 验收指标 |
| 2026-08-18 | 用户验收 | 退出鱼塘、离席、跨塘切换及 Overlay 延迟优化验证通过，状态改为已实现 |
