# Server 环境变量清单（BE-OPT-D / QUAL-04）

权威模板：仓库根目录 `.env.example`。代码常量：`server/src/env.ts`。

## 必填（生产）

| 变量 | 说明 |
|------|------|
| `JWT_SECRET` | 玩家 JWT；开发可用 `AUTH_DISABLED=1` 绕过 |
| `ADMIN_SECRET` | Admin API / SSE |
| `PLAYER_ERASE_PEPPER` | 删号匿名化胡椒 |
| `ALLOWED_ORIGINS` | CORS；**禁止 `*`**（compose 亦不再默认 `*`） |

## Steam 桌面端账号登录

| 变量 | 默认 | 说明 |
|------|------|------|
| `STEAM_AUTH_ENABLED` | `false` | 显式开启 Steam Ticket 登录 |
| `STEAM_APP_ID` | 空 | 服务端校验的 Steam App ID |
| `STEAM_WEB_API_KEY` | 空 | Steam Web API Key；仅放 Node 服务端密钥环境，不进入 Unity/Git |
| `STEAM_AUTH_IDENTITY` | `fish-social-server-v1` | 服务端要求的 `GetAuthTicketForWebApi` identity |

真实 Steam 登录必须同时配置上述四项；本地 Fake Ticket 测试不得作为真实 Steam 验收。

## 容量（QUAL-10）

| 变量 | 默认 | 含义 |
|------|------|------|
| `MAX_HUMAN_SOCKETS` | `200` | 已绑定**真人**软上限；超限 `join_pond` → `human_socket_limit` |
| `MAX_SOCKET_CONNECTIONS` | `200` | **全部** Socket.IO 连接硬顶（含未鉴权） |
| `MAX_HUMANS_PER_POND` | 空 | 可选单塘真人上限 |

详见 [`human-capacity.md`](./human-capacity.md)。

## 稳定性 / 运维面

| 变量 | 默认 | 说明 |
|------|------|------|
| `SHUTDOWN_TIMEOUT_MS` | `8000` | 优雅停机强制退出 |
| `SOCKET_EVENT_RATE_PER_SEC` | `20` | 单连接高频事件预算 |
| `ADMIN_ALLOW_QUERY_KEY` | 生产 `false` | REST 是否允许 `?key=`；SSE `/api/admin/live-session` 始终允许 |
| `OPS_STATIC_ENABLED` | `true` | `false` 时不挂载 `/planning` `/ops` `/analytics`（**公网建议关**；仅内网可开） |
| `LOG_MASK_USER_DATA` | 生产默认掩码 | `false` 关闭；掩码含 `playerId` / `nickname` |

## Metrics / PG（QUAL-03）

| 变量 | 说明 |
|------|------|
| `METRICS_PG_URL` | 可选 PG 双写 |
| `METRICS_DUAL_WRITE=true` | SQLite + PG 写 |
| `METRICS_READ_FROM` | **仅 `sqlite`**；设 `postgres` 时启动拒绝 |

## 日志降噪（OBS-LOG-1）

| 变量 | 默认 | 说明 |
|------|------|------|
| `PERF_LOG_SLOW_MS` | `50` | 仅当 tick/bite/ecology/snapshot 耗时 ≥ 此值才打 perf info |
| `PERF_LOG_INFO` | `0` | `1` = 按 `PERF_LOG_INTERVAL_MS` 抽样全量 perf info |
| `PERF_LOG_INTERVAL_MS` | `30000` | 慢日志节流 |
| `ECOLOGY_VERBOSE` | `0` | `1` = 输出补鱼/迁徙/seed console |
| `FANOUT_LOG_INFO` | `0` | `1` = 生态 tick 打 `socket_broadcast_fanout` info（默认仅 Prometheus） |
| `SOCKET_TAP_IGNORE_EVENTS` | `session_timer_tick,pond_ecology_updated` | tap 忽略列表 |
| `SOCKET_TAP_INCLUDE_TIMER` | `0` | `1` = 从忽略列表去掉 `session_timer_tick` |

详见规格：`docs/planning/specs/服务端日志降噪与分层输出.md`。

## 探活

停机中 `/health` 与 `/ready` 均为 503 — 见 [`shutdown-health.md`](./shutdown-health.md)。
