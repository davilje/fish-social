# Unity 移植 — 分阶段需求清单

| 项 | 内容 |
|----|------|
| 功能名称 | Unity 移植分阶段需求 |
| 状态 | **已定稿**（P0～P5 规划；其中 P0～P2 已实现，P3～P5 尚未实现）；总目标受 `STEAM-DESKTOP-EPIC` 统筹 |
| 编号 | **UNITY-EPIC**（总表）· **UNITY-P0～P5**（阶段） |
| 设计时间 | **2026-07-26** |
| 完成时间 | P0：**2026-07-26** |
| 目标版本 | unity-client |
| 来源 | [`../product/Unity移植工程路径蓝图.md`](../product/Unity移植工程路径蓝图.md)（**REF-UNITY-1**） |
| 体验参照 | [`../product/钓鱼世界与鱼塘场景优化策略.md`](../product/钓鱼世界与鱼塘场景优化策略.md)（**REF-SCENE-1**） |
| 目标形态 | **Unity Windows 桌面端 + Steamworks + 现有 Node 权威服务端** |

> 本文件把参考蓝图拆成**可排期、可验收**的 Unity 技术子阶段。产品定位、Steam 账号/Lobby、桌面助手和并行顺序以 [`Steam桌面端独立游戏转型计划.md`](./Steam桌面端独立游戏转型计划.md) 为上位规格。  
> 原则：权威玩法留在 `server/`；协议先冻结；场景在 Unity **重做**（不平移 RN View）。

---

## 0. UNITY-EPIC 产品规划定义

### 0.1 背景与目标

现有 Fish Social 的玩法权威、生态模拟和社交服务已经位于 Node 服务端，但原客户端主要依赖 Expo/RN，无法直接满足 Windows 桌面宠物、托盘挂机、Steam 身份和低打扰通知的产品形态。`UNITY-EPIC` 用于定义从 RN 客户端切换到 Unity Windows 客户端的完整产品规划边界。

最终目标不是把服务端或运营平台搬进 Unity，而是交付一个可通过 Steam 发行的 Unity Windows 客户端：

- 启动后完成 Steam 身份登录，并通过 JWT 连接现有 Node 服务。
- 在 Unity 中呈现桌面宠物、2D 鱼塘、同塘玩家和钓鱼状态。
- 支持窗口、托盘、通知、断线恢复和低打扰挂机。
- 复用现有服务端的鱼塘、钓鱼、库存、社交和生态权威。
- 在不依赖 Expo 的情况下完成“选塘 → 钓鱼 → 收鱼 → 背包/社交”的主循环。

### 0.2 用户与场景

| 用户 | 场景 | 期望结果 |
|------|------|----------|
| Steam 玩家 | 启动桌面端 | 自动检测 Steam，登录失败时给出可操作提示 |
| 挂机玩家 | 关闭窗口或隐藏到托盘 | 合法钓鱼会话继续，后台降低渲染负载 |
| 社交玩家 | 进入同一鱼塘 | 看到自己和其他玩家的宠物、昵称及基础钓鱼状态 |
| 收鱼玩家 | 鱼咬钩后恢复窗口 | 以服务端状态为准完成收杆、领取和背包更新 |
| 运维人员 | 查看客户端问题 | 在浏览器运营平台和 Node 日志中排查，不进入 Unity |

### 0.3 产品规划范围

| 规划块 | 对应阶段 | 出口 |
|--------|----------|------|
| 迁移决策与契约 | P0～P1 | Unity 与 Node 使用稳定、可追踪的协议和 DTO |
| 最小可玩网络闭环 | P2 | Windows Development Build 可真实登录、进塘、钓鱼、收鱼和重连 |
| Unity 正式表现层 | P3 | 等距鱼塘、宠物、钓位、状态反馈与多人排序可用 |
| Unity 主循环与壳层 | P4 | 选塘、钓鱼、收鱼、背包/商店/社交不依赖 Expo |
| 发行与运营 | P5 | Steam Windows 包、日志、回滚和 RN 退役策略明确 |

