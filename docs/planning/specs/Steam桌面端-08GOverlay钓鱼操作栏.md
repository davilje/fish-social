# STEAM-DESKTOP-08G：Overlay 钓鱼操作栏

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-08G` |
| 类型 | 功能 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P0 |
| 设计时间 | **2026-08-17** |
| 依赖 | 07B、07C、07G |

## 目标

将选择钓鱼点位、开始钓鱼、收杆和领取鱼获迁移到 `960×480` 原生 Overlay，减少主窗口和 Overlay 之间的操作切换。

## 功能范围

- Overlay 底部常驻操作栏。
- 钓位选择。
- 开始钓鱼。
- 收杆。
- 领取鱼获。
- 当前钓位、Fishing Phase、待领取鱼获和操作可用状态。
- 操作失败、网络断开和服务端拒绝提示。

## 交互

- 选钓位：点击 Overlay 场景里已有钓位标记；操作栏只反映当前选中/占用状态，不另做一套完整鱼塘 UI。
- 开始 / 收杆 / 领取：操作栏按钮；按服务端 phase 启用或禁用。
- 挂机主路径以 Overlay 为准。主窗口可保留同等入口作后备，但必须走同一套 `SocialPondSessionController`，禁止两套权威。

## 技术边界

- Overlay 只显示按钮、状态并发送 Named Pipe 命令，**不连 Socket、不跑 uGUI、不传图**。
- Unity `SocialPondSessionController` 继续执行真实业务操作。
- 服务端继续负责钓位权限、钓鱼状态、咬钩、逃脱、鱼获和库存。
- 新增版本化命令：`take_spot`、`start_fishing`、`stop_fishing`、`accept_catch`。命令可带 `spotId` 等字段，不含贴图。
- Unity 推送当前 phase、可用操作、当前钓位、待领取鱼获和错误信息。
- Overlay 场景拖动只能作用于背景或空白区域，不能覆盖操作按钮的命中测试；透明区仍可点击穿透。
- 操作失败不 `leave_pond`、不重建 Overlay。

## 验收

- [x] Overlay 可以选择不同钓位。
- [x] Overlay 可以开始钓鱼、收杆和领取鱼获。
- [x] 按钮状态随服务端 phase 正确禁用/启用。
- [x] 操作结果和错误信息能回显到 Overlay。
- [x] 不绕过 Unity/Node 权威逻辑。
- [x] 不触发重复 Socket、重复 Overlay 或错误离塘。
- [x] Overlay 拖动不影响按钮点击。

## 验收记录

- 2026-08-17：用户验收通过。Overlay 钓位选择、开始钓鱼、收杆、领取鱼获、状态同步、错误回显及拖动/按钮命中测试均通过。

## 关联文件

- Unity 会话：`SocialPondSessionController.cs`
- Unity IPC：`NativeOverlayStateDto.cs`、`NativeOverlayProcessController.cs`
- Overlay：`desktop-overlay/MainWindow.xaml`、`MainWindow.xaml.cs`
