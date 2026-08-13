# Unity 迁移决策记录（UNITY-P0）

| 项 | 内容 |
|----|------|
| 编号 | **UNITY-P0** |
| 状态 | **已实现**（决策冻结） |
| 设计时间 | **2026-07-26** |
| 完成时间 | **2026-07-26** |
| 来源 | [`../product/Unity移植工程路径蓝图.md`](../product/Unity移植工程路径蓝图.md)（REF-UNITY-1） |
| 契约清单 | [`Unity契约冻结清单-v0.md`](./Unity契约冻结清单-v0.md) |

---

## 1. 目标形态（已确认）

| 决策 | 结论 |
|------|------|
| 客户端 | **Unity Windows 桌面端**（2D 斜 45° / Tilemap / 序列帧 / 桌面助手壳） |
| 平台接入 | **Steamworks 原生接口**：Steam Ticket、SteamID64、好友、Lobby、邀请；第一阶段不替换 Node 实时传输 |
| 服务端 | **现有 Node**（Express + Socket.io + SQLite）保持玩法权威 |
| Admin / 运营 | **继续浏览器**（`admin-web/`、运营平台）；不进 Unity |
| 权威边界 | FSM / 咬钩 / 占位 / 库存写 **禁止**迁入 Unity |

---

## 2. 仓库形态

| 决策 | 结论 |
|------|------|
| 布局 | **monorepo** 增加 `unity/`（与 `mobile/`、`server/`、`shared/` 同仓） |
| 不采用（默认） | 独立 Unity 仓库（除非后续发行合规强制拆仓，再另立决策） |
| `shared/` | P1 起拆 `contracts`（wire）vs `rules`（仅 server）；本阶段仅冻结清单 |

---

## 3. `mobile/`（Expo / RN）冻结策略

| 阶段 | 策略 |
|------|------|
| **P0 起（即日起）** | 主开发切向 Unity Windows/Steam；`mobile/` **仅紧急修复**（崩溃、阻断进塘、严重安全） |
| **功能增量** | 新玩法 / UI / 壳层默认只做 Unity（P2 起）；不在 RN 双写 |
| **并行期** | 协议兼容期内可保留 RN 内测包作对照；不以商店双端并行为默认目标 |
| **退役** | 由 **UNITY-P5** 书面定停商店包时间点 |

团队认同口径：**主开发切 Unity 后，RN 仅紧急修复。**

---

## 4. 协议 / DTO 版本约定

| 约定 | 说明 |
|------|------|
| 文档版本 | 契约清单文件名带 `v0` / `v1`…；重大不兼容升主版本 |
| 运行时字段 | 建议在 `pond_snapshot`（或握手 ack）增加可选 `protocolVersion: string`（例 `"1.0.0"`）；**P1 工程化时落地代码**，P0 只冻结命名 |
| 变更纪律 | 默认 **不改** Socket 事件名与主 DTO 字段名；破例须书面批准并升 `protocolVersion` |
| 鉴权 | JWT：`Authorization: Bearer` + Socket `auth.token`（不变） |

当前冻结基线：`protocolVersion` 文档基线 **`1.0.0-draft`**（见契约清单 v0 头表）。

---

## 5. 非目标（本决策不覆盖）

- 不重写 FSM、不换 SQLite、不建 Unity 场景  
- 不改默认 Socket 事件名  
- 不把千人多机（BE-OPT-E / S4）绑进本迁移  

---

## 6. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-26 | UNITY-P0 决策合入；确认 Unity+Node、monorepo `unity/`、`mobile/` 紧急修复策略、协议版本约定 |
