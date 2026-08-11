# Unity 移植工程路径蓝图（参考）

| 项 | 内容 |
|----|------|
| 文档性质 | **参考文档**（非开发需求 / 非功能规格） |
| 状态 | **已文档化** |
| 编号 | **REF-UNITY-1** |
| 设计时间 | **2026-07-26** |
| 关联 | [`钓鱼世界与鱼塘场景优化策略.md`](./钓鱼世界与鱼塘场景优化策略.md)（**REF-SCENE-1**）· [`../specs/Unity移植-分阶段需求清单.md`](../specs/Unity移植-分阶段需求清单.md)（**UNITY-P0～P5**）· [`../architecture/Unity迁移决策记录.md`](../architecture/Unity迁移决策记录.md) · [`../architecture/Unity契约冻结清单-v0.md`](../architecture/Unity契约冻结清单-v0.md) |
| 目标形态 | **Unity 2D 客户端 + 现有 Node 权威服务端**（推荐默认） |

> 用途：固定「切什么 / 留什么 / 按什么顺序做」的工程路径。  
> **可执行拆分**见 [`Unity移植-分阶段需求清单.md`](../specs/Unity移植-分阶段需求清单.md)；各阶段立项后走 `planning-progress-sync`。  
> 本文件本身仍为参考蓝图，不单独派发整包开发。

---

## 1. 当前代码架构（事实）

### 1.1 仓库切分

```text
fish-social/
├── mobile/        Expo 52 + RN 0.76 + expo-router（游戏客户端，含 Web）
├── server/        Express + Socket.io + better-sqlite3（玩法权威）
├── shared/        @fish-social/shared（类型、常量、公式、Socket 事件图）
├── admin-web/     Vite 运营/Admin 前端（浏览器）
├── scripts/       verify / 规划 / 模拟 / 运维脚本（Node）
└── docs/          策划、美术规范、运营说明
```

| 包 | 职责 | Unity 迁移默认处置 |
|----|------|-------------------|
| `mobile/` | 世界地图、鱼塘场景、社交/商店/背包 UI、进塘 Socket | **整体替换为 Unity 工程** |
| `server/` | FSM、生态、bot、库存、JWT、REST、实时 | **保留**（权威） |
| `shared/` | 契约 + 部分规则公式 | **拆成契约包**；公式继续服务端用 |
| `admin-web/` + 运营 HTML | 运维 | **保留浏览器**；不进 Unity |
| `scripts/` / `docs/` | 工程与策划 | **保留**；客户端验收改为 Unity 构建 |

### 1.2 客户端依赖方向

```text
app/（路由）
  → components/（场景与弹窗）
      → lib/hooks（usePondSocket / useWorldMap / …）
          → lib/apiClient · socialApi · auth
              → @fish-social/shared
              → fetch / socket.io-client
```

- 路由只做组合；联网集中在 hooks/API。  
- 例外：离线 demo 会在客户端本地模拟（**生产 Unity 路径禁止当权威**）。

### 1.3 服务端权威边界

| 必须服务端 | 客户端（表现） |
|------------|----------------|
| 占位 / 钓点、钓鱼相位 FSM、咬钩与收杆结果 | 渲染 `pond_snapshot` / tick / 飘字 |
| 生态库存、bot、每日 8h、饵消耗 | 发意图：`start_fishing` / `accept_catch` … |
| 库存/金币/社交写、排行榜聚合 | REST 拉列表与弹窗 UI |

核心模块：`fishingStateMachine` · `pondEcology` · `bots` · `gameState` · `inventory` · `socketPondHandlers`。

### 1.4 协议面（迁移时冻结）

**Socket C2S：** `register_player` · `join_pond` · `leave_pond` · `start_fishing` · `stop_fishing` · `send_chat` · `accept_catch`  

**Socket S2C：** `pond_snapshot` · `pond_ecology_updated` · `pond_user_*` · `session_timer_tick` · `chat_message` · `fish_bite` / `fish_miss` · `fishing_float_text` · `bait_depleted` · `gear_updated` · `codex_unlocked` · `inventory_updated` · 社交推送 · `error`

**REST 组：** `/api/world` · auth · players · inventory · shop/gear/codex · friends/DM · posts · leaderboard · client-logs · `/api/admin/*`

鉴权：JWT（Bearer + Socket `auth.token`）。

### 1.5 玩法关键持久化（SQLite）

