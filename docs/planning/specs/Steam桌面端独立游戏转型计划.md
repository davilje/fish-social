# Steam 桌面端独立游戏转型计划

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Steam 桌面端独立游戏转型与 Unity 并行开发计划 |
| 负责人 | 主 Agent / 制作人 |
| 状态 | **已文档化** |
| 目标版本 | v1.0-steam-desktop |
| 编号 | **STEAM-DESKTOP-EPIC** |
| 设计时间 | **2026-08-11** |
| 关联全景章节 | Steam 独立游戏定位、桌面助手形态、Unity 客户端迁移 |

---

## 1. 背景与目标

> 本总计划的产品定位、版本边界和并行关系已完成；具体 Unity 表现层开发已拆分至 `STEAM-DESKTOP-07`。

### 1.1 背景

第一阶段移动端鱼塘功能、额度、收杆结算和实时状态问题已完成修复。项目目标更新为 Steam 独立游戏：面向上班族，提供低打扰挂机钓鱼、好友聊天和轻社交互动，并将主客户端迁移为 Unity Windows 桌面端。

### 1.2 目标

- 将产品定位固定为 Steam 独立游戏，而非移动端功能的直接搬运。
- 使用 Unity 开发桌面端客户端，支持窗口化、最小化、后台挂机和低打扰通知。
- 使用 Steamworks 原生接口接入 Steam 身份、好友、Lobby、邀请和 Rich Presence。
- 第一阶段保留 Node 作为游戏权威服务，实时游戏数据继续走现有服务端协议。
- 支持空鱼塘休眠：停止高频 Tick，但通过 `lastSimulatedAt` 在重新激活时补算鱼群迁移、成长和生态变化。
- 允许 Unity 桌面壳、Steam 账号契约、服务端离线生态补算并行开发。

### 1.3 非目标

- 第一阶段不直接重写为全 Steam Networking/Relay 实时传输。
- 不把 Steam Lobby 当作玩家数据或游戏权威服务。
- 不把库存、金币、鱼获、每日额度和生态状态存入客户端或仅存入 Steam Cloud。
- 不为每个鱼塘创建一个常驻服务器进程。
- 不在 Unity 迁移阶段继续双写完整 RN 功能。

---

## 2. 目标架构与并行关系

```text
Steamworks 身份 / 好友 / Lobby / 邀请
                 ↓
Unity Windows 桌面客户端 ── JWT + Socket.IO ── Node 权威服务
        │                                      │
        └─ 窗口/托盘/通知/表现                    ├─ 鱼塘 FSM / 钓鱼 / 社交
                                               ├─ 离线生态补算
                                               └─ SQLite → 后续可迁 PostgreSQL
```

### 2.1 推荐顺序

| 阶段 | 任务 | 关系 |
|------|------|------|
| G0 | 产品定位、平台边界、账号与网络决策 | 先行门 |
| G1 | Steam Ticket、SteamID64 与 `playerId` 绑定契约 | 与 Unity 桌面壳并行 |
| G1 | Unity Windows 工程、窗口/托盘/通知基础壳 | 与 Steam 账号契约并行 |
| G1 | 空鱼塘生态补算与休眠唤醒 | 与客户端并行，依赖服务端状态字段 |
| G2 | Steam Lobby、好友邀请、`pondId` 映射 | 依赖账号契约 |
| G2 | Unity 网络薄客户端与现有鱼塘闭环 | 依赖契约和 Steam 登录 |
| G3 | 桌面助手完整主循环、社交和场景重做 | 依赖最小可玩闭环 |
| G4 | Steam Networking/Relay 评估与可选接入 | 第一阶段验收后再决定 |

### 2.2 可并行与不可并行

**可以并行：**

- Unity 桌面窗口、托盘、通知和基础导航。
- Steam 账号验证接口和 `playerId` 映射设计。
- 服务端空鱼塘离线补算。
- Unity P1 契约工程化和 C# DTO。

**必须先后：**

- Steam Lobby 邀请必须在 Steam 身份绑定契约之后。
- Unity 鱼塘网络闭环必须在 C# DTO、鉴权和 Socket 事件目录稳定之后。
- 桌面助手完整 UI 必须在最小可玩钓鱼闭环之后收口。
- Steam Networking/Relay 不作为当前 Unity 迁移前置条件。

---

## 3. 子需求

| 编号 | 子需求 | 优先级 | 主要角色 |
|------|--------|--------|----------|
| STEAM-DESKTOP-01 | Steam 独立游戏定位与桌面助手信息架构 | P0 | 策划 + UI |
| STEAM-DESKTOP-02 | Steam 身份、账号绑定与安全会话 | P0 | 后端 + Unity |
| STEAM-DESKTOP-03 | Steam 好友、Lobby、邀请与鱼塘映射 | P1 | 后端 + Unity |
| STEAM-DESKTOP-04 | Unity Windows 桌面端基础壳 | P0 | Unity |
| STEAM-DESKTOP-05 | 空鱼塘休眠与生态离线补算 | P1 | 后端 |

---

## 4. 账号与数据安全基线

- Unity 获取 Steam Ticket，Node 服务端验证 Ticket，不信任客户端自报的 SteamID。
- 使用 `SteamID64 ↔ playerId` 映射；业务数据继续关联内部 `playerId`。
- Steam 验证成功后签发游戏 JWT；短期 JWT 与可撤销 Refresh Token 分离管理。
- 客户端只能提交游戏意图，鱼获、库存、每日额度和生态结果由 Node 权威计算。
- `accept_catch`、购买、收杆等写操作必须幂等，并记录审计事件。
- Token、Steam API 密钥、数据库备份不得写入客户端、日志或版本库。
- Steam Cloud 只保存窗口布局、音量等非权威偏好，不作为玩家资产唯一来源。

---

## 5. 验收标准

- [ ] 规格明确 Steam 独立游戏、桌面助手和上班族挂机社交定位。
- [ ] Unity Windows 客户端可启动、登录、最小化/恢复，并保留后台挂机状态。
- [ ] Node 能验证 Steam Ticket，并稳定完成 `SteamID64 ↔ playerId` 绑定。
- [ ] Steam Lobby 可映射到 `pondId`，好友邀请可进入对应鱼塘。
- [ ] Unity 通过现有 Node 服务完成“进入鱼塘→挂机钓鱼→收鱼→聊天”最小闭环。
- [ ] 空鱼塘停止高频 Tick；重新进入时按 `lastSimulatedAt` 幂等补算鱼群迁移和成长。
- [ ] 权威资产不由客户端或 Steam Cloud 单独保存。
- [ ] 第一阶段不依赖 Steam Networking/Relay；是否升级由真实联机压测和成本报告决定。

---

## 6. 风险与缓解

| 风险 | 缓解 |
|------|------|
| Unity 迁移与 Steam 网络重写同时发生 | 第一阶段保留 Node Socket.IO，Relay 后置 |
| Steam 身份与内部账号错绑 | 服务端验证 Ticket；单独的绑定表和审计 |
| 空鱼塘补算重复或跳跃 | `lastSimulatedAt`、版本号、事务和幂等补算 |
| 桌面助手范围膨胀 | 先锁定登录、进塘、挂机、聊天、收鱼五项主循环 |
| RN 与 Unity 双端重复开发 | RN 进入紧急修复模式，新功能只进入 Unity |
| Steam 平台依赖影响未来多平台 | 把 Steam 适配封装在客户端平台层，Node 仍保留内部 playerId |

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-11 | 主 Agent | 新增 Steam 独立游戏定位、Unity 桌面端并行路线、Steam 账号与空鱼塘补算计划 |
