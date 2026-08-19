<!-- 来源: docs/planning/specs/Steam桌面端-09AOverlay玩家右键菜单.md -->

你是 Fish Social **Unity + 原生 Overlay 开发 Agent**。实现 **STEAM-DESKTOP-09A：Overlay 玩家右键菜单**。

## 必读

1. [`docs/planning/specs/Steam桌面端-09AOverlay玩家右键菜单.md`](../specs/Steam桌面端-09AOverlay玩家右键菜单.md)
2. [`desktop-overlay/OverlayPetActor.cs`](../../../desktop-overlay/OverlayPetActor.cs) · [`MainWindow.xaml`](../../../desktop-overlay/MainWindow.xaml)
3. [`fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs`](../../../fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs)
4. Web 参考：[`mobile/components/UserProfileModal.tsx`](../../../mobile/components/UserProfileModal.tsx)

## 必须做

1. **WPF**：`OverlayPetActor` 右键弹出 ContextMenu（查看资料 / 添加好友 / 私聊 / 点赞互动）；不与场景级产品菜单冲突。
2. **IPC**：新增 `player_open_profile` / `player_add_friend` / `player_open_dm` / `player_like_recent`，带 `playerId` + `commandId`。
3. **Unity**：`DesktopAppBootstrap` 分发命令 → 现有 REST/Social API；需 `Show` 主窗口的项置顶并路由到对应 Panel。
4. **权限**：非好友、Bot、已申请等按 spec §3.3 灰态或 toast。
5. **回归**：不 `leave_pond`、不重建 Overlay/Socket。

## 不做

- Overlay 内完整资料页
- 新 Socket 协议
- 私聊 UI 在 Overlay

## 完成后

- [x] 勾选 spec §5
- [x] 验收后更新 spec 状态与 CHANGELOG
