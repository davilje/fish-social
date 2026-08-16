# Steam 桌面宠物与多人鱼塘表现层

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Steam 桌面宠物与多人鱼塘表现层 |
| 编号 | **STEAM-DESKTOP-07** |
| 类型 | Unity 功能开发 |
| 负责人 | Unity 前端工程师 |
| 状态 | **已确认** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P0 |
| 设计时间 | **2026-08-13** |
| 上位需求 | `STEAM-DESKTOP-EPIC`、`STEAM-DESKTOP-01` |

## 1. 目标

将已完成的 Steam 登录、鱼塘会话、多人快照、桌面壳和生态能力，收口为“2D 多人社交桌面宠物”的可见产品体验。

本需求只负责 Unity 表现层和交互层，不重新实现 Node 权威逻辑。

### 1.1 与先前 web / RN 版本的关系

Steam 桌面版**不平移**移动端页面，也不迁移旧档。鱼塘、钓鱼、库存和在线状态仍由同一套 Node + Socket.IO 权威，表现层不同：

| 项 | web / RN | Steam 桌面（本需求） |
|----|----------|----------------------|
| 权威 | `pond_snapshot`、`pond_user_joined/left/updated`、`fish_bite` | 相同，不改协议 |
| 鱼塘画面 | RN 正交 Tilemap + 头像二头身 | `960×480` 原生 Overlay 占位场景，猫咪基准 `128×128` |
| 钓鱼动画 | 头像 + phase 文案/气泡/咬钩环，无序列帧状态机 | `PetStateController` + 序列帧渲染器；Overlay **本地播帧** |
| 入口 | 打开鱼塘页 | `480×320` 登录 → `1280×720` 主窗口 → Overlay 挂机 |
| 账号 | 移动端档 | Steam 新档 |

07C 不新开网络协议，只把已有快照/事件反映到 Overlay。

## 2. 拆分阶段

| 阶段 | 内容 | 依赖 | 优先级 |
|------|------|------|--------|
| STEAM-DESKTOP-07A | 桌面宠物主视图与鱼塘入口 | STEAM-DESKTOP-04 | P0 |
| STEAM-DESKTOP-07B | 2D 鱼塘环境、钓位和自己的猫咪 | STEAM-DESKTOP-04、UNITY-P1/P2 | P0 |
| STEAM-DESKTOP-07C | 同塘玩家宠物、昵称、钓鱼状态和进出场同步 | STEAM-DESKTOP-03、UNITY-P1/P2 | P0 |
| STEAM-DESKTOP-07D | 产品区域右键菜单 | 07A | P0 |
| STEAM-DESKTOP-07E | 好友、聊天、背包、图鉴、设置主窗口页签 | 07D | P0/P1 |
| STEAM-DESKTOP-07F | 托盘、通知、断线恢复和完整主流程验收 | 07A～07E | P0 |

## 3. 产品主流程

```text
Steam 启动
→ Steam 登录
→ 显示桌面宠物主视图
→ 选择或恢复鱼塘
→ 获取 pond_snapshot
→ 显示池塘、自己的猫咪和同塘玩家宠物
→ 选择钓点并开始钓鱼
→ 最小化/隐藏到托盘继续挂机
→ 鱼咬钩通知
→ 恢复窗口并收鱼
→ 右键打开主窗口对应页签（好友、背包、图鉴或设置）
→ 主窗口盖在 Overlay 之上；隐藏主窗口后 Overlay 恢复置顶
```

## 4. 表现与交互要求

### 4.1 桌面宠物

