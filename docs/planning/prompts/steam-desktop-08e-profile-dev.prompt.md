# STEAM-DESKTOP-08E：个人中心与资料编辑

请只实现 `STEAM-DESKTOP-08E`。

## 必读

- `docs/planning/specs/Steam桌面端-08E个人中心与资料编辑.md`
- `mobile/app/profile.tsx`
- `mobile/components/UserProfileModal.tsx`

## 要求

- 创建 `PanelProfile.prefab` 和 `PanelProfileEdit.prefab`。
- 显示昵称、头像/默认头像、playerId、在线状态和展示鱼获。
- 支持编辑和保存，显示保存中、成功、失败和重试。
- 沿用服务端权限和字段，不迁移 Web 旧档，不修改 SteamID；可改的是游戏资料，不是 Steam 账号名。
- 编辑资料不离塘、不重建 Overlay。