`players` · `inventory` · `player_gear` · `pond_fish` / `pond_state` · `daily_fishing` · `player_pond_session` · `pending_catch_locks` · `fish_codex`  

（社交、埋点、配置审计为次要表，迁移客户端时不必动。）

### 1.6 与「目标体验」的缺口

现 `PondScene`：固定画布、伪等距色块、无 tilemap、无相机拖拽、无序列帧。  
Unity 2D 路径天然覆盖 REF-SCENE-1 中的斜 45° / 方格地形 / 拖拽视野 / 序列帧角色；**不**依赖把 RN 场景「平移」进引擎。

---

## 2. 目标架构（推荐）

```text
┌─────────────────────┐         JWT + REST + Socket.io        ┌──────────────────────┐
│  Unity 2D Client    │ ◄──────────────────────────────────► │  Node Server (现有)   │
│  · 等距 Tile 场景   │         契约：events + DTO            │  · FSM / 生态 / bot   │
│  · 序列帧角色       │                                       │  · SQLite             │
│  · UI（UGUI/UITK）  │                                       │  · 社交 / 商店 API    │
│  · NetClient 层     │                                       └──────────────────────┘
└─────────────────────┘
         │
         │ 可选同仓
         ▼
┌─────────────────────┐     ┌─────────────────────┐
│ contracts/          │     │ admin-web + ops     │
│ OpenAPI + 事件目录  │     │ 继续浏览器          │
│ (+ C# 生成)         │     └─────────────────────┘
└─────────────────────┘
```

**原则**

1. **权威不进客户端**：咬钩/收杆/占位只信服务端。  
2. **协议先冻结再换皮**：事件名与主 DTO 尽量不变。  
3. **场景表现在 Unity 重做**：不复用 RN View 层级。  
4. **运营与策划管线留在 Node/Web**。

---

## 3. 工程路径（阶段蓝图）

### Phase 0 — 决策与契约冻结（1 个短迭代）

| 产出 | 说明 |
|------|------|
| 迁移决策记录 | 确认「Unity 客户端 + Node 服」；RN Web 是否并行维护 |
| 契约清单 | 从 `shared/types.ts` 导出 Socket 事件表 + REST 路径表 |
| 仓库形态 | 建议 monorepo 增加 `unity/`（或独立 repo + submodule）；`mobile/` 冻结或仅修 bug |
| 非目标 | 本阶段不重写 FSM、不换 SQLite |

**出口：** 事件/DTO 有版本号；团队认同切线。

---

### Phase 1 — 契约工程化

| 工作 | 说明 |
|------|------|
| 拆分 shared | `contracts`（wire types / 常量展示）vs `rules`（咬钩公式等，仅 server） |
| OpenAPI | 覆盖玩家/库存/商店/世界/排行榜等主路径 |
| Socket 目录 | 事件名、payload、ack 错误码枚举（可 markdown + JSON Schema） |
| C# 生成 | 从 schema 生成 DTO（或手写首版再自动化） |
| 兼容策略 | 服务端仍消费现有 `shared`；契约与 TS 同源或生成校验 |

**出口：** Unity 空工程能编译引用同一套 DTO；有一份「禁止客户端掷骰」说明。

---

### Phase 2 — Unity 网络薄客户端（无华丽场景）

| 工作 | 说明 |
|------|------|
| Net | Socket.IO C# 客户端（或评估改原生 WebSocket——**默认优先兼容现协议**） |
| Auth | 登录 / 存 token / `register_player` |
| 进塘闭环 | `join_pond` → 收 `pond_snapshot` → `start_fishing` / `stop_fishing` → `fish_bite` → `accept_catch` |
| 调试 UI | 纯 UGUI 列表：在线用户、相位文字、聊天一行 |
| 重连 | 对齐现有 leave/reconnect / session checkpoint 语义 |

**出口：** 真机或 Editor 连现网（或本地 server）可完整钓一条鱼入库。  
**仍可用临时几何体代替美术。**

---

### Phase 3 — 2D 斜 45° 场景核心（产品差异点）

对照 REF-SCENE-1 / 种田式构建：

| 模块 | 内容 |
|------|------|
| Tilemap | 方格地形；岸/水/路分层；四塘可换 theme |
| Camera | 正交相机；拖拽平移（可选缩放） |
| 坐标 | 格子 ↔ 世界 ↔ 钓点 id 映射表（可配置） |
| 角色 | 序列帧（待机/抛竿/等鱼/上钩）；Sorting by y |
| 反馈 | 飘字、气泡、上钩倒计时（表现层；数据仍来自 Socket） |
| 钓点 | 可点选格子入座（若玩法 FEAT 同期立项）；否则先映射现有 20 spot |

