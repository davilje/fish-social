# Steam 好友、Lobby、邀请与鱼塘映射

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Steam 好友、Lobby、邀请与鱼塘映射 |
| 编号 | **STEAM-DESKTOP-03** |
| 负责人 | 后端 + Unity 工程师 |
| 状态 | **已实现**（核心功能已验证；双 Steam 账号联调因缺少第二测试账号跳过） |
| 目标版本 | v1.0-steam-desktop |
| 设计时间 | **2026-08-12** |
| 上位规格 | [`Steam桌面端独立游戏转型计划.md`](./Steam桌面端独立游戏转型计划.md) |
| 前置需求 | `STEAM-DESKTOP-02`、`UNITY-P1`、`UNITY-P2` |

---

## 1. 目标

在已有 Steam 登录、JWT、Socket 和鱼塘会话基础上，增加好友发现、Lobby 创建/加入、邀请和 `pondId` 映射能力。

本需求优先验证社交链路和权限边界，不制作最终美术 UI，也不重做鱼塘场景。

标准流程：

```text
Steam 好友
  → 创建或加入 Lobby
  → Lobby 保存 pondId
  → Unity 请求 Node 进入鱼塘
  → Node 校验玩家权限
  → Socket 加入对应 pond
```

---

## 2. 范围

### 2.1 Steam/服务端

- 获取 Steam 好友列表和在线状态。
- 创建 Lobby。
- 加入 Lobby。
- 读取和写入 Lobby 的 `pondId` 元数据。
- 发送和接受 Steam 邀请。
- 邀请打开后进入对应鱼塘。
- Node 校验 Lobby、玩家和 `pondId` 的关系。
- Steam Lobby 只做发现、邀请和房间元数据，不作为玩家数据权威。

### 2.2 Unity 功能 UI

先实现简化版功能 UI：

- 好友入口。
- 好友列表。
- 在线/离线状态。
- 创建 Lobby。
- 加入 Lobby。
- 邀请好友。
- 当前 Lobby 成员。
- 当前鱼塘和进入状态。
- 错误、空列表、邀请失败和权限拒绝提示。

简化版只使用运行时 UGUI、文本、按钮和临时列表，不要求正式图标、美术资源或最终布局。

### 2.3 UI 解耦

业务逻辑必须独立于视觉 UI：

```text
Steam/Lobby Adapter
        ↓
SocialLobbyController
        ↓
LobbyViewModel / 状态事件
        ↓
FriendsPanel / LobbyPanel
```

- UI 不直接调用 Steam 原生 API。
- UI 不决定玩家权限。
- UI 不保存 `pondId` 之外的权威业务数据。
- 后续正式 UI 只替换 Panel 和 View，不重写 Lobby/权限逻辑。
- 文案、颜色、坐标和图标不得成为业务判断条件。

---

## 2.4 Lobby 与鱼塘生命周期边界

Lobby 只是 Steam 社交发现、邀请和短期访问授权，不是鱼塘实体，也不是鱼塘运行容器。

- 创建者关闭 Lobby，只撤销后续加入权限，不删除或关闭对应 `pondId`。
- 成员离开 Lobby，不等于离开鱼塘；鱼塘会话由 Node Socket 会话独立管理。
- 创建者断线、退出游戏或 Steam Lobby 失效，不得删除鱼塘数据。
- 最后一个玩家离开鱼塘后，鱼塘进入空闲休眠；鱼群成长、迁移和数量补充继续由服务端离线补算。
- 任意玩家重新进入同一 `pondId` 时，Node 先按 `lastSimulatedAt` 完成幂等补算，再恢复实时 Tick。
- Lobby 失效后，玩家仍可通过世界地图或其他合法入口进入持久化的 `pondId`。

```text
Steam Lobby（临时社交入口）
              ↓ pondId 映射
Node + SQLite（持久化鱼塘权威）
  在线玩家 → 实时 Tick
  无在线玩家 → 休眠 + 离线生态补算
```

---

## 3. 非目标

- 不实现 Steam Networking/Relay。
- 不替换现有 Node Socket.IO。
- 不制作最终好友/Lobby 美术稿。
- 不重做鱼塘地图、角色、动画和钓鱼玩法。
- 不把 Steam Lobby 当作库存、鱼获、金币或鱼塘状态数据库。
- 不绕过 Node 直接凭客户端 `pondId` 进入鱼塘。
- 不实现完整聊天系统；只保留入口和后续扩展接口。

---

## 4. 数据与安全边界

建议 Lobby 元数据只保存：

```text
pondId
gameVersion
protocolVersion
```

