# BUG 修复：Steam Lobby 创建权限拒绝与状态残留

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Steam Lobby 创建权限拒绝与状态残留 |
| 编号 | **BUG-22** |
| 类型 | Bug 修复 / Steam 桌面端 |
| 负责人 | 后端 + Unity 工程师 |
| 状态 | **已实现** |
| 优先级 | P0 |
| 设计时间 | **2026-08-13** |
| 关联需求 | `STEAM-DESKTOP-03` |

---

## 1. 问题现象

真实 Steam 登录成功后：

- 点击“创建 Lobby”显示“服务端拒绝当前 Lobby 权限”。
- Lobby 状态进入 `Failed`。
- 已获得 Steam Lobby ID，例如 `1097752418953332229`。
- 鱼塘为 `pond-calm`。
- 创建失败后继续点击邀请/关闭/离开，可能出现 Lobby 状态与真实 Steam Lobby 不一致。

当前 Unity 将所有 HTTP `403` 统一转换为相同文案，无法区分真实错误原因。

---

## 2. 风险范围

### 服务端

创建 Lobby 前必须同时确认：

- JWT 有效。
- JWT 对应的 `playerId` 存在。
- `playerId` 存在未撤销的 `steam_accounts` 绑定。
- 绑定的 `steam_id64` 可解析。
- AppID、游戏版本、协议版本和 `pondId` 正确。

### Unity

Steam Lobby 回调成功不代表 Node Lobby 权限记录创建成功。必须区分：

```text
Steam Lobby 创建成功
→ Node 权限登记成功
→ LobbyJoined
```

如果 Node 登记失败：

- 关闭或离开 Steam Lobby。
- 清空 `CurrentLobbyId`、`CurrentPondId` 和 pending 状态。
- 状态进入 `Failed`。
- 显示服务端真实 `code/error`。
- 不允许继续邀请或关闭一个未登记成功的 Lobby。

---

## 3. 修复要求

### 3.1 服务端诊断

- `/api/social/lobby/create` 对绑定校验失败返回稳定错误码。
- 记录结构化诊断日志，但不得记录 JWT、Ticket 或 Web API Key。
- 日志至少包含脱敏后的 `playerId`、SteamID 是否存在、AppID、Lobby ID、pondId 和拒绝原因。
- 确认服务端和 Unity 使用同一数据库、JWT_SECRET、AppID 和服务地址。

### 3.2 Unity 错误展示

客户端不得把所有错误都显示为“服务端拒绝当前 Lobby 权限”。

至少区分：

- `LOBBY_STEAM_BINDING_REQUIRED`
- `LOBBY_ID_INVALID`
- `POND_NOT_FOUND`
- `LOBBY_GAME_VERSION_MISMATCH`
- `LOBBY_PROTOCOL_VERSION_MISMATCH`
- `LOBBY_OWNER_REQUIRED`
- `LOBBY_CACHE_MISSING`
- `LOBBY_INVITE_INVALID`
- 网络不可用

### 3.3 Unity 状态回滚

创建流程失败时必须清理：

```text
CurrentLobbyId = null
CurrentPondId = null
pendingLobbyId = null
pendingPondId = null
```

同时关闭/离开 Steam Lobby，但不得关闭 `pondId` 对应的鱼塘。

### 3.4 生命周期边界

- Lobby 创建失败不影响鱼塘。
- Lobby 关闭不删除鱼塘。
- 离开 Lobby 不离开鱼塘。
- 只有 Socket/鱼塘会话离开才影响在线人数和休眠判断。

---

## 4. 验收标准

- [x] 创建成功时显示真实 Lobby ID、pondId 和 `LobbyJoined`。
- [x] 创建权限失败时显示服务端真实错误码。
- [x] 创建失败后不能继续邀请或关闭失效 Lobby。
- [x] 创建失败后重新登录/重新创建可以恢复。
- [x] 有效 Steam 绑定可以创建 Lobby。
- [x] 缺少绑定、撤销绑定或 JWT 对应错误 playerId 时明确拒绝。
- [x] `pond-calm` 等合法鱼塘不会因 Lobby 失败或关闭而删除。
- [x] 服务端日志不包含 JWT、Ticket 或 Web API Key。
- [x] `npm test -- --run` 通过。
- [x] Unity Windows Development Build 通过创建成功、创建失败、重试和状态回滚测试。

---

## 5. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-13 | 主 Agent | 登记 BUG-22，覆盖 Lobby 创建权限诊断、Steam 绑定一致性和 Unity 状态回滚 |
| 2026-08-13 | 主 Agent | 修复创建者 `LobbyEntered` 被误判为邀请加入，避免无邀请 Token 导致权限回滚 |
| 2026-08-13 | 验证修复 | `socialLobbyLifecycle.test.ts` 已隔离 `STEAM_APP_ID` 测试环境，定向测试恢复 2/2 通过；完整服务端测试 33/33 通过 |
| 2026-08-13 | 用户验收 | 真实 Unity Windows Development Build + Steam 联调通过，BUG-22 验收完成 |