### 0.4 角色、权限与权威边界

- Unity 只提交玩家意图：登录、进塘、选位、开始/停止钓鱼、收鱼、聊天和查询。
- Node 服务端唯一决定鱼塘权限、占位、钓鱼相位、咬钩结果、收鱼结果、库存、金币、每日额度和生态状态。
- 客户端不得自行掷骰、生成鱼获、修改库存、伪造在线玩家或仅凭 `pondId` 绕过服务端权限。
- Steam Lobby 只负责发现、邀请和 `pondId` 映射；鱼塘实体和玩家资产归 Node/SQLite 管理。
- Admin、运营平台、数据报表和服务端运维继续留在浏览器/Node，不迁入 Unity。

### 0.5 复用的 API / Socket 契约

| 类型 | 范围 | 规则 |
|------|------|------|
| REST | `/api/auth/steam`、`/api/world`、players、inventory、shop/gear/codex、friends/DM、posts、leaderboard、client-logs | 优先复用现有接口；破坏性变更须升级 `protocolVersion` |
| Socket C2S | `register_player`、`join_pond`、`leave_pond`、`start_fishing`、`stop_fishing`、`send_chat`、`accept_catch` | Unity 发送意图，不在客户端实现权威 FSM |
| Socket S2C | `pond_snapshot`、`pond_user_*`、`fish_bite`、`fish_miss`、`inventory_updated`、`chat_message`、`error` | 以服务端事件驱动表现和恢复 |

### 0.6 总体验收标准

- [x] P0～P2 已按各阶段出口完成，并有对应 Unity/Node 联调记录。
- [ ] Unity 正式场景具备可拖拽的 2D 鱼塘、钓位、宠物和多人状态表现。
- [ ] Unity 主循环“选塘 → 钓鱼 → 收鱼 → 背包/社交”不依赖 Expo。
- [ ] 托盘挂机、通知、断线恢复和服务端快照恢复通过 Windows 验收。
- [ ] 可提交 Steam 的最小可靠 Windows 包、日志方案和协议兼容回滚方案完成。

---

## 1. 总览与依赖

```text
UNITY-P0 决策与契约冻结        ← 已实现（2026-07-26）
    ↓
UNITY-P1 契约工程化            ← 已实现（2026-08-12）
    ↓
UNITY-P2 网络薄客户端「钓一条」 ← 已实现（2026-08-12）
    ↓
UNITY-P3 等距 Tile 场景核心    ← 已定稿（承接 REF-SCENE-1）
    ↓
UNITY-P4 壳层功能迁入          ← 已定稿（可分子包）
    ↓
UNITY-P5 发布与运维对齐        ← 已定稿
```

| 编号 | 名称 | 状态 | 优先级 | 前置 |
|------|------|------|--------|------|
| **UNITY-P0** | 决策与契约冻结 | **已实现** | P0 | REF-UNITY-1 |
| **UNITY-P1** | 契约工程化（OpenAPI/Socket/C# DTO） | **已实现** | P0 | P0 出口 |
| **UNITY-P2** | Unity 网络薄客户端（完整钓一条） | **已实现** | P0 | P1 出口 |
| **UNITY-P3** | 2D 斜 45° 场景核心 | **已定稿** | P0 | P2 出口 · REF-SCENE-1 |
| **UNITY-P4** | 壳层功能迁入 Unity | **已定稿** | P1 | P2（可与 P3 错峰） |
| **UNITY-P5** | 发布与运维对齐 | **已定稿** | P1 | P4 主循环出口 |

**全局非目标（各阶段共用）**

- 不把 FSM / 咬钩权威搬进 Unity  
- 默认不改 Socket 事件名与主 DTO（除非 P0 书面批准）  
- Admin / 运营平台 **不**进 Unity  
- 千人多机（BE-OPT-E）与本移植正交，另立项  

---

## 2. UNITY-P0 — 决策与契约冻结

