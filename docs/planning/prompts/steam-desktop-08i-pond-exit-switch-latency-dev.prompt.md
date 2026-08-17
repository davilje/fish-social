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

## 禁止

- 不修改鱼塘容量、钓鱼概率、每日时长和鱼获结算规则。
- 不绕过服务端判断来释放钓位或领取鱼获。
- 不在 Overlay 内绘制世界地图、商店或其他主窗口页面。
- 不启动第二个 Unity Player。
- 不用直接断开 Socket 代替完整的 `leave_pond` 事务。

## 验收

- Overlay 点击钓位直接入座。
- 满员或钓位冲突仍正确拒绝。
- 离席后钓位释放且其他玩家可以使用。
- 退出鱼塘完成收杆、领鱼、离席、离塘、关闭 Overlay、返回主页。
- 在钓位中切换世界地图鱼塘，旧塘清理完成后进入新塘。
- 不重复创建 Socket/Overlay。
- 正常网络下无固定 250ms 级按钮等待。
- 失败、超时和重复点击不会造成卡死。

