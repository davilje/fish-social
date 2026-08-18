# STEAM-DESKTOP-08C：动态墙与好友动态

> 状态：**已实现**（2026-08-19 用户验收通过）

请只实现 `STEAM-DESKTOP-08C`。

## 必读

- `docs/planning/specs/Steam桌面端-08C动态墙与好友动态.md`
- `mobile/app/social.tsx`
- `mobile/components/PostCard.tsx`
- `mobile/lib/socialApi.ts`

## 要求

- 创建独立 `PanelSocialFeed.prefab` 新页签，不要塞进 `PanelSocial` 聊天页。
- 使用独立动态卡片 Prefab，显示作者、时间、鱼获、可见范围、点赞和评论。
- 接入点赞、评论、删除自己的评论和实时动态事件。
- 处理加载、空数据、权限拒绝、失败和重试。
- 动态页切换不能触发 `leave_pond`。

## 禁止

- 不在本地伪造点赞、评论或可见性。
- 不在 Overlay 内渲染动态墙。