| 项 | 内容 |
|----|------|
| 状态 | **已实现** |
| 设计时间 | **2026-07-26** |
| 完成时间 | **2026-07-26** |
| 工期 | 约 1 短迭代 |

### 范围

| # | 产出 | 说明 |
|---|------|------|
| 1 | 迁移决策记录 | [`../architecture/Unity迁移决策记录.md`](../architecture/Unity迁移决策记录.md) |
| 2 | 契约冻结清单 v0 | [`../architecture/Unity契约冻结清单-v0.md`](../architecture/Unity契约冻结清单-v0.md) |
| 3 | 仓库形态决议 | monorepo `unity/`；`mobile/` 仅紧急修复 |
| 4 | 版本约定 | 文档 v0 + `protocolVersion` 基线 `1.0.0-draft` |

### 非目标

- 不重写 FSM、不换 SQLite、不建华丽场景  

### 验收

- [x] 决策记录合入 `docs/planning/architecture/`  
- [x] Socket/REST 清单可被 P1 直接当作输入  
- [x] 团队认同：主开发切 Unity 后 RN 仅紧急修复  

### 派发（已完成）

见 [`../prompts/unity-p0-decision-contract-dev.prompt.md`](../prompts/unity-p0-decision-contract-dev.prompt.md)

---

## 3. UNITY-P1 — 契约工程化

| 项 | 内容 |
|----|------|
| 状态 | **已实现** |
| 设计时间 | **2026-07-26** |

### 范围

| # | 工作 | 说明 |
|---|------|------|
| 1 | 拆分 shared | `contracts`（wire/展示常量）vs `rules`（咬钩公式，仅 server） |
| 2 | OpenAPI | 覆盖 world / auth / players / inventory / shop·gear·codex / friends·DM / posts / leaderboard |
| 3 | Socket 目录 | 事件名、payload、ack 错误码（Markdown + JSON Schema 或等价） |
| 4 | C# DTO | 从 schema 生成或手写首版；Unity 空工程可引用编译 |
| 5 | 兼容 | 服务端继续跑；契约与 TS 同源或生成校验，防漂移 |

### 验收（出口）

- [x] Unity 工程编译通过并引用 C# DTO
- [x] 有「禁止客户端掷骰 / 权威在服务端」说明页
- [x] 已完成契约与现有 server/shared 的冲突清单

**立项时**：状态改为已确认，另写/刷新专属 prompt。

---

## 4. UNITY-P2 — 网络薄客户端

| 项 | 内容 |
|----|------|
| 状态 | **已实现** |
| 设计时间 | **2026-07-26** |

### 范围

| # | 工作 | 说明 |
|---|------|------|
| 1 | Net | Socket.IO C#（默认兼容现协议；不优先改原生 WS） |
| 2 | Auth | 登录、存 JWT、`register_player` |
| 3 | 进塘闭环 | `join_pond` → `pond_snapshot` → `start_fishing` / `stop_fishing` → `fish_bite` → `accept_catch` |
| 4 | 调试 UI | 纯 UGUI：在线用户、相位文字、聊天一行 |
| 5 | 重连 | 对齐 leave / reconnect / session checkpoint 语义 |

### 验收（出口）

- [x] Windows Development Build 连接本地 Node，完整钓鱼并入库
- [x] 使用临时 UGUI，无需正式美术
- [x] 断线后可重新认证、进塘并恢复会话
- [x] 背包收到 `inventory_updated` 并显示鱼获数据

### 2026-08-12 验收记录

- Steam JWT 登录、Socket 认证、进塘、选钓位、开始/收杆、咬钩、领取鱼获和背包更新已通过真实联调。
- 服务端关闭后客户端不崩溃，重启服务端后可重新连接。
- `FISHING_TEST_MODE=instant` 已验证：测试鱼塘无鱼时会为选中钓位补充测试鱼，避免本地快速验收永久停留在 `waiting`。

---

## 5. UNITY-P3 — 2D 斜 45° 场景核心

