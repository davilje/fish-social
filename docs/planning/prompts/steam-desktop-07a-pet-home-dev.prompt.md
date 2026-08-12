# STEAM-DESKTOP-07A：桌面宠物主视图与鱼塘入口

请在 `fish-social-unity/` 内实现桌面宠物主视图与鱼塘入口。

## 必读

- `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md`
- `docs/planning/specs/Steam桌面端产品定位与信息架构.md`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShellUi.cs`

## 范围

- 启动后显示自己的 2D 猫咪宠物。
- 显示当前 Steam 登录、Socket、鱼塘和钓鱼状态。
- 提供进入/恢复当前鱼塘入口。
- 提供返回桌面宠物主视图入口。
- 先允许使用简化占位美术资源，不阻塞交互验证。

## 边界

- 不实现多人鱼塘场景细节、右键菜单、完整弹窗。
- 不修改 Node、Steam 认证和鱼塘权威逻辑。
- 不修改 `mobile/`、`server/`、`shared/`。

## 验收

- Windows Development Build 启动后可看到桌面宠物。
- 登录状态和当前钓鱼状态可见。
- 可进入鱼塘并返回桌面宠物视图。
- 状态切换不会创建重复会话。
