# Unity 网络薄客户端契约 v1

## 1. 范围

本契约覆盖 UNITY-P1/P2 的 Unity Windows 薄客户端最小闭环：

```text
Steam 登录
→ JWT
→ Socket.IO auth.token
→ register_player
→ join_pond
→ pond_snapshot
→ take_spot
→ start_fishing
→ fish_bite
→ stop_fishing
→ accept_catch
→ inventory_updated
```

协议版本基线：`1.0.0-draft`。本阶段不引入 Steam Networking/Relay，不迁移移动端 UI 或业务权威。

## 2. 权威边界

- Node 服务端是玩家身份、鱼塘用户、钓鱼阶段、鱼获、库存、金币和每日额度的唯一权威。
- Unity 只发送操作意图：进塘、选择钓位、开始/停止钓鱼、领取服务端已产生的鱼获。
- Unity 不得自报 SteamID64 作为登录身份，不得把 `playerId` 当作 Socket 鉴权来源。
- `register_player` / `join_pond` 中的 `playerId` 仅作为兼容 payload；服务端必须使用 Socket JWT 的 `authPlayerId`。
- Unity 不计算咬钩概率、品质、尺寸、鱼获结果或每日额度。

## 3. Socket 鉴权与连接

连接地址：

```text
ws(s)://<server>/socket.io/?EIO=4&transport=websocket
```

Socket.IO connect payload：

```json
{"token":"<JWT>"}
```

禁止把 JWT 放进 query string、普通日志或持久化存储。

连接状态：

```text
Disconnected → Connecting → Connected
                     └────→ Failed
Connected → Reconnecting → Connected
```

## 4. 最小事件契约

| 方向 | 事件 | Payload | 结果 |
|---|---|---|---|
| C2S | `register_player` | `playerId: string` | 服务端以 JWT 身份绑定 Socket |
| C2S | `join_pond` | `{ pondId, nickname, playerId }` | ack `{ ok, userId?, error? }`，随后 `pond_snapshot` |
| C2S | `take_spot` | `{ pondId, spotId }` | ack `{ ok, error? }` |
| C2S | `start_fishing` | `{ pondId, spotId? }` | ack `{ ok, error? }` |
| C2S | `stop_fishing` | `pondId` | ack `{ ok, error?, todayRemainingMs? }` |
| S2C | `pond_snapshot` | `PondSnapshot` | 当前鱼塘、用户、消息和库存 |
| S2C | `pond_user_updated` | `PondUser` | 当前阶段/钓位变化 |
| S2C | `session_timer_tick` | `{ userId, sessionFishingMs }` | 仅展示计时 |
| S2C | `fish_bite` | `PendingFishCatch` | 服务端生成的待领取鱼获 |
| C2S | `accept_catch` | `catchId` | ack `{ ok, error?, item? }` |
| S2C | `inventory_updated` | `FishInventoryItem[]` | 服务端库存结果 |

其余事件以 `shared/types.ts` 和 `Unity契约冻结清单-v0.md` 为准，不在 Unity 层自行扩展。

## 5. 已知冲突与处理

| 项 | 现状 | 处理 |
|---|---|---|
| `register_player.playerId` | 历史协议要求传入 | Unity 传当前登录 playerId；服务端仍以 JWT 覆盖 |
| `join_pond.playerId` | 历史 payload 保留 | 仅兼容字段，不作为权威身份 |
| `fish_bite` | 服务端直接推送 `PendingFishCatch` | Unity 只展示并提交 `catchId` |
| Socket.IO C# 依赖 | Unity 工程未引入第三方包 | 当前使用 Windows 可用的 Engine.IO v4 WebSocket 适配层；后续若替换包必须审计许可证和版本 |
| 自动重连 | 服务端已有断线/恢复语义 | 客户端重连后重新认证并重新 `join_pond`，不重复创建玩家档案 |

## 6. 禁止项

- 不复制 `mobile/` TypeScript 到 Unity。
- 不在 Unity 本地掷骰、结算鱼获或改库存。
- 不把 Web API Key、完整 Ticket、JWT 写入 Unity 资源、日志或 PlayerPrefs。
- 不通过硬编码“在线”绕过真实 Socket 状态。
