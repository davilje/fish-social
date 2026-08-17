# STEAM-DESKTOP-08H：全量 UI Prefab 化与动态内容组件

请只实现 `STEAM-DESKTOP-08H`，不得修改 `mobile/`、`server/`、`shared/`。

## 必读

- `docs/planning/specs/Steam桌面端-08H全量UI预制体化.md`
- `docs/planning/specs/Steam桌面端-08F好友列表与申请Prefab.md`
- `.cursor/rules/unity-desktop-dev-agent.mdc`
- `.cursor/rules/planning-docs.mdc`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShellUi.cs`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopSocialModalView.cs`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopCatchBagModalView.cs`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopGalleryModalView.cs`
- `fish-social-unity/Assets/Scripts/Desktop/Editor/DesktopPrefabBaker.cs`

## 开发要求

1. 使用 Unity Editor `PrefabUtility.SaveAsPrefabAsset` 生成合法 Prefab，禁止手写
   `PrefabInstance` YAML。
2. 壳层、页面、列表行、Grid item 分层管理；Prefab 提供布局结构，View 只绑定数据
   和事件。
3. 生成并接入：
   - `DesktopShell`、`LoginView`、`MainView`、`HeaderStatus`、`BottomNavigation`
   - `PanelSocial` 内的 `PondPage`、`FriendsPage` 双栏页面
   - `OnlinePlayerRow`、`PondChatMessageRow`、`FriendRow`
   - `FriendRequestRow`、`SteamInviteRow`
   - `DirectMessageConversationRow`、`DirectMessageRow`
   - `CatchSlot`、`GallerySpeciesSlot`
4. 好友申请的接受/拒绝、Steam 好友邀请、私聊、移除必须是独立 Button，使用统一
   最小宽度、首选宽度、最小高度、首选高度和间距。
5. `PanelSocial` 统一承载在线钓友/鱼塘聊天和好友/私聊两个页面，不再接入
   `PanelFriends`、`PanelFriendRequests`、`PanelDirectMessages`、`PanelPondChat`
   等重复页面 Prefab。
6. 背包和图鉴 Grid item 必须从 Prefab 实例化，删除对应的 `new GameObject` UI 结构创建。
7. 处理 loading、empty、error、重试和刷新时，不重复创建 Socket，不触发离塘。
8. 所有文字、透明 Image 和父级拖动区域不得覆盖 Button 的 Raycast 区域。
9. 在 100%、125%、150% DPI 以及 1280×720、1024×600、800×600 下验证布局。

## 验收

- [ ] Unity Editor 菜单可生成并验证全部 Prefab。
- [ ] 好友、好友申请、Steam 邀请、私聊行均来自独立 Prefab。
- [ ] 接受、拒绝、邀请进塘、私聊、移除按钮可单独点击。
- [ ] 背包和图鉴每个 Grid item 来自独立 Prefab。
- [ ] 主窗口壳层不再由业务 View 运行时拼接。
- [ ] 不存在旧好友 UI 和新好友 UI 同时可见。
- [ ] Unity Windows Development Build 启动并完成主流程。

## 派发

建议角色：`@unity-desktop-dev`

执行命令：

`@docs/planning/prompts/steam-desktop-08h-ui-prefab-migration-dev.prompt.md 按此实现 STEAM-DESKTOP-08H`
