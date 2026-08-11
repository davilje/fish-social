# 开发交接提示词：Steam 桌面端独立游戏转型（STEAM-DESKTOP-EPIC）

你是 Fish Social 的版本协调 Agent。请按规格推进 Steam 独立游戏桌面端阶段，主客户端迁移到 Unity Windows，保留 Node 服务端作为玩法权威。当前阶段允许 Unity 桌面壳、Steam 账号契约和服务端离线生态补算并行，但不得提前重写为全 Steam Networking/Relay 实时传输。

## 必读

1. `docs/planning/specs/Steam桌面端独立游戏转型计划.md`
2. `docs/planning/specs/Unity移植-分阶段需求清单.md`
3. `docs/planning/architecture/Unity迁移决策记录.md`
4. `docs/planning/architecture/Unity契约冻结清单-v0.md`
5. `shared/types.ts`
6. `server/src/` 中的认证、鱼塘会话、钓鱼状态机和玩家数据模块

## 目标边界

- Steamworks 原生接口用于 Steam Ticket、SteamID64、好友、Lobby、邀请和 Rich Presence。
- Node 继续负责 JWT、鱼塘 FSM、钓鱼结果、库存、每日额度、社交和持久化。
- Unity 负责 Windows 桌面窗口、托盘/后台、通知、场景表现和用户输入。
- 空鱼塘不运行高频实时 Tick；通过 `lastSimulatedAt` 在唤醒时幂等补算鱼群迁移、成长和生态变化。
- Steam Cloud 不保存金币、库存、鱼获和每日额度等权威资产。

## 建议顺序

1. 先冻结 Steam Ticket → `playerId` 绑定、JWT 会话和错误码契约。
2. 并行实现 Unity Windows 启动/窗口/最小化/托盘/通知基础壳。
3. 并行实现服务端 `lastSimulatedAt`、离线补算、版本和幂等事务。
4. 在账号契约稳定后接入 Steam Lobby、好友邀请和 `pondId` 映射。
5. 用现有 Socket.IO 完成 Unity 最小闭环：登录→进塘→挂机→聊天→收鱼。
6. 最后再评估 Steam Networking/Relay，不把它设为本阶段前置条件。

## 安全要求

- 不信任客户端自报 SteamID；服务端验证 Steam Ticket。
- `SteamID64 ↔ playerId` 绑定必须可审计、可撤销、避免重复绑定。
- 所有库存、金币、鱼获和每日额度变更必须由 Node 服务端决定。
- 写操作必须幂等，Token、API Key 和数据库备份不得进入日志或仓库。
- 断线、重复进入和服务器重启不能重复执行离线补算。

## 验收

- Unity Windows 客户端能启动并完成窗口化/最小化/恢复。
- Steam Ticket 验证成功后建立内部 `playerId` 和 JWT 会话。
- Steam Lobby 能携带 `pondId`，好友邀请能进入对应鱼塘。
- Unity 使用现有 Node 协议完成一次完整钓鱼和收鱼。
- 空鱼塘停止高频 Tick，重新进入能按时间差补算且重复唤醒不重复结算。
- 运行现有 `verify:*` 门禁；新增契约、认证和离线补算测试。

## 不要做

- 不要把 FSM、咬钩、库存或鱼获权威搬到 Unity。
- 不要为每个鱼塘创建常驻服务器进程。
- 不要在未完成最小闭环前同时替换 Socket.IO 为 Steam Networking。
- 不要继续在 RN 和 Unity 双写完整新功能；RN 仅保留紧急修复。

建议角色：`@frontend-dev`（Unity 桌面端）+ `@backend-dev`（Steam 认证、Lobby 契约、离线生态）；主 Agent 负责跨线验收。
