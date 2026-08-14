# STEAM-DESKTOP-07C：同塘玩家宠物与状态同步

请在 `fish-social-unity/` 内实现同一鱼塘内其他玩家宠物的表现和状态同步。

## 必读

- `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md`
- `fish-social-unity/Assets/Scripts/Desktop/Social/SocialPondSessionController.cs`
- `fish-social-unity/Assets/Scripts/Desktop/Social/SocialLobbyController.cs`

## 范围

- 处理 `pond_user_joined`、`pond_user_left`、`pond_user_updated`。
- 显示同塘玩家宠物形象、昵称和基础钓鱼状态。
- 为自己和其他玩家使用统一的宠物表现接口。
- 玩家离开时清理表现对象，重复事件不得产生重复对象。

## 边界

- 数据必须来自服务端快照和 Socket 事件。
- 不在 Unity 本地伪造在线玩家、鱼塘状态或钓鱼结果。
- 不修改 Lobby 权限和 Node 鱼塘权威逻辑。

## 验收

- 两个玩家进入同一鱼塘时能互相看到宠物。
- 进出场和钓鱼状态变化能正确同步。
- 断线重连后不会残留旧玩家对象。
- 同塘玩家显示不影响自己的钓鱼会话。