- 桌面宠物是主视觉，不是普通头像。
- 07A～07F 第一阶段只使用 Unity 主程序的普通 Windows 窗口，不启动第二个 Unity Player。
- 透明、置顶、桌面穿透和自由拖拽不属于 07A～07F 的实现范围，后续由独立的原生 Overlay 需求（`STEAM-DESKTOP-07G`）承接。
- 宠物主视图必须能显示当前钓鱼状态和最近通知。
- 第一阶段先使用空白正方形 2D 猫咪占位 UI，建议基准尺寸 `256×256`，保持等比例显示。
- 占位资源通过独立 Sprite/Texture2D 或 Prefab 引用接入，后续替换正式猫咪资源不得修改布局、状态控制和网络业务代码。
- 首版动画方案采用“序列帧 + 宠物状态机”，不把 Spine 作为 07A 的前置依赖。
- 状态机至少覆盖 `idle`、`fishing`、`hooked`、`catching`、`dragging`、`offline`；没有正式美术时可复用同一张占位图，通过状态文字/颜色区分。
- 状态机与渲染器解耦。Unity 主窗口使用 `SpriteFramePetRenderer`；Overlay 使用同等接口的本地序列帧渲染器（WPF）。后续可增加 Spine，不得改动网络、窗口和鱼塘权威逻辑。
- **Overlay 支持序列帧，且必须本地播放：** Unity 只通过 Named Pipe 推送 `petVisualState`（及位置/钓位），**不传输图片或逐帧数据**。Overlay 按状态切本地帧循环；主窗口隐藏时 Overlay 仍继续播，不依赖 Unity 渲染循环。
- 序列帧资源应使用统一尺寸、锚点和中心点，避免状态切换时角色跳位；动画播放速度和循环规则由渲染器配置，不写入服务端。可替换路径：主窗口 `Resources/Pet/`，Overlay 旁 `OverlayResources/`（如 `cat-idle-0.png`）。

### 4.2 多人鱼塘

- 使用服务端快照渲染鱼塘环境和在线玩家。07B/07C 的鱼塘场景和玩家宠物**优先画在 Overlay**；主窗口保留状态栏和恢复入口。
- 自己和其他玩家使用统一的 2D 宠物表现接口（同一状态枚举、同一 `128×128` 基准、同一序列帧渲染器）。
- 至少显示昵称、宠物形象和基础钓鱼状态。
- Unity 订阅 `pond_snapshot`、`pond_user_joined`、`pond_user_left`、`pond_user_updated`，把 `fishingPhase` 映射为 `petVisualState` 后推给 Overlay。Overlay **不连接** REST/Socket，不维护第二套状态机。
- 断线重连以服务端 `pond_snapshot` 全量覆盖 Overlay 角色列表，按 `playerId` 复用对象，离开则删除；不得残留、不得本地伪造多人状态。
- 打开主窗口、隐藏 Overlay 或切换面板不得 `leave_pond`，不得重建会话。

### 4.3 右键菜单与主窗口页签

右键菜单只作用于 Fish Social 窗口或宠物区域，不拦截 Windows 桌面其他区域。

07E 的 Web→Steam 字段、权限、REST/Socket 映射以
[`Steam桌面端Web功能对齐设计.md`](./Steam桌面端Web功能对齐设计.md) 为准。
好友/聊天、背包、图鉴、设置是 **主窗口页签**，不是盖在窗口上的功能弹窗；不复制 Web 的移动端路由。

菜单至少包含：

- 当前鱼塘
- 好友与聊天
- 鱼获/背包
- 图鉴
- 设置
- 打开主窗口
- 隐藏到托盘
- 退出

Overlay 或主窗口右键点到功能项后：显示并聚焦 Unity 主窗口，`PanelRouter` 切到对应页签。**此时主窗口必须高于 Overlay**，功能页不得被 Overlay 挡住；主窗口隐藏到托盘后 Overlay 恢复置顶。打开、关闭、切页不得触发 `leave_pond`，不得清空当前鱼塘和宠物状态。

## 5. 技术边界