**出口：** 可拖拽查看大场景；多人角色不穿模遮挡失控；与 Phase 2 网络联通。

---

### Phase 4 — 壳层功能迁入 Unity

按依赖迁，不必一次做完：

1. 世界地图选塘（人数/主题）  
2. 背包 / 商店 / 图鉴  
3. 获鱼弹窗与分享入口  
4. 社交墙 / 好友 / DM（可第二波）  
5. 排行榜  

Admin **不**迁 Unity（继续 `admin-web`）。

**出口：** 主循环（选塘→钓→收鱼→卖/背包）不依赖 Expo。

---

### Phase 5 — 发布与运维对齐

| 工作 | 说明 |
|------|------|
| 构建 | Android / iOS（+ 可选 PC）CI；环境变量指向 API |
| 可观测 | Unity 崩溃/日志对接现有 `client-logs` 或等价 |
| 并行策略 | 明确停 Expo 商店包时间点；协议兼容期 |
| 验收 | 新写 Unity 冒烟清单；Node 侧 `verify:*` 继续守服务端 |

**出口：** 可提交商店的最小可靠包 + 回滚方案（仍可回 RN 热修仅当协议未破）。

---

## 4. 工作量与风险（粗）

| 块 | 相对量 | 风险 |
|----|--------|------|
| 契约 + 生成 | 中 | 双端漂移 |
| 网络薄客户端 | 中高 | Socket.IO C# 生态、ack/重连细节 |
| 等距 Tile + 相机 + 序列帧 | **高** | 美术管线、性能、坐标与 spot 对齐 |
| 全套社交 UI | 高 | 范围膨胀 |
| 服务端 | **低**（默认不动） | 仅当改协议或容量方案时升高 |

**高风险决策**

- 是否在移植期改 Socket 协议 → 建议 **否**。  
- 是否双端同时维护 RN + Unity → 成本高；建议 Phase 2 起 **主开发只在 Unity**，RN 仅紧急修复。  
- 是否把 FSM 搬进 Unity → **否**。

---

## 5. 建议仓库落地形态

```text
fish-social/
├── server/          # 不变角色
├── shared/          # 过渡：逐步拆 contracts
├── contracts/       # 新增：OpenAPI + socket schema（可选路径）
├── unity/           # 新增：Unity 工程（Client）
├── mobile/          # 冻结 / 退役中
├── admin-web/
├── scripts/
└── docs/planning/product/  # 本蓝图与 REF-SCENE-1
```

Unity 内部分层建议：

```text
Assets/
  _Project/
    Net/           # Socket + REST + JWT
    Domain/        # 只读 DTO、本地表现状态（非权威）
    Scene/Isometric/  # Tile、Camera、SpotBinder
    Characters/    # 序列帧、排序
    UI/            # 壳层界面
    Config/        # 塘 theme、格子与 spot 映射
```

---

## 6. 与现策划文档关系

| 文档 | 关系 |
|------|------|
| REF-SCENE-1 | 体验目标（斜 45°、可读、分塘）；Unity Phase 3 承接 |
| FEAT-UI-1/2 | HUD/Overlay 语义可移植为 Unity UI 规则，实现作废重做 |
| 功能 FEAT（钓鱼/社交） | 服务端已实现部分 **不因换端重做**；仅补「点选钓位」等新玩法 FEAT |

---

## 7. 阶段出口检查（摘要）

- [ ] Phase 0：切线与协议冻结书面确认  
- [ ] Phase 1：契约可生成 C# DTO  
- [ ] Phase 2：Unity 联网完成「钓一条」  
- [ ] Phase 3：等距 Tile + 拖拽相机 + 序列帧角色  
- [ ] Phase 4：主循环壳层不依赖 Expo  
- [ ] Phase 5：商店构建与监控就绪  

---

## 8. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-26 | 初稿：架构事实 + Unity+Node 目标切线 + Phase 0–5 工程路径；登记 **REF-UNITY-1** |
| 2026-07-26 | 需求拆分：[`Unity移植-分阶段需求清单.md`](../specs/Unity移植-分阶段需求清单.md)（UNITY-P0～P5） |
