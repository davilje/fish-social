# 开发交接提示词：Steam 桌面宠物与多人鱼塘表现层（STEAM-DESKTOP-07）

你是 Fish Social 的 Unity 前端开发 Agent。请在 `fish-social-unity/` 内实现 `STEAM-DESKTOP-07`，将现有 Steam 桌面壳、鱼塘会话和 Socket 快照收口为 2D 多人社交桌面宠物体验。

## 必读

1. `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md`
2. `docs/planning/specs/Steam桌面端产品定位与信息架构.md`
3. `docs/planning/specs/Steam桌面端独立游戏转型计划.md`
4. `docs/planning/specs/Unity Windows桌面端基础壳.md`
5. `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShellUi.cs`
6. `fish-social-unity/Assets/Scripts/Desktop/Social/SocialPondSessionController.cs`
7. `fish-social-unity/Assets/Scripts/Desktop/Social/SocialLobbyController.cs`

## 实现拆分

## 开发顺序与当前门禁

1. 先确认 `STEAM-DESKTOP-03` 的双 Steam 账号、邀请、Lobby→`pondId` 和权限验收；在 07C 开始前必须补齐该记录。
2. 按 `07A → 07B → 07C → 07D → 07E → 07F` 顺序执行；`STEAM-DESKTOP-ART-01` 可在 07B 表现接口稳定后并行。
3. `UNITY-P3～P5` 是架构出口，不是本提示词中的一次性开发任务：
   - P3 由 07B/07C 的场景与多人表现承接；
   - P4 由 07A/07D/07E/07F 的 Unity 主循环承接；
   - P5 由 07F 后的 Steam 发布、日志和回滚验收承接。
4. 已完成的 `UNITY-P0～P2`、Steam 认证、基础桌面壳、鱼塘会话和离线生态不重复开发。

### 07A：桌面宠物主视图

- 启动后先显示自己的 2D 猫咪宠物。
- 显示当前鱼塘/钓鱼/连接状态。
- 从主视图进入或恢复鱼塘。
- 不复制移动端完整页面。

### 07B：2D 鱼塘表现

- 显示鱼塘背景、水面、钓位和基础环境。
- 显示自己的宠物、钓位和钓鱼状态。
- 复用现有服务端 `pond_snapshot` 和会话数据。
- 使用可替换的表现接口，避免把玩法逻辑写进 UI。

### 07C：同塘玩家表现

- 处理 `pond_user_joined`、`pond_user_left`、`pond_user_updated`。
- 显示同塘玩家宠物形象、昵称和基础状态。
- 状态更新必须来自服务端，不得本地伪造多人在线状态。
- 玩家离开时清理对应表现对象。

### 07D：右键菜单

右键菜单只绑定 Fish Social 窗口/宠物区域，不拦截 Windows 桌面其他区域。

菜单入口：

```text
当前鱼塘
好友与聊天
鱼获/背包
图鉴
设置
隐藏到托盘
退出
```

### 07E：弹窗层

- 好友/聊天、背包、图鉴、设置使用弹窗或面板承载。
- 弹窗打开和关闭不得触发 `leave_pond`。
- 弹窗关闭后恢复原鱼塘和宠物表现状态。
- 暂无真实数据的入口保留明确占位，不伪造服务端结果。

### 07F：主流程和恢复

验证：

```text
Steam 启动
→ 登录
→ 桌面宠物
→ 进入鱼塘
→ 自己和其他玩家宠物
→ 开始钓鱼
→ 最小化/托盘挂机
→ 鱼咬钩通知
→ 恢复窗口收鱼
→ 右键打开弹窗
→ 关闭弹窗回到鱼塘
→ 断线重连后恢复服务端快照
```

## 严格边界

- 不修改 Node 鱼塘、生态、库存和钓鱼权威逻辑。
- 不把 Steam Lobby 当作鱼塘权威。
- 不在 Unity 本地生成伪造多人状态。
- 不触发无意义的 `leave_pond`。
- 不修改 `mobile/`、`server/`、`shared/`，除非发现必须修正的契约问题；契约问题先报告。
- 不提交 `Library/`、`Temp/`、`Obj/`、构建产物或密钥。

## 验收

- Unity Windows Development Build 可启动并显示桌面宠物。
- 能进入鱼塘并显示 2D 环境。
- 能显示自己和同塘其他玩家宠物。
- 玩家加入/离开/状态变化正确同步。
- 右键菜单和主要弹窗可用。
- 弹窗不导致离塘或状态重置。
- 托盘挂机、咬钩通知、恢复窗口和收鱼可用。
- 断线恢复后以服务端快照为准。

建议提交拆分：

```text
feat(unity): add desktop pet and pond presentation
feat(unity): render multiplayer pond pets
feat(unity): add context menu and modal layer
test(unity): verify desktop pet main flow
```

完成后提交 Unity Development Build 验证结果，并回写 `STEAM-DESKTOP-07` 的验收项。
