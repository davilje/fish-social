# STEAM-DESKTOP-08E：个人中心与资料编辑

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-08E` |
| 类型 | 功能 |
| 状态 | **已确认** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P1 |
| 设计时间 | **2026-08-17** |
| 依赖 | 07E |

## 目标

在 Steam 主窗口增加个人中心和资料编辑页面，补齐 Web 端玩家资料能力。

## 功能范围

- 独立 `PanelProfile.prefab`。
- 独立 `PanelProfileEdit.prefab`。
- 昵称、头像/默认头像、玩家 ID、在线状态。
- 展示鱼获和基础统计。
- 编辑昵称、头像和展示内容。
- 保存中、成功、失败和重试状态。

## 入口与约束

- 主窗口页签：`PanelProfile.prefab` + `PanelProfileEdit.prefab`。
- 可加 Overlay `menu_profile`。编辑资料不 `leave_pond`、不重建 Overlay。

## 数据与边界

- Steam 身份和服务端 `playerId` 继续由 Steam 会话提供。
- 不迁移 Web/RN 旧档，不允许客户端修改 SteamID 或权威统计。
- 可编辑的是游戏资料昵称/头像/展示鱼获，不是 Steam 账号名。
- 昵称长度、头像和展示鱼获权限沿用服务端规则。

## 验收

- [ ] 登录后可打开个人中心。
- [ ] 资料字段与服务端一致。
- [ ] 编辑成功后重新打开页面能看到保存结果。
- [ ] 非法输入、网络失败和权限拒绝有明确提示。
- [ ] 编辑资料不离开鱼塘、不重建 Overlay。

## 关联文件

- Web 基准：`mobile/app/profile.tsx`、`mobile/components/UserProfileModal.tsx`
- Unity 会话：`SteamAuthController`、`AuthenticatedApiClient`
