# STEAM-DESKTOP-08F：好友列表与申请 Prefab

请只实现 `STEAM-DESKTOP-08F`，优先修复好友申请按钮重叠问题。

## 必读

- `docs/planning/specs/Steam桌面端-08F好友列表与申请Prefab.md`
- `mobile/app/social.tsx`
- `mobile/lib/socialApi.ts`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopSocialModalView.cs`
- `fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelSocial.prefab`

## 要求

- 从 `PanelSocial` 抽出好友/申请为 `PanelFriends.prefab`，禁止两套好友 UI 并存。
- 好友申请行使用独立的接受/拒绝 Button、最小宽度和间距。
- 支持好友列表、搜索、邀请、接受、拒绝、移除和私聊入口。
- 验证 100%/125%/150% DPI 下按钮仍可单独点击。
- 操作后刷新服务端状态，不离塘、不创建第二个 Socket。

## 禁止

- 不使用覆盖整行的透明按钮承载两个操作。
- 不把好友申请继续全部运行时拼装在旧页面中。
