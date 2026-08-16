# Steam 桌面端下一阶段开发计划（07A～07G）

你是 Fish Social 的 Unity 前端开发 Agent。策划已完成下一阶段排期，本次只执行尚未完成的桌面宠物表现层，不重复已验收能力。

## 当前基线

- `UNITY-P0～P2`：已实现。
- `STEAM-DESKTOP-02`：已实现。
- `STEAM-DESKTOP-04`、`04A～04F`：已实现。
- `STEAM-DESKTOP-05`：已实现。
- `STEAM-DESKTOP-03`：已实现；核心链路已验证，双 Steam 账号联调因缺少第二测试账号跳过。
- `STEAM-DESKTOP-07A～07F`：已确认，待开发；Unity 主程序调用原生 Overlay，不启动第二个 Unity Player。
- Overlay 序列帧本地播放；IPC 只传 `petVisualState`，不传图；同塘同步沿用现有 Socket。
- `STEAM-DESKTOP-07G`：已确认，作为独立原生 Windows Overlay，不能通过第二个 Unity Player 实现。
- Unity 工程：`fish-social-unity/`。

## 本阶段目标

将已有 Steam 登录、Socket 鱼塘会话、桌面壳和服务端权威能力，收口为可见的 2D 多人社交桌面宠物主流程：

```text
Steam 登录
→ 宠物主视图
→ 进入/恢复鱼塘
→ 看到鱼塘、自己的猫咪和同塘玩家
→ 开始/停止钓鱼
→ 托盘挂机与咬钩通知
→ 恢复窗口并收鱼
→ 右键菜单打开好友、背包、图鉴和设置
```

## 执行顺序

### 1. 执行 07A + 07B：主视图和鱼塘基础表现

对应 `UNITY-P3` 的第一部分。

- 启动后显示自己的 2D 猫咪和登录/连接/钓鱼状态。
- 可进入或恢复当前鱼塘。
- 显示 2D 鱼塘、水面、钓位、自己的猫咪和基础钓鱼表现。
- 使用真实 `pond_snapshot`、`pond_user_updated` 和当前会话状态。
- 场景对象必须可重复刷新，不能因快照/断线重复创建。

出口：Windows Development Build 可启动、登录、进入鱼塘、开始/收杆，并稳定显示自己的宠物与钓位。

### 2. 执行 07C：同塘玩家表现

**已实现**（2026-08-15 用户验收）。对应 `UNITY-P3` 的多人表现出口。主要画在 `960×480` Overlay。

- 显示同塘玩家昵称、`128×128` 宠物和基础钓鱼状态。
- Unity 处理 `pond_snapshot`、`pond_user_joined`、`pond_user_left`、`pond_user_updated`，映射为 `petVisualState` 后推 Overlay。
- Overlay 用与自己的猫同一套本地序列帧渲染器；不连 Socket，IPC 不传贴图。
- 按 `playerId` 复用对象；断线用快照全量覆盖，不残留、不伪造。
- 打开主窗口不得离塘、不得重建会话。

出口：至少双客户端真实联调通过，玩家进出和状态变化可见，序列帧随状态切换。

### 3. 执行 07D + 07E：菜单和主窗口页签

对应 `UNITY-P4` 的第一批功能迁入。

- 在 Fish Social 窗口/宠物区域提供右键菜单。
- 菜单入口：当前鱼塘、好友/聊天、鱼获/背包、图鉴、设置、托盘、退出。
- 用主窗口页签承载好友、聊天、背包、图鉴和设置，不用功能弹窗。
- Overlay 菜单唤起后主窗口必须高于 Overlay；回托盘后 Overlay 恢复置顶。
- 切页/关窗不得触发 `leave_pond`，不得清空当前鱼塘状态。
- 复用现有 REST、Socket 和 Lobby Controller，不把业务权威写进 UI。

出口：所有入口可进入和返回，窗口外桌面不被拦截，功能页不被 Overlay 挡住。

### 4. 执行 07F：主流程与恢复验收

对应 `UNITY-P4` 主循环出口和 `UNITY-P5` 验收前置。

- 验证托盘隐藏后会话仍合法，后台降低渲染负载。
- 验证鱼咬钩通知、恢复窗口、收鱼和 `inventory_updated`。
- 验证 Socket 断线、服务端重启、重新认证和快照恢复。
- 完成 Windows Development Build 全流程冒烟。
- 记录已知问题、日志和回滚步骤。

## 并行任务

`STEAM-DESKTOP-ART-01` 可与 07B 之后并行，但资源替换不得阻塞程序验收，也不得要求重写业务脚本。

## 本阶段不做

- 不重写 Node FSM、咬钩公式、库存、生态或权限。
- 不重复开发 Steam 登录、P1/P2 网络闭环和 04 基础壳。
- 不实现 Steam Networking/Relay。
- 不做透明穿透桌面、系统级置顶或自由拖拽桌面宠物。
- 07G 另行使用 `steam-desktop-07g-native-overlay-dev.prompt.md` 实现，不得把原生 Overlay 逻辑混入 07A～07F。
- 不把 Admin/运营平台迁入 Unity。

## 完成回写

每个子阶段独立验收后，更新对应 `STEAM-DESKTOP-07A～07G` spec、计划表状态和 `docs/planning/CHANGELOG.md`，然后运行：

```text
npm run planning:master-xlsx
```