- Unity 主程序负责表现、输入、普通窗口、页签、托盘、通知，以及 **唯一** 的 Steam/JWT/Socket.IO 连接。
- 原生 Overlay 只渲染 Unity 推送的场景与状态（含序列帧），不加载 Unity 场景、不运行 Steam/Socket/鱼塘权威逻辑。
- Node 负责鱼塘、生态、钓鱼、库存、社交和在线状态权威。
- 继续使用现有 JWT + Socket.IO；07C **不新增** 网络协议，只扩展 Overlay 的状态 DTO（同塘用户列表）。
- 不把 Steam Lobby 作为鱼塘权威。
- 不新增一塘一进程。
- 07A～07F 禁止通过启动第二个 Unity Player 实现透明桌面宠物。
- 不在本阶段接入 Spine Runtime、正式猫咪美术或复杂换装；Spine 作为后续可替换渲染器。
- 不修改 `mobile/`。
- 不将 `Library/`、`Temp/`、构建产物和密钥提交到版本库。

## 6. 验收标准

- [ ] 启动后可看到桌面宠物主视图。
- [ ] 可进入鱼塘并看到 2D 鱼塘环境。
- [x] 可看到自己的猫咪和至少一名同塘玩家宠物。
- [x] 玩家加入、离开、钓鱼状态变化能正确反映。
- [x] 右键菜单能打开并执行主要入口。
- [x] 好友、聊天、背包、图鉴、设置使用主窗口页签承载（不用功能弹窗）。
- [x] Overlay 菜单唤起主窗口后，主窗口高于 Overlay；回托盘后 Overlay 恢复置顶。
- [x] 07E 的背包、图鉴、鱼塘聊天、好友和私聊核心数据与 Web 端语义一致。
- [x] 07E 正确处理 loading、empty、error、401/403、断线和重试状态。
- [x] 07E 不重复创建 Socket、鱼塘会话或 Overlay。
- [x] 打开/关闭主窗口或切换页签不会离塘或重置鱼塘状态。
- [ ] 最小化/托盘隐藏后仍保持合法挂机。
- [ ] 鱼咬钩通知、恢复窗口和收鱼流程可用。
- [ ] 断线恢复后以服务端快照恢复，不显示伪造多人状态。
- [ ] Unity Windows Development Build 通过完整主流程，且进入鱼塘不会启动第二个 Unity Player。

## 7. 下一阶段开发计划

### 7.1 执行顺序

| 顺序 | 需求 | 主要目标 | 出口 |
|------|------|----------|------|
| 1 | `STEAM-DESKTOP-07A` | 空白正方形猫咪主视图、登录/Socket/钓鱼状态和鱼塘入口 | 可启动、显示宠物、进入/恢复鱼塘 |
| 2 | `STEAM-DESKTOP-07B` | 2D 鱼塘、钓位、自己的猫咪和钓鱼状态 | 真实 `pond_snapshot` 驱动鱼塘表现 |
| 3 | `STEAM-DESKTOP-07C` | Overlay 同塘玩家、昵称、进出场；统一序列帧接口；沿用现有 Socket | 多客户端状态正确呈现，IPC 不传图 |
| 4 | `STEAM-DESKTOP-07D` | 窗口内右键菜单和功能路由 | 菜单可用且不影响窗口外桌面 |
| 5 | `STEAM-DESKTOP-07E` | 好友/聊天、背包、图鉴、设置主窗口页签 | 切页不离塘；菜单唤起时主窗口高于 Overlay |
| 6 | `STEAM-DESKTOP-07F` | 托盘、通知、断线恢复和完整主流程验收 | Windows Development Build 全流程通过 |
| 7 | `STEAM-DESKTOP-07G` | 独立原生桌面宠物 Overlay；仅通过 Named Pipe 接收状态和发送命令 | Overlay 不影响 Unity 主窗口，支持透明、置顶、拖动和关闭 |

`STEAM-DESKTOP-03` 的核心功能已实现；双 Steam 账号联调因缺少第二测试账号跳过，不阻塞 07A 开发。`STEAM-DESKTOP-ART-01` 可在 07B 接口稳定后并行替换正式资源。`STEAM-DESKTOP-ART-02` 为后续：用 Unity Canvas Prefab 导出 Overlay 像素布局，有表后停用自动缩放。

