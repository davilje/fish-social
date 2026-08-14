# STEAM-DESKTOP-07E：桌面宠物功能弹窗层

请在 `fish-social-unity/` 内实现桌面宠物功能弹窗层。

## 必读

- `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md`
- `docs/planning/specs/Steam桌面端产品定位与信息架构.md`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShellUi.cs`
- `fish-social-unity/Assets/Scripts/Desktop/Social/SocialLobbyController.cs`

## 范围

- 主窗口底部横向按钮行进入好友/聊天弹窗。
- 鱼获/背包弹窗。
- 图鉴弹窗。
- 窗口和通知设置弹窗。
- 统一打开、关闭、返回和错误提示接口。
- 从 Overlay 右键菜单点击“打开主窗口”后，主窗口可继续打开这些功能弹窗。
- 主窗口默认尺寸为 `1280×720`；弹窗不得破坏 Overlay 的 `960×480` 鱼塘会话。

## 强制边界

- 弹窗打开和关闭不得触发 `leave_pond`。
- 不得清空当前 pond、玩家宠物或多人场景状态。
- 暂无真实数据的功能可以使用明确占位，但不得伪造服务端结果。
- 业务数据继续从现有 API/Socket 获取。

## 验收

- 右键菜单可以打开各弹窗。
- 弹窗可关闭并回到原鱼塘状态。
- 弹窗切换不会重复创建或销毁鱼塘会话。
- 网络错误和空数据有清晰提示。
