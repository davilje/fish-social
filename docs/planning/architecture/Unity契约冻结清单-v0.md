# Unity 契约冻结清单 v0（UNITY-P0）

| 项 | 内容 |
|----|------|
| 文档版本 | **v0** |
| `protocolVersion` 基线 | **`1.0.0-draft`**（P1 落地可选握手字段） |
| 状态 | **已冻结（P0 出口）** |
| 完成时间 | **2026-07-26** |
| 权威源码 | `shared/types.ts` · `server/src/*Routes*.ts` · `createApp.ts` · `shop.ts` · `admin.ts` |
| 决策 | [`Unity迁移决策记录.md`](./Unity迁移决策记录.md) |
| 用途 | **UNITY-P1** OpenAPI / Socket 目录 / C# DTO 的直接输入 |

> 默认：**不改**下列事件名与路径；破例须升 `protocolVersion` 并改本文件。  
> Admin 路径仅供运维对照，**Unity 客户端不消费**。

---

## 1. Socket · Client → Server（C2S）

来源：`ClientToServerEvents`（`shared/types.ts`）

| 事件 | Payload | Ack |
|------|---------|-----|
| `register_player` | `playerId: string` | 无 |
| `join_pond` | `JoinPondPayload` `{ pondId, nickname, playerId }` | `{ ok, userId?, error? }` |
| `leave_pond` | `string`（pondId）或 `LeavePondPayload` | 无 |
| `start_fishing` | `StartFishingPayload` `{ pondId, spotId }` | `{ ok, error? }` |
| `stop_fishing` | `pondId: string` | `{ ok, error? }` |
| `send_chat` | `SendChatPayload` `{ pondId, text }` | `{ ok, error? }` |
| `accept_catch` | `catchId: string` | `{ ok, error?, item? }` |

---

## 2. Socket · Server → Client（S2C）

来源：`ServerToClientEvents`

| 事件 | Payload（摘要） |
|------|-----------------|
| `pond_snapshot` | `PondSnapshot` |
| `pond_ecology_updated` | `PondEcologySummary` |
| `pond_user_joined` | `PondUser` |
| `pond_user_left` | `userId: string` |
| `pond_user_updated` | `PondUser` |
| `session_timer_tick` | `SessionTimerTickPayload` |
| `chat_message` | `ChatMessage` |
| `fish_bite` | `PendingFishCatch` |
| `fish_miss` | `FishingMiss` |
| `fishing_float_text` | `FishingFloatTextPayload` |
| `bait_depleted` | `BaitDepletedPayload` |
| `gear_updated` | `PlayerGearState` |
| `codex_unlocked` | `CodexUnlockPayload` |
| `inventory_updated` | `FishInventoryItem[]` |
| `dm_message` | `DirectMessage` |
| `friend_request` | `FriendRequest` |
| `post_liked` | `{ postId, playerId, liked, likeCount }` |
| `post_commented` | `{ postId, comment }` |
| `post_comment_deleted` | `{ postId, commentId, commentCount }` |
| `error` | `message: string` |

### 进塘闭环（P2 最小路径）

```text
register_player
  → join_pond → pond_snapshot
  → start_fishing → (pond_user_updated / session_timer_tick / fishing_float_text…)
  → fish_bite → accept_catch → inventory_updated
  → leave_pond（或断线语义）
```

---

## 3. REST · Unity 客户端主路径

鉴权：JWT `Authorization: Bearer <token>`（与现 RN 一致）。

### 3.1 Auth / 世界 / 库存

| Method | Path | 鉴权 | 说明 |
|--------|------|------|------|
| POST | `/api/auth/dev-token` | 本地开发 | 仅 localhost |
| GET | `/api/world` | 公开 | 世界地图 / 鱼塘列表 |
| GET | `/api/inventory/:playerId` | self | 背包 |
| POST | `/api/inventory/sell` | auth | 卖鱼 |
| POST | `/api/client-logs` | auth | 客户端日志 |

