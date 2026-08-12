# 开发交接提示词：BUG-22 Steam Lobby 创建权限与状态残留

你是 Fish Social 的后端 + Unity 联调工程师。请修复 `BUG-22`，解决 Steam Lobby 创建时服务端权限拒绝信息不透明，以及创建失败后 Unity 状态残留的问题。

## 必读

1. `docs/planning/specs/BUG修复-SteamLobby创建权限与状态残留.md`
2. `docs/planning/specs/Steam好友Lobby邀请与鱼塘映射.md`
3. `docs/planning/specs/Steam身份账号绑定与安全会话.md`
4. `server/src/socialRoutes.ts`
5. `server/src/auth.ts`
6. `server/src/steamAuth.ts`
7. `server/src/db.ts`
8. `fish-social-unity/Assets/Scripts/Desktop/Social/SocialLobbyController.cs`
9. `fish-social-unity/Assets/Scripts/Desktop/Social/SocialLobbyApiClient.cs`
10. `fish-social-unity/Assets/Scripts/Desktop/Social/SteamSocialLobbyAdapter.cs`

## 已知现象

Unity 已获取 Steam Lobby ID，例如：

```text
1097752418953332229
```

使用鱼塘：

```text
pond-calm
```

但调用 `/api/social/lobby/create` 后界面只显示：

```text
服务端拒绝当前 Lobby 权限
```

当前客户端把所有 HTTP 403 统一映射成这一句话，无法确认是：

- JWT 对应 playerId 不存在
- steam_accounts 绑定不存在或已撤销
- 服务端使用了不同数据库
- SteamID64 无法解析
- AppID/环境不一致
- 服务端权限规则拒绝

## 后端任务

### 1. 复核认证绑定

在 `/api/social/lobby/create` 中确认：

```text
resolveAuthedPlayerId(req)
→ steam_accounts.player_id
→ steam_accounts.steam_id64
```

必须使用同一数据库和同一 JWT_SECRET。

### 2. 增加结构化诊断

创建失败时记录：

```text
eventType: social_lobby_create_rejected
playerId: 脱敏
steamBindingFound: boolean
steamIdFound: boolean
appId
lobbyId
pondId
code
```

禁止记录：

- JWT
- Steam Ticket
- Publisher Web API Key

### 3. 保留稳定错误码

至少区分：

```text
LOBBY_STEAM_BINDING_REQUIRED
LOBBY_ID_INVALID
POND_NOT_FOUND
LOBBY_GAME_VERSION_MISMATCH
LOBBY_PROTOCOL_VERSION_MISMATCH
```

### 4. 检查 Lobby 生命周期

- Steam Lobby 创建失败不删除鱼塘。
- Node 权限登记失败只关闭/离开 Steam Lobby。
- Lobby 关闭不删除 `pondId`。
- 离开 Lobby 不影响鱼塘会话和生态生命周期。

## Unity 任务

### 1. 显示真实错误

修改 `SocialLobbyApiClient.ReadError()`，优先解析服务端 JSON：

```json
{
  "ok": false,
  "code": "LOBBY_STEAM_BINDING_REQUIRED",
  "error": "需要有效的 Steam 账号绑定"
}
```

界面应显示稳定的用户提示，同时在 Debug 日志中记录错误码，不记录 JWT/Ticket。

### 2. 创建成功的状态顺序

必须保持：

```text
Steam Lobby 创建成功
→ Node /api/social/lobby/create 成功
→ 写入 CurrentLobbyId / CurrentPondId
→ LobbyJoined
```

不要在 Node 创建成功前将 Lobby 标记为最终有效。

### 3. 创建失败回滚

`OnLobbyCreated()` 的 API 回调失败时必须：

```text
_adapter.CloseLobby()
CurrentLobbyId = null
CurrentPondId = null
_pendingLobbyId = null
_pendingPondId = null
State = Failed
```

失败后点击邀请、关闭或离开，不得操作一个已经失效的 Lobby。

### 4. 重试

创建失败后再次登录/创建必须能够重新生成 Lobby，不得复用旧 Lobby ID 或旧状态。

## 测试要求

### 后端

- 有效 Steam 绑定创建成功。
- 无绑定返回 `LOBBY_STEAM_BINDING_REQUIRED`。
- 撤销绑定返回同一稳定错误码。
- JWT 对应不存在的 playerId 被拒绝。
- 合法 lobbyId、`pond-calm`、版本和协议可以通过后续校验。
- 服务端日志包含诊断字段但不包含敏感凭证。

### Unity

- 创建成功后可邀请好友。
- 创建失败后界面显示真实错误原因。
- 创建失败后点击邀请不会发送旧 Lobby 请求。
- 创建失败后点击关闭不会误报关闭成功。
- 修复后可以重新创建成功。
- Lobby 创建失败、关闭或离开都不会删除 `pondId`。

## 验证命令

```powershell
npm test -- --run
npm run verify:engineering
```

Unity 侧使用 Windows Development Build 验证：

```text
登录
→ 创建 Lobby 成功
→ 创建失败
→ 查看具体错误
→ 重试创建
→ 邀请好友
→ 关闭/离开 Lobby
→ 确认鱼塘仍可从其他入口进入
```

## 交付

- 修复后的后端诊断和稳定错误码。
- 修复后的 Unity 错误展示和状态回滚。
- 测试结果和实际拒绝原因。
- 更新 `BUG-22` 需求验收状态。

建议角色：`@backend-dev` 主责认证与路由，`@frontend-dev` 主责 Unity 状态和错误展示。
