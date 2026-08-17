# STEAM-DESKTOP-08H：全量 UI Prefab 化与动态内容组件

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-08H` |
| 类型 | 工程 / UI |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P0 |
| 设计时间 | **2026-08-17** |
| 依赖 | 07E、08F |

## 目标

将 Steam 桌面端 Unity UI 从“代码创建结构、运行时拼接内容”迁移为
“Prefab 提供结构、运行时代码只绑定数据和事件”，解决不同分辨率下的布局漂移、
按钮重叠、Grid 内容裁切和重复 UI 问题。

## Prefab 分类

### 1. 壳层与公共组件

- `DesktopShell.prefab`
- `LoginView.prefab`
- `MainView.prefab`
- `HeaderStatus.prefab`
- `BottomNavigation.prefab`
- `ToastMessage.prefab`
- `PrimaryButton.prefab`
- `SecondaryButton.prefab`
- `TextStatus.prefab`
- `SearchInput.prefab`

### 2. 功能页面

- `PanelSocial.prefab`
- `PanelCatch.prefab`
- `PanelGallery.prefab`
- `PanelSettings.prefab`

`PanelSocial.prefab` 内部统一承载 `PondPage` 和 `FriendsPage` 两个双栏页面；
在线钓友、鱼塘聊天、好友和私聊不再拆成互相独立的页面 Prefab。

### 3. 列表行组件

- `OnlinePlayerRow.prefab`
- `PondChatMessageRow.prefab`
- `FriendRow.prefab`
- `FriendRequestRow.prefab`
- `SteamInviteRow.prefab`
- `DirectMessageConversationRow.prefab`
- `DirectMessageRow.prefab`

### 4. Grid 内容组件

- `CatchSlot.prefab`
- `GallerySpeciesSlot.prefab`
- 后续商店、排行榜、动态墙、地图坐标使用各自独立 item Prefab。

## 实现边界

- Prefab 负责节点层级、RectTransform、LayoutGroup、颜色、字体、按钮尺寸和
  Raycast 配置。
- 运行时代码只负责实例化 Prefab、填充 DTO、绑定 Button/Toggle/InputField 回调。
- 好友申请的接受、拒绝必须是两个独立 Button。
- Steam 好友的“邀请进塘”必须是独立 Button，并与接受、拒绝、私聊、移除统一尺寸。
- Grid 的每一格必须从对应 item Prefab 实例化，不得继续直接 `new GameObject`。
- 列表行必须从对应 row Prefab 实例化，不得在业务 View 内重复创建 UI 结构。
- 同一功能只能保留一套可见 UI；好友与私聊统一由 `PanelSocial` 的
  `FriendsPage` / `FriendsChatPage` 承载。
- Unity 仍是认证、Socket、鱼塘和业务命令唯一入口。

## 非目标

- 不修改 `mobile/`、`server/`、`shared/` 的业务规则。
- 不把数据 DTO、网络请求或权限判断写入 Prefab。
- 不在本需求中新增商店、地图、排行榜或动态墙业务接口。
- 不使用手写 Unity YAML 伪造 Prefab。

## 验收标准

- [x] 主窗口登录、首页、顶部栏、底部栏和 Toast 均由 Prefab 提供结构。
- [x] 在线钓友、鱼塘聊天、好友、好友申请、Steam 邀请、私聊均使用独立行 Prefab。
- [x] 接受、拒绝、邀请进塘、私聊、移除均为独立 Button，尺寸统一且可单独点击。
- [x] 背包每个鱼获格使用 `CatchSlot.prefab`。
- [x] 图鉴每个鱼种格使用 `GallerySpeciesSlot.prefab`。
- [x] 100%、125%、150% DPI 和至少三种窗口尺寸下无裁切、重叠和越界。
- [x] 不存在旧好友页和新好友页同时可见的两套 UI。
- [x] 切页、刷新和处理好友请求不离开鱼塘、不创建第二个 Socket。
- [x] 删除或停用业务 View 内用于创建同类 UI 结构的运行时代码。
- [x] Unity Editor Prefab 验证工具和 Windows Development Build 均通过。

## 变更记录

- **2026-08-17**：用户验收通过。确认 `PanelSocial` 双页结构、好友/私聊切换、
  独立列表行与 Grid item Prefab、响应式布局和按钮交互符合需求。

## 关联文件

- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShellUi.cs`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopSocialModalView.cs`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopCatchBagModalView.cs`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopGalleryModalView.cs`
- `fish-social-unity/Assets/Scripts/Desktop/Editor/DesktopPrefabBaker.cs`
- `fish-social-unity/Assets/Resources/Desktop/Prefabs/`