### 7.3 动画实现决策

| 决策项 | 本阶段方案 | 后续替换 |
|--------|------------|----------|
| 角色状态来源 | 现有鱼塘会话事件和窗口交互事件；`fishingPhase` → `petVisualState` | 不变 |
| 动画驱动 | `PetStateController` 状态机，Unity 与 Overlay 共用同一枚举 | 不变 |
| 主窗口渲染器 | `SpriteFramePetRenderer` | 可替换为 `SpinePetRenderer` |
| Overlay 渲染器 | 本地序列帧（WPF）；按 `petVisualState` 切帧，不经 IPC 传图 | 可替换为同状态名的 Spine/正式图 |
| 资源要求 | 统一尺寸、锚点、状态命名和循环配置 | Spine 动画名映射到同一状态枚举 |
| 业务边界 | 动画只表现状态，不决定鱼塘、库存或钓鱼结果；帧率与循环不写入服务端 | 不变 |

该决策属于 `STEAM-DESKTOP-07A` 收口，`STEAM-DESKTOP-07C` 把同一套序列帧接到 Overlay 上自己的猫和同塘玩家。不新增需求编号。只有当 Spine 引入换装、角色养成或新的交互业务时，才需要另立需求。

### 7.2 本阶段不做

- 不重写 Node 鱼塘 FSM、咬钩公式、库存、生态或权限。
- 不重复开发 Steam 登录、P1/P2 网络闭环和 04 基础壳。
- 不实现 Steam Networking/Relay、透明穿透桌面或系统级置顶；这些能力只在 07G 的原生 Overlay 中实现。
- 不因打开菜单、页签或设置而执行 `leave_pond`。

## 8. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-16 | 策划 | `STEAM-DESKTOP-07E` 用户验收通过：主窗口页签承载好友/背包/图鉴/设置；Overlay 菜单切页且主窗口高于 Overlay。父需求 07 仍为已确认 |
| 2026-08-16 | 策划 | 07E 改口径：功能弹窗改为主窗口页签；Overlay 菜单切页；主窗口必须高于 Overlay |
| 2026-08-16 | 策划 | 后续美术 `STEAM-DESKTOP-ART-02`：Overlay 布局管线；不纳入 07A～07G 本期出口 |
| 2026-08-16 | 主 Agent | `STEAM-DESKTOP-07D` 用户验收通过：主窗口与 Overlay 产品区域右键菜单可打开并路由到现有入口；隐藏到托盘/退出走现有生命周期；不拦截桌面系统菜单、不 `leave_pond`。父需求 07 仍为已确认 |
| 2026-08-15 | 主 Agent | `STEAM-DESKTOP-07C` 用户验收通过：Overlay 同塘玩家/机器人、昵称、进出场与 petVisualState 本地序列帧已落地；计划状态改为已实现。父需求 07 仍为已确认 |
| 2026-08-15 | 主 Agent | 07C 开工前收口：桌面与 web/RN 权威相同、表现不同；Overlay 本地序列帧（IPC 只传 `petVisualState`）；同塘同步沿用现有 Socket，不新开协议 |
| 2026-08-14 | 主 Agent | 确认 07A 采用“序列帧 + 状态机”作为首版动画方案；渲染器与状态机解耦，Spine 后置为可替换实现 |
| 2026-08-14 | 主 Agent | 因第二 Unity Player + UniWindowController 方案导致全屏、Skybox、主窗口阻塞和高资源占用，明确 07A～07F 不启动 Unity Overlay，新增独立原生 Overlay 需求 07G |
| 2026-08-14 | 主 Agent | 确认 STEAM-DESKTOP-03 核心链路已实现；下一阶段从 07A 开始，先使用可替换的空白正方形猫咪占位 UI |
| 2026-08-13 | 主 Agent | 将 STEAM-DESKTOP-EPIC / 01 拆分为桌面宠物、多人鱼塘表现、右键菜单和弹窗的可执行 Unity 需求 |
