# STEAM-DESKTOP-07D：桌面宠物右键菜单

请在 `fish-social-unity/` 内实现 Fish Social 产品区域的右键菜单。

## 必读

- `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md`
- `docs/planning/specs/Steam桌面端产品定位与信息架构.md`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShellUi.cs`

## 菜单入口

```text
当前鱼塘
好友与聊天
鱼获/背包
图鉴
设置
隐藏到托盘
退出
```

## 要求

- 右键菜单只作用于 Fish Social 窗口或桌面宠物区域。
- 不拦截 Windows 桌面其他区域的系统右键。
- 菜单项通过事件或接口调用，不把业务逻辑写死在菜单视图。
- 菜单关闭、重复打开和窗口切换状态正确。

## 边界与验收

- 不实现弹窗内部业务，那属于 07E。
- 右键能打开、点击能分发到对应入口。
- 隐藏到托盘和退出调用现有桌面生命周期服务。
- 菜单操作不会触发 `leave_pond`。
