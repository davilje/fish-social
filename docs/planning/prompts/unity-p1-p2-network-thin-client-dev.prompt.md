# 开发交接提示词：UNITY-P1 / UNITY-P2 Unity 网络薄客户端（已实现）

你是 Fish Social 的 Unity 网络工程师，负责在现有 Unity Windows 桌面壳上完成契约工程化和网络薄客户端接入。

## 必读

1. `docs/planning/specs/Unity移植-分阶段需求清单.md`
2. `docs/planning/specs/Steam身份账号绑定与安全会话.md`
3. `docs/planning/architecture/Unity契约冻结清单-v0.md`
4. `fish-social-unity/Assets/Scripts/Desktop/Auth/`
5. `server/src/` 的 auth、Socket.IO、pond session 和现有事件定义
6. `shared/` 中现有类型与协议定义

## 当前已完成，不要重复实现

- Unity Windows 桌面壳 `STEAM-DESKTOP-04` 已完成。
- Steamworks.NET 已安装，SteamAPI 初始化已验证。
- Steam Ticket 登录已验证。
- SteamID/playerId 登录响应和 JWT REST 会话已验证。
- `AuthenticatedApiClient` 已可用，当前“验证当前会话”已返回成功。
- 已增加 `UnityNetworkDtos`、`Unity网络薄客户端契约-v1.md` 和 Windows Socket.IO WebSocket 适配层。
- 已在 Unity Windows Development Build 中完成真实连接、进塘、钓鱼、背包更新和断线重连验收（2026-08-12）。

## 阶段门禁

必须先完成 UNITY-P1，再完成 UNITY-P2。不能直接在 UI 中散落 Socket 调用，也不能用假在线状态替代连接。

## UNITY-P1：契约工程化

### 目标

让 Unity、Node 和 shared 对 REST/Socket 的事件名、payload、错误码和鉴权边界使用同一份可审计契约。

### 必做

1. 盘点现有 REST 路径、Socket 事件、ack 返回和错误码。
2. 固定 `join_pond`、`pond_snapshot`、`register_player`、`start_fishing`、`stop_fishing`、`fish_bite`、`accept_catch`、聊天等事件契约。
3. 为关键 payload 建立 JSON Schema 或等价 Markdown/JSON 契约。
4. 建立 Unity C# DTO，允许 Unity 工程编译引用。
5. 记录 `protocolVersion` 和兼容策略。
6. 明确服务端是鱼塘、鱼获、库存、额度和玩家状态的唯一权威。
7. 明确客户端不得自报 `playerId`、鱼获、金币、概率或钓鱼结果。

### P1 出口

- [x] Unity 工程可编译引用 DTO。
- [x] 关键 REST/Socket 事件有契约文档。
- [x] 有契约与现有 server/shared 的冲突清单。
- [x] 有禁止客户端掷骰和伪造业务状态的说明。

## UNITY-P2：网络薄客户端

### 目标

使用现有 Node Socket.IO 完成 Unity 的最小实时闭环，不引入 Steam Networking/Relay 替换现有协议。

### 必做

1. 选择与 Node Socket.IO 版本兼容、许可证可审计的 Unity C# 客户端。
2. 将实现封装为 `ISocialSocketClient`，替换 `UnavailableSocialSocketClient`。
3. JWT 只能通过 `auth.token` 发送。
4. 不允许客户端通过连接参数自报 `playerId` 作为权威身份。
5. 实现连接、断开、连接失败和重连状态。
6. 实现重连后的会话恢复，不重复创建玩家会话。
7. 实现最小闭环：

```text
Steam 登录
→ JWT
→ Socket 连接
→ register_player
→ join_pond
→ pond_snapshot
→ start_fishing
→ fish_bite
→ stop_fishing
→ accept_catch
```

8. 将 Socket 状态接入桌面壳：

```text
连接：连接中
连接：在线
连接：断开
连接：重连中
```

9. 记录普通错误提示，但不得打印完整 JWT、Ticket 或敏感 payload。
10. 为在线用户、当前 phase、聊天一行增加最小调试 UI，可使用临时文字和几何体。

### P2 出口

- [x] Unity Development Build 可使用真实 JWT 连接本地 Node Socket.IO。
- [x] 可以进入鱼塘并收到 `pond_snapshot`。
- [x] 可以完成一次真实钓鱼并通过 `accept_catch` 入库。
- [x] 断线后可以重连，不重复创建玩家或重复结算。
- [x] 服务端拒绝错误 JWT。
- [x] 连接状态不再显示“离线（占位）”。
- [x] Unity 不保存服务端 Web API Key。

## 测试矩阵

至少验证：

- Steam 登录成功，Socket 连接成功。
- 服务端关闭后，客户端显示连接失败但不崩溃。
- 错误 JWT 被拒绝。
- 断网后恢复连接。
- 同一玩家重连不会重复 `register_player` 造成脏会话。
- 多个 Steam 账号进入同一鱼塘时状态隔离正确。
- `accept_catch` 只能由服务端根据真实会话和事件处理。

## 禁止事项

- 不把移动端 TypeScript 源码直接复制进 Unity。
- 不把 Web API Key 放入 Unity、shared、日志或 Git。
- 不用 Fake Ticket 或本地假 Socket 通过真实验收。
- 不在 Unity 本地计算并提交鱼获、金币、每日额度或概率结果。
- 不在本阶段改用 Steam Networking/Relay。
- 不为了让 UI 显示在线而硬编码连接成功。

## 交付

- 代码、契约、测试说明和运行日志摘要。
- 明确 P1/P2 各自完成与未完成项。
- 运行 Unity 编译、Windows Development Build 和 Node 相关测试。
- 验收通过后再更新 `UNITY-P1`、`UNITY-P2` 和 `STEAM-DESKTOP-02` 的计划状态。

建议角色：`@frontend-dev` 主责 Unity，`@backend-dev` 配合协议核对与服务端联调。
