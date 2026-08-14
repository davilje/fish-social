# STEAM-DESKTOP-07C：同塘玩家宠物与状态同步

请在 `fish-social-unity/` 内实现同一鱼塘内其他玩家宠物的表现和状态同步，主要表现于 `960×480` 原生 Overlay。

## 必读

- `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md`
- `fish-social-unity/Assets/Scripts/Desktop/Social/SocialPondSessionController.cs`
- `fish-social-unity/Assets/Scripts/Desktop/Social/SocialLobbyController.cs`

## 范围

- 处理 `pond_user_joined`、`pond_user_left`、`pond_user_updated`。
- 显示同塘玩家宠物形象、昵称和基础钓鱼状态。
- 其他玩家猫咪基准尺寸为 `128×128`，与自己的猫使用统一表现接口。
- Overlay 中至少显示场景、自己的猫和同塘其他用户；不得因主窗口隐藏而停止状态同步。
- 为自己和其他玩家使用统一的宠物表现接口。
- 玩家离开时清理表现对象，重复事件不得产生重复对象。

## 边界

- 数据必须来自服务端快照和 Socket 事件。
- 不在 Unity 本地伪造在线玩家、鱼塘状态或钓鱼结果。
- 不修改 Lobby 权限和 Node 鱼塘权威逻辑。
- 不重写 07G 的窗口、进程和 IPC 生命周期，只使用其状态传输接口。

## 验收

- 两个玩家进入同一鱼塘时能互相看到宠物。
- 进出场和钓鱼状态变化能正确同步。
- 断线重连后不会残留旧玩家对象。
- 同塘玩家显示不影响自己的钓鱼会话。
- 主窗口与 Overlay 切换时玩家对象不重复创建、不残留。
