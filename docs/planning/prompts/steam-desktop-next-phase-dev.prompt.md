# Steam 桌面端下一阶段开发计划（07A～07G）

你是 Fish Social 的 Unity 前端开发 Agent。策划已完成下一阶段排期，本次只执行尚未完成的桌面宠物表现层，不重复已验收能力。

## 当前基线

- `UNITY-P0～P2`：已实现。
- `STEAM-DESKTOP-02`：已实现。
- `STEAM-DESKTOP-04`、`04A～04F`：已实现。
- `STEAM-DESKTOP-05`：已实现。
- `STEAM-DESKTOP-03`：已实现；核心链路已验证，双 Steam 账号联调因缺少第二测试账号跳过。
- `STEAM-DESKTOP-07A`：已实现；480×320 登录、1280×720 主窗口、底部导航、占位猫状态机，进入鱼塘调用原生 Overlay。
- `STEAM-DESKTOP-07B`：已实现；960×480 Overlay 鱼塘、钓位、128×128 自己的猫，由 pond_snapshot 驱动。
- `STEAM-DESKTOP-07C～07F`：已确认，待开发；Unity 主程序调用已实现的原生 Overlay，不启动第二个 Unity Player。
- `STEAM-DESKTOP-07G`：已实现，独立原生 Windows Overlay，不能通过第二个 Unity Player 实现。
- Unity 工程：`fish-social-unity/`。

## 本阶段目标

将已有 Steam 登录、Socket 鱼塘会话、桌面壳和服务端权威能力，收口为可见的 2D 多人社交桌面宠物主流程：

```text
480×320 登录窗口
→ Steam 登录
→ 1280×720 主窗口与小猫状态栏
→ 底部横向按钮行
→ 进入/恢复鱼塘，隐藏主窗口
→ 960×480 原生 Overlay
→ 看到鱼塘、自己的猫咪和同塘玩家（每只猫 128×128）
→ 开始/停止钓鱼
→ 托盘挂机与咬钩通知
→ 恢复窗口并收鱼
→ 右键菜单打开好友、背包、图鉴和设置
```

## 执行顺序

### 1. 执行 07A + 07B：主视图和鱼塘基础表现

对应 `UNITY-P3` 的第一部分。

- 启动后显示 `480×320` 登录窗口，登录成功后显示 `1280×720` 主窗口。
- 主窗口显示自己的 2D 猫咪状态栏，底部使用横向按钮行。
- 可进入或恢复当前鱼塘。
- 进入鱼塘时隐藏主窗口并调用 `960×480` 原生 Overlay。
- 显示 2D 鱼塘、水面、钓位、自己的猫咪和基础钓鱼表现。
- 使用真实 `pond_snapshot`、`pond_user_updated` 和当前会话状态。
- 场景对象必须可重复刷新，不能因快照/断线重复创建。

出口：Windows Development Build 可启动、登录、进入鱼塘、开始/收杆，并稳定显示自己的宠物与钓位。

### 2. 执行 07C：同塘玩家表现

对应 `UNITY-P3` 的多人表现出口。

- 显示同塘玩家昵称、宠物和基础钓鱼状态。
- 正确处理 `pond_user_joined`、`pond_user_left`、`pond_user_updated`。
- 处理多人遮挡和 y 排序。
- 不在 Unity 本地伪造在线玩家或钓鱼状态。

出口：至少双客户端真实联调通过，玩家进出和状态变化可见。

### 3. 执行 07D + 07E：菜单和弹窗

对应 `UNITY-P4` 的第一批功能迁入。

- 在 `960×480` Overlay 和主窗口产品区域提供右键菜单。
- 菜单入口：当前鱼塘、好友/聊天、鱼获/背包、图鉴、设置、托盘、退出。
- Overlay 菜单必须提供“打开主窗口”，恢复 `1280×720` 主窗口但不得触发 `leave_pond`。
- 用弹窗承载好友、聊天、背包、图鉴和设置。
- 弹窗打开/关闭不得触发 `leave_pond`，不得清空当前鱼塘状态。
- 复用现有 REST、Socket 和 Lobby Controller，不把业务权威写进 UI。

出口：所有入口可进入和返回，窗口外桌面不被拦截。

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
- 不在 07A～07F 中实现原生 Overlay 的窗口、进程和 IPC；调用 07G 已实现的稳定接口。
- 不启动第二个 Unity Player，不使用透明 Unity Overlay。
- 不把 Admin/运营平台迁入 Unity。

## 完成回写

每个子阶段独立验收后，更新对应 `STEAM-DESKTOP-07A～07G` spec、计划表状态和 `docs/planning/CHANGELOG.md`，然后运行：

```text
npm run planning:master-xlsx
```
