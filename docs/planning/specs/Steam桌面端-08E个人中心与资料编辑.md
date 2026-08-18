# STEAM-DESKTOP-08E：个人中心与资料编辑

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-08E` |
| 类型 | 功能 |
| 状态 | **已实现** |
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
- 客户端不迁移 Web/RN 旧档，不允许客户端修改 SteamID 或权威统计；本次验收前由运维完成 SteamID 到既有玩家记录的受控绑定恢复。
- 可编辑的是游戏资料昵称/头像/展示鱼获，不是 Steam 账号名。
- 昵称长度、头像和展示鱼获权限沿用服务端规则。

## 验收

- [x] 登录后可打开个人中心。
- [x] 资料字段与服务端一致。
- [x] 编辑成功后重新打开页面能看到保存结果。
- [x] 非法输入、网络失败和权限拒绝有明确提示。
- [x] 编辑资料不离开鱼塘、不重建 Overlay。

## 验收记录

- 2026-08-18：用户验收通过。
- 验证个人资料保存、服务端回读、昵称/简介/头像/展示鱼获编辑、错误与重试状态。
- 验收中发现原 SteamID 绑定到了新 `playerId`；已在服务端事务中恢复到旧玩家记录，保留本次编辑的资料字段，并创建数据库备份。
- 恢复后旧玩家数据可见：2 条背包鱼获、1 条好友关联、2659 条钓鱼记录。

## 变更记录

- 2026-08-18：完成 Steam 桌面端个人中心、资料编辑、展示鱼获和 `menu_profile` 主窗口入口。
- 2026-08-18：完成 Steam 账号玩家绑定恢复并通过用户验收，状态改为「已实现」。

## 关联文件

- Web 基准：`mobile/app/profile.tsx`、`mobile/components/UserProfileModal.tsx`
- Unity 会话：`SteamAuthController`、`AuthenticatedApiClient`
