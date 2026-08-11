# 真人 Socket 容量（R2-3）· 双容量说明（BE-OPT-D / QUAL-10）

单实例推荐：**活跃真人 Socket &lt; 200**（可用 env 调整）。Bot 在内存中运行，**不占用**真人限额。

## 两个上限怎么选

| 变量 | 默认 | 作用对象 | 超限行为 |
|------|------|----------|----------|
| `MAX_HUMAN_SOCKETS` | `200` | 已 `bindPlayer` 的**真人** | `join_pond` 软拒绝 `human_socket_limit` |
| `MAX_SOCKET_CONNECTIONS` | `200` | **全部** Socket.IO 连接（含未鉴权） | 连接建立后立即 disconnect |

推荐：两者同为 `200`；若网关有握手洪水，可把 `MAX_SOCKET_CONNECTIONS` 略高于真人上限以便观察。

## 配置

```bash
MAX_HUMAN_SOCKETS=200
# MAX_HUMANS_PER_POND=   # 可选；空=不额外限制（钓位逻辑仍生效）
# MAX_SOCKET_CONNECTIONS=200
```

## 行为

- 超限时新 `join_pond` 返回错误码 `human_socket_limit`（或 `humans_per_pond`）
- 已绑定玩家重连 / 再进塘 **不拒绝**
- 结构化日志：`capacity_reject`（playerId、current、limit）
- `/ready` 与 `GET /api/admin/status` 暴露：`humanSocketCount`、`humanInPond`、`botInPondCount`、`capacityLimit`

## 验收

```bash
npm run verify:capacity-limit
```

规格：`docs/planning/specs/架构-单实例容量与真人隔离-R2-3.md`  
环境总表：[`server-env.md`](./server-env.md)
