# STEAM-DESKTOP-07C：同塘玩家宠物与状态同步

> 状态：**已实现**（用户验收 2026-08-15）

请在 `fish-social-unity/` 内实现同一鱼塘内其他玩家宠物的表现和状态同步，主要表现于已有的 `960×480` 原生 Overlay。
本任务只运行一个 Unity 主程序；严禁启动第二个 Unity Player。
Overlay 不连接 Socket；网络仍由 Unity 现有会话承担。

## 必读

- `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md`（§1.1 与 web 差异、§4.1 Overlay 本地序列帧、§4.2 同步模型）
- `docs/planning/specs/Steam原生桌面宠物Overlay.md`
- `fish-social-unity/Assets/Scripts/Desktop/Auth/SocialPondSessionController.cs`
- `fish-social-unity/Assets/Scripts/Desktop/Social/SocialLobbyController.cs`

## 与 web / RN 的关系

- 权威事件不变：`pond_snapshot`、`pond_user_joined`、`pond_user_left`、`pond_user_updated`。
- 不新开网络协议，不把 Steam Lobby 当鱼塘权威。
- 表现不同：Overlay `128×128` 序列帧宠物 + 昵称，而不是 RN 头像二头身。

## 范围

- Unity 处理 `pond_user_joined`、`pond_user_left`、`pond_user_updated`，并把同塘用户列表推进 Overlay 状态 DTO。
- Overlay 显示同塘玩家宠物形象、昵称和基础钓鱼状态。
- 自己和其他玩家使用统一的宠物表现接口：同一 `petVisualState` 枚举、同一 `128×128` 基准、同一套 **Overlay 本地序列帧** 渲染器。
- Unity 将服务端 `fishingPhase` 映射为 `petVisualState` 后下发；Overlay 按状态切本地帧，**IPC 不传图片或逐帧数据**。
- 主窗口隐藏时 Overlay 继续播帧、继续收 Unity 推送的状态；不得因切主窗口而停止同步或重建会话。
- 玩家离开时清理表现对象；按 `playerId` 复用，重复事件不得产生重复对象。
- 断线重连以 `pond_snapshot` 全量覆盖角色列表。

## 边界

- 数据必须来自服务端快照和 Socket 事件。
- 不在 Unity 本地伪造在线玩家、鱼塘状态或钓鱼结果。
- 不修改 Lobby 权限和 Node 鱼塘权威逻辑。
- 不重写 07G 的窗口、进程和 IPC 生命周期，只扩展状态载荷。
- 不修改 `mobile/`、`server/`、`shared/`。

## 验收

- 两个玩家进入同一鱼塘时能互相看到宠物。
- 进出场和钓鱼状态变化能正确同步，Overlay 序列帧随 `petVisualState` 切换。
- 断线重连后不会残留旧玩家对象。
- 同塘玩家显示不影响自己的钓鱼会话。
- 主窗口与 Overlay 切换时玩家对象不重复创建、不残留。
