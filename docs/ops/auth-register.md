# 鉴权与注册（BE-OPT-A / SEC-01～06）

## 生产必配

| 变量 | 说明 |
|------|------|
| `JWT_SECRET` | 必填（与 `AUTH_DISABLED` 互斥旁路） |
| `PLAYER_ERASE_PEPPER` | **生产必填**；缺则进程启动失败 |
| `ADMIN_SECRET` | Admin 运维台 |

```bash
# 生产示例
NODE_ENV=production
JWT_SECRET=...
PLAYER_ERASE_PEPPER=...
```

## 注册 `POST /api/players/register`

| 模式 | 行为 |
|------|------|
| `AUTH_DISABLED=1` 且 `NODE_ENV=development` | **旧行为**：请求体 `playerId` 任意（含已有账号）直接签 JWT |
| `NODE_ENV=production` | **忽略**客户端 `playerId`，服务端 mint `p_<uuid>` + 返回 `{ profile, token }` |
| development + JWT | 可指定**新** `playerId`；若 id 已存在 → `403` `player_id_taken`；缺省则 mint |

客户端必须以响应里的 `profile.playerId` 与 `token` 落盘（勿假定请求 id 不变）。已有账号请走已持有的 JWT，勿再「注册冒充」。

本地纯旁路：

```bash
NODE_ENV=development
AUTH_DISABLED=1
```

## 私有读接口

以下需 `Authorization: Bearer <token>`，且路径 `playerId` 必须等于 token 主体（否则 403）：

- `GET /api/players/:playerId`
- `GET /api/friends/:playerId` · `.../requests`
- `GET /api/posts/friends/:playerId`
- `GET /api/inventory/:playerId`
- `GET /api/dm/...`

仍公开：`/api/world`、`/api/posts/wall`、`/api/players/search`、`/api/players/:id/public-view`。

## client-logs

`POST /api/client-logs`：需鉴权；`logs.length` ≤ 50；落库 `player_id` 强制为 token 主体。

## 验收

```bash
npm run verify:auth
npm run verify:backend-opt-a
```
