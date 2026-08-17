# STEAM-DESKTOP-08I：鱼塘退出、离席、跨塘切换与 Overlay 延迟优化

请只实现 `STEAM-DESKTOP-08I`。

## 必读文档

- `docs/planning/specs/Steam桌面端-08I鱼塘退出与跨塘切换优化.md`
- `docs/planning/specs/Steam桌面端-08A世界地图与鱼塘选择.md`
- `docs/planning/specs/Steam桌面端-08GOverlay钓鱼操作栏.md`
- `fish-social-unity/Assets/Scripts/Desktop/Auth/SocialPondSessionController.cs`
- `fish-social-unity/Assets/Scripts/Desktop/Auth/SocialSocketClient.cs`
- `fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopWorldMapPanel.cs`
- `fish-social-unity/Assets/Scripts/Desktop/NativeOverlayProcessController.cs`
- `desktop-overlay/MainWindow.xaml`
- `desktop-overlay/MainWindow.xaml.cs`
- `desktop-overlay/PondScenePresenter.cs`
- `server/src/socketPondHandlers.ts`
- `server/src/pondSession.ts`
- `server/src/fishingStateMachine.ts`

## 必须实现

### 1. 服务端钓位释放

- 增加 `leave_spot` Socket 事件和服务端状态校验。
- 仅允许当前玩家释放自己的钓位。
- `seated → idle`，清空 `spotId`，广播 `pond_user_updated`。
- 正在钓鱼时不得直接离席，必须先收杆。
- 保留满员和真人钓位不可抢占规则。

### 2. Unity 会话状态机

- 为客户端补齐 `leave_spot` 和 `leave_pond` 调用。
- 增加幂等的 `ExitPond` 流程：
  `stop_fishing → accept_catch → leave_spot → leave_pond`。
- 增加幂等的 `SwitchPond(targetPondId)` 流程：
  清理当前会话后再加入目标鱼塘。
- 流程执行期间禁用相关按钮，禁止重复命令。
- 任一步失败时保留当前会话并回显错误。
- 不重复创建 Socket、Overlay 或 Unity Player。

### 3. Overlay 交互

- 点击可用钓位后直接发送 `take_spot`，不再要求用户二次点击选择按钮。
- 底栏增加「离席」和「退出鱼塘」。
- 退出鱼塘后关闭 Overlay、显示主窗口并回到主页。
- 根据 Unity 推送的 `availableActions` 正确启用/禁用按钮。
- 保留服务端拒绝信息，不能在 Overlay 本地伪造成功。

### 4. 世界地图跨塘切换

- 禁止 `DesktopWorldMapPanel` 直接对已有连接调用 `ConnectAndJoin` 作为切换方案。
- 通过 Unity 会话控制器的 `SwitchPond` 执行清理和重新加入。
- 新鱼塘加入成功后才显示新的 Overlay 场景。

### 5. 延迟优化

- `NativeOverlayProcessController.WriterLoop` 改为事件驱动，移除固定 250ms 等待造成的延迟。
- 命令消息优先于状态消息发送。
- 状态消息合并/节流，避免高频完整序列化。
- Socket WebSocket 接收支持分片拼包。
- 点击后立即显示处理中状态，ACK 后显示最终结果。
- 为点击、Pipe、Unity、Socket ACK、状态回传和 WPF 渲染增加调试时间戳日志。
- 主窗口隐藏但 Overlay 运行时，禁止使用 5 FPS 后台节流；Overlay 关闭/隐藏后才恢复后台帧率。
- Socket ACK、连接状态和错误事件使用高优先级 Unity 主线程队列；普通鱼塘广播每帧限量处理，不能阻塞交互 ACK。
- `NativeOverlayProcessController` 维护持久递增的状态 `sequence`；WPF 丢弃重复或过期序号，避免重复渲染。
- 完全相同的状态应合并或跳过发送，避免高频 `pond_user_updated` 造成 IPC 和渲染压力。
- 日志使用 UTC Unix milliseconds，并关联 Overlay `commandId`、Socket `ackId`、状态 `sequence`。

### 6. 延迟验证

必须从日志计算以下区间：

```text
Overlay command_sent → Unity command_received
Unity command_received → Socket event_sent
Socket event_sent → Socket ack_received
Socket ack_received → Unity state_queued
Unity state_queued → Overlay state_received
```

目标：

- 正常网络按钮反馈 P50 ≤ 100ms；
- P95 ≤ 300ms；
- ACK 超时率为 0；
- 高密度鱼塘状态广播下不得出现持续队列积压；
- 服务端 `stopping → seated` 的 200ms 状态阶段不计入 ACK 延迟。

## 禁止

- 不修改鱼塘容量、钓鱼概率、每日时长和鱼获结算规则。
- 不绕过服务端判断来释放钓位或领取鱼获。
- 不在 Overlay 内绘制世界地图、商店或其他主窗口页面。
- 不启动第二个 Unity Player。
- 不用直接断开 Socket 代替完整的 `leave_pond` 事务。
- 不以“重启服务端”或“重新打包”替代客户端性能修复。

## 验收

- Overlay 点击钓位直接入座。
- 满员或钓位冲突仍正确拒绝。
- 离席后钓位释放且其他玩家可以使用。
- 退出鱼塘完成收杆、领鱼、离席、离塘、关闭 Overlay、返回主页。
- 在钓位中切换世界地图鱼塘，旧塘清理完成后进入新塘。
- 不重复创建 Socket/Overlay。
- 正常网络下无固定 250ms 级按钮等待。
- 失败、超时和重复点击不会造成卡死。

