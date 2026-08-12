# 开发交接提示词：Steam 好友、Lobby、邀请与鱼塘映射（STEAM-DESKTOP-03）

你是 Fish Social 的后端 + Unity 社交功能工程师。请在已完成 Steam 登录、JWT、Socket 和鱼塘会话的基础上，实现 `STEAM-DESKTOP-03`。

## 必读

1. `docs/planning/specs/Steam好友Lobby邀请与鱼塘映射.md`
2. `docs/planning/specs/Steam身份账号绑定与安全会话.md`
3. `docs/planning/specs/Steam桌面端产品定位与信息架构.md`
4. `docs/planning/specs/Unity移植-分阶段需求清单.md`
5. `fish-social-unity/Assets/Scripts/Desktop/Auth/`
6. `fish-social-unity/Assets/Scripts/Desktop/UI/`
7. `server/src/` 的 auth、pond、Socket 和现有玩家权限代码

## 当前基线

- `STEAM-DESKTOP-04` 桌面壳已实现。
- `STEAM-DESKTOP-02` Steam 登录、JWT REST、Socket、进塘和钓鱼闭环已验收。
- `UNITY-P1/P2` 契约与网络薄客户端已实现。
- 当前允许继续使用 Node Socket.IO。
- 当前不引入 Steam Networking/Relay。
- Lobby 仅负责社交发现、邀请和 `pondId` 映射；鱼塘由 Node + SQLite 持久化维护。
- 房主关闭或离开 Lobby 不得关闭鱼塘；无玩家时由 STEAM-DESKTOP-05 的离线生态逻辑继续积累。

## 产品与 UI 原则

本阶段使用简化版功能 UI，不制作最终美术界面：

- 好友列表
- 在线/离线状态
- 创建 Lobby
- 加入 Lobby
- 邀请好友
- Lobby 成员
- 当前 pondId 和进入状态
- 错误、空列表、拒绝和版本不兼容提示

UI 只负责显示状态和发出命令，不能直接调用 Steam API 或决定玩家权限。

必须按以下边界实现：

```text
Steam/Lobby Adapter
        ↓
SocialLobbyController
        ↓
LobbyViewModel / 状态事件
        ↓
FriendsPanel / LobbyPanel
```

后续正式 UI 只替换 Panel 和 View，不重写 Adapter、Controller、协议和服务端权限。

## 后端任务

1. 确认 Lobby 创建、加入、邀请所需的服务端接口边界。
2. Lobby 元数据只允许包含：

```text
pondId
gameVersion
protocolVersion
```

3. Node 校验 SteamID64、playerId、Lobby、pondId 和版本兼容性。
4. 不信任客户端自报身份或权限。
5. 不把库存、鱼获、金币和鱼塘权威状态写入 Steam Lobby。
6. 为 Lobby 不存在、无权限、版本不兼容和鱼塘不可用返回稳定错误码。
7. 增加服务端权限、重复加入和邀请失效测试。

## 关键返工：Lobby 与 pond 生命周期解耦

当前 `server/src/socialRoutes.ts` 的 `socialLobbies` 内存记录只能表示临时 Lobby 入口，不能绑定鱼塘生命周期。不得按“房主离开即关闭鱼塘”的方式实现。

必须满足：

- Lobby 关闭不删除鱼塘。
- Lobby 房主离开不关闭鱼塘。
- Lobby 失效只影响新的 Lobby 加入，不影响已有鱼塘。
- `pondId` 是持久化鱼塘实体，不是 Lobby 的临时资产。
- 最后一个玩家离开后，鱼塘进入休眠，停止高频 Tick。
- 鱼群成长、迁移和补充通过 `last_simulated_at` 继续离线积累。
- 玩家从世界地图、好友入口或新的 Lobby 重新进入同一 `pondId` 时，先执行幂等离线补算，再恢复实时 Tick。
- 不为每个鱼塘创建常驻服务器进程。

### 服务端必须调整

1. `close lobby` 只使 Lobby 入口失效，不删除 `pondId` 对应鱼塘。
2. 不以 `ownerPlayerId` 的离开作为鱼塘关闭条件。
3. 将 Lobby 失效、鱼塘不存在、玩家无权限和离线补算错误分开。
4. 进入鱼塘接口以持久化 `pondId` 和 Node 权限为准，不能要求 Lobby 永久存在。
5. 增加以下回归测试：
   - 房主离开后其他玩家仍可留在鱼塘。
   - Lobby 关闭后鱼塘仍可从地图进入。
   - 最后一个玩家离开后触发休眠。
   - 重新进入同一 `pondId` 触发一次幂等离线补算。
   - 重新创建 Lobby 后可重新映射到原 `pondId`。

## Unity 任务

1. 使用 Steamworks.NET 接入好友列表、在线状态、Lobby 创建/加入和邀请。
2. 新增独立 `SocialLobbyController`，不要把业务逻辑写入 `DesktopShellUi`。
3. 新增可替换的 `FriendsPanel` 和 `LobbyPanel` 简化 UI。
4. Lobby 创建成功后写入当前 `pondId`。
5. 收到邀请后读取 Lobby 元数据，并通过 Node 权限接口进入鱼塘。
6. 使用已有 `SocialPondSessionController` 进入 Socket 鱼塘会话。
7. 处理 Steam 未运行、好友为空、邀请失败、Lobby 失效和服务端拒绝。
8. 状态必须可观察：

```text
未登录
→ 好友加载中
→ Lobby 创建中
→ 等待邀请
→ Lobby 已加入
→ 进入鱼塘中
→ 已进入鱼塘
→ 失败/重试
```

9. 区分“离开 Lobby”和“离开鱼塘”：成员离开 Steam Lobby 不得自动断开
   `SocialPondSessionController`；房主关闭 Lobby 只撤销后续邀请。

## 禁止事项

- 不做最终视觉稿和复杂动效。
- 不把好友/Lobby 逻辑散落在多个按钮回调中。
- 不用客户端自报 `playerId` 或 `pondId` 绕过服务端权限。
- 不把 Steam Lobby 当作业务数据库。
- 不实现 Steam Networking/Relay。
- 不提交 Web API Key、Ticket、JWT 或真实玩家数据。
- 不用本地模拟成功替代真实 Steam 双账号验收。

## 验收

至少完成：

1. 测试账号 A 创建 Lobby。
2. Lobby 写入正确 `pondId`。
3. 测试账号 A 邀请测试账号 B。
4. 测试账号 B 接受邀请并加入 Lobby。
5. B 读取正确 `pondId`。
6. A/B 都通过 Node 权限校验并进入同一鱼塘。
7. 任一方无权限、版本错误或 Lobby 失效时被拒绝。
8. 简化 UI 可以被替换，业务层测试不依赖具体视觉布局。

## 交付

- Unity Adapter、Controller、ViewModel、简化 UI。
- Node 接口、权限校验和测试。
- 双 Steam 账号真实联调记录。
- 运行 `npm run planning:verify -- v1.0-steam-desktop` 或专项验证。
- 验收通过后回写 `STEAM-DESKTOP-03` 状态和计划看板。

建议角色：`@frontend-dev` 主责 Unity，`@backend-dev` 主责服务端，联调后由主 Agent 验收。