| 项 | 内容 |
|----|------|
| 状态 | **已定稿** |
| 设计时间 | **2026-07-26** |
| 关联 | REF-SCENE-1 |

### 范围

| 模块 | 内容 |
|------|------|
| Tilemap | 方格地形；岸/水/路分层；四塘 theme |
| Camera | 正交；拖拽平移（可选缩放） |
| 坐标 | 格子 ↔ 世界 ↔ `spotId` 映射（可配置） |
| 角色 | 序列帧（待机/抛竿/等鱼/上钩）；Sorting by y |
| 反馈 | 飘字、气泡、上钩倒计时（表现层；数据来自 Socket） |
| 钓点 | 先映射现有 20 spot；点选入座若玩法另立 FEAT 则同期 |

### 验收（出口）

- [ ] 可拖拽查看大场景  
- [ ] 多人角色遮挡/排序可控  
- [ ] 与 P2 网络联通（场景上可见真实相位/渔获反馈）  

---

## 6. UNITY-P4 — 壳层功能迁入

| 项 | 内容 |
|----|------|
| 状态 | **已定稿** |
| 设计时间 | **2026-07-26** |

可拆子包（立项时可再拆编号）：

| 序 | 子包 | 说明 |
|----|------|------|
| 4.1 | 世界地图选塘 | 人数/主题 |
| 4.2 | 背包 / 商店 / 图鉴 | REST |
| 4.3 | 获鱼弹窗与分享 | 对齐 FEAT-UI 语义，实现重做 |
| 4.4 | 社交墙 / 好友 / DM | 可第二波 |
| 4.5 | 排行榜 | REST；口径跟服务端 |

### 验收（出口）

- [ ] 主循环「选塘→钓→收鱼→卖/背包」不依赖 Expo  

---

## 7. UNITY-P5 — 发布与运维对齐

| 项 | 内容 |
|----|------|
| 状态 | **已定稿** |
| 设计时间 | **2026-07-26** |

| # | 工作 | 说明 |
|---|------|------|
| 1 | 构建 | Windows Steam 桌面包 CI；环境变量指向 API；移动端不再是主发行目标 |
| 2 | 可观测 | 崩溃/日志对接 `client-logs` 或等价 |
| 3 | 并行退役 | 明确停 Expo 商店包时间点；协议兼容期 |
| 4 | 验收 | Unity 冒烟清单；Node `verify:*` 继续守服 |

### 验收（出口）

- [ ] 可提交 Steam 的最小可靠桌面包 + 回滚方案（协议未破时可保留 RN 紧急热修）  

---

## 8. 工作量与风险（继承蓝图）

| 块 | 量 | 风险 |
|----|----|------|
| P0–P1 契约 | 中 | 双端漂移 |
| P2 网络 | 中高 | Socket.IO C#、ack/重连 |
| P3 等距场景 | **高** | 美术管线、坐标与 spot |
| P4 全套社交 | 高 | 范围膨胀 |
| 服务端 | **低** | 默认不动 |

---

## 9. 与现需求关系

| 现编号 | 关系 |
|--------|------|
| REF-UNITY-1 | 工程路径参考；本清单为其可执行拆分 |
| REF-SCENE-1 | 体验目标；主要由 **UNITY-P3** 承接 |
| FEAT-UI-1/2 | HUD/Overlay **语义**可移植；实现在 Unity 重做 |
| FEAT / 社交服务端 | 已实现逻辑 **不因换端重做** |

---

## 10. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-08-13 | 完善 UNITY-EPIC 产品规划定义：补充背景目标、用户场景、阶段出口、权限边界、API/Socket 复用和总体验收；明确 P3～P5 尚未完成 |
| 2026-07-26 | 自 REF-UNITY-1 拆分 UNITY-P0～P5；P0 **已确认**，P1～P5 **已定稿** |
| 2026-07-26 | UNITY-P0 **已实现**：决策记录 + 契约冻结清单 v0；看板双页签（千人 / Unity） |