服务端必须再次校验：

- SteamID64 是否已绑定当前 `playerId`。
- Lobby 是否属于当前游戏 AppID。
- Lobby 中的 `pondId` 是否存在。
- 玩家是否有权进入该鱼塘。
- 版本和协议是否兼容。

客户端不得信任邀请参数中的玩家身份、鱼获、库存或权限。

---

## 5. Lobby 与鱼塘生命周期

### 5.1 权威关系

```text
Steam Lobby = 临时社交入口 / 邀请关系
pondId      = 持久化鱼塘实体
Node/DB     = 鱼塘、玩家和生态权威
```

Lobby 不能成为进入鱼塘的唯一条件。玩家还应能通过世界地图、好友入口或新的 Lobby 重新进入同一个 `pondId`。

### 5.2 生命周期规则

- Lobby 关闭不删除鱼塘。
- Lobby 房主离开不关闭鱼塘。
- Lobby 失效只影响新的 Lobby 加入，不影响已经进入鱼塘的玩家。
- 最后一个玩家离开后，鱼塘进入空闲休眠。
- 空鱼塘不运行高频实时 Tick，但鱼群成长、迁移和补充继续按离线时间积累。
- 任意玩家重新进入同一个 `pondId` 时，Node 先按 `last_simulated_at` 执行幂等离线补算，再恢复实时 Tick。
- 不为每个鱼塘创建常驻服务器进程。

### 5.3 当前实现整改

当前 `socialLobbies` 内存 Map 只能作为临时 Lobby 入口记录，不能承担鱼塘生命周期或玩家资产权限。需要：

- 移除“房主关闭/离开即使鱼塘失效”的隐含语义。
- `close lobby` 只删除或失效 Lobby 入口，不删除 `pondId`。
- 进入鱼塘时以持久化 `pondId` 和 Node 权限为准。
- 将 Lobby 失效、鱼塘不存在、无权限和离线补算错误分开返回。
- 为“Lobby 关闭后重新从地图进入同一鱼塘”增加测试。

---

## 6. 验收标准

- [x] Unity 可查看好友列表和在线状态。
- [x] 可创建 Lobby 并写入 `pondId`。
- [x] 好友可通过邀请加入 Lobby。
- [x] 加入 Lobby 后能读取正确的 `pondId`。
- [x] Node 能校验 Lobby、玩家和鱼塘权限，并返回权限拒绝。
- [x] 单账号/本地模拟链路可进入对应 Node 鱼塘 Socket 会话；双 Steam 账号联调因缺少第二测试账号跳过。
- [x] 关闭 Lobby、鱼塘不存在、版本不兼容和无权限时有明确提示。
- [x] 简化版 UI 可替换，不影响 Controller、Adapter 和服务端协议。
- [x] 不使用 Steam Networking/Relay。
- [x] 不把权威玩家数据写入 Steam Lobby。
- [x] 真实 Steam 测试与本地模拟测试分开记录。
- [x] Lobby 关闭不删除鱼塘。
- [x] 房主离开不关闭鱼塘。
- [x] Lobby 失效后，已有鱼塘仍可从地图/好友入口进入。
- [x] 最后一个玩家离开后，鱼塘进入休眠并停止高频 Tick。
- [x] 重新进入同一 `pondId` 前，按 `last_simulated_at` 完成幂等补算。
- [x] 不创建每鱼塘常驻服务器进程。

---

## 7. 推荐实现顺序

```text
1. 修正 socialLobbies 与 pond 生命周期边界
2. 冻结 Lobby 元数据和 Node 权限接口
3. Unity Steam 好友/Lobby Adapter
4. SocialLobbyController
5. 简化版 FriendsPanel/LobbyPanel
6. Lobby → pondId → Socket 进塘联调
7. 休眠、离线补算和重新进入验收
8. 后续正式 UI 视觉重做
```

## 8. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-14 | 验收 | 核心 Lobby/邀请/pondId/权限/生命周期链路验证完成；双 Steam 联调因缺少第二测试账号跳过，需求标记已实现 |
| 2026-08-12 | 主 Agent | 新增 STEAM-DESKTOP-03，明确简化版 UI、业务逻辑解耦和 Lobby 权限边界 |
| 2026-08-12 | 架构复核 | 修正 Lobby 与 pond 生命周期：房主/Lobby 失效不删除鱼塘，重新进入触发离线生态补算 |
| 2026-08-12 | 主 Agent | 完成 Lobby/好友/邀请/pondId 映射实现；按要求跳过双 Steam 账号验收并登记计划表 |