### 3.2 Players

| Method | Path | 鉴权 |
|--------|------|------|
| POST | `/api/players/register` | 公开 |
| GET | `/api/players/:playerId` | self |
| GET | `/api/players/:playerId/public-view` | 公开 |
| PUT | `/api/players/:playerId/settings` | auth |
| PUT | `/api/players/:playerId/profile` | auth |
| PUT | `/api/players/:playerId/showcase` | auth |
| GET | `/api/players/search` | 公开 |

### 3.3 Shop / Gear / Codex

| Method | Path | 鉴权 |
|--------|------|------|
| GET | `/api/shop/baits` | 公开 |
| GET | `/api/shop/tackle` | 公开 |
| POST | `/api/shop/baits/buy` | auth |
| POST | `/api/shop/tackle/buy` | auth |
| POST | `/api/shop/tackle/repair` | auth |
| GET | `/api/player/gear` | auth |
| POST | `/api/player/equip/bait` | auth |
| POST | `/api/player/equip/tackle` | auth |
| GET | `/api/player/codex` | auth |

### 3.4 Friends / DM

| Method | Path | 鉴权 |
|--------|------|------|
| GET | `/api/friends/:playerId` | self |
| GET | `/api/friends/:playerId/requests` | self |
| POST | `/api/friends/request` | auth |
| POST | `/api/friends/accept` | auth |
| POST | `/api/friends/reject` | auth |
| POST | `/api/friends/remove` | auth |
| GET | `/api/dm/conversations/:playerId` | self |
| GET | `/api/dm/:playerId/:friendPlayerId` | self |
| POST | `/api/dm` | auth |

### 3.5 Posts

| Method | Path | 鉴权 |
|--------|------|------|
| GET | `/api/posts/wall` | 公开 |
| GET | `/api/posts/friends/:playerId` | self |
| POST | `/api/posts` | auth |
| POST | `/api/posts/:postId/like` | auth |
| GET | `/api/posts/:postId/likes` | 公开 |
| POST | `/api/posts/:postId/comments` | auth |
| GET | `/api/posts/:postId/comments` | 公开 |
| DELETE | `/api/posts/:postId/comments/:commentId` | auth |

### 3.6 Leaderboard

| Method | Path | 鉴权 |
|--------|------|------|
| GET | `/api/leaderboard/daily-biggest` | 公开 |
| GET | `/api/leaderboard/weekly-king` | 公开 |
| GET | `/api/leaderboard/pond/:pondId` | 公开 |
| GET | `/api/leaderboard/rare` | 公开 |
| GET | `/api/leaderboard/my-rank` | auth |

---

## 4. REST · Admin（不进 Unity）

组前缀：`/api/admin/*`（密钥 / RBAC）。完整枚举见 `server/src/admin.ts`、`adminEcologyRoutes.ts`。  
Unity 迁移 **不**要求 C# 绑定这些路径。

---

## 5. 关键 DTO（P1 优先生成）

| 类型 | 用途 |
|------|------|
| `PondSnapshot` / `PondUser` / `PondConfig` | 进塘 |
| `PendingFishCatch` / `FishInventoryItem` / `FishingMiss` | 钓获 |
| `JoinPondPayload` / `StartFishingPayload` / `SendChatPayload` | C2S |
| `PlayerGearState` / `CodexUnlockPayload` | 装备图鉴 |
| `ChatMessage` / 社交推送 payload | 聊天与墙 |

权威字段以 `shared/types.ts` 为准；本表只列事件与路径。

---

## 6. 禁止项（给 Unity / 契约工程）

- 客户端本地掷骰决定咬钩 / 品质 / 尺寸  
- 客户端权威改库存 / 金币 / 占位  
- 擅自改事件名或去掉 ack 语义  

---

## 7. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-26 | v0 初版：自 `shared/types.ts` + server 路由导出；供 P1 输入 |
