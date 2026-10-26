# STEAM-DESKTOP-08C：动态墙与好友动态

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-08C` |
| 类型 | 功能 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P1 |
| 设计时间 | **2026-08-17** |
| 依赖 | 07E |

## 目标

在 Steam 社交页增加 Web 端公共动态墙和好友动态，不复制 Web 路由，使用桌面滚动页签。

## 功能范围

- 独立 `PanelSocialFeed.prefab`。
- 公共动态墙、好友动态和滚动加载。
- 鱼获分享卡片、玩家头像、昵称、时间和可见范围。
- 点赞、评论、删除自己的评论。
- 分享后刷新动态。
- `post_liked`、`post_commented` 等实时更新。

## 入口与约束

- 独立 `PanelSocialFeed.prefab` 作为新页签，不要把动态墙塞进现有 `PanelSocial` 的聊天页。
- Overlay 菜单可加 `menu_feed` 切到该页；不在 Overlay 渲染动态。
- 切页不 `leave_pond`。

## 数据与边界

- 动态内容、点赞数、评论和可见权限由服务端决定。
- 沿用 `public/friends` 可见性规则。
- 不在 Unity 本地伪造点赞、评论或动态数量。
- 不把 Web 路由、排行榜或商店并入本页。

## 验收

- [x] 公共动态墙和好友动态可切换。
- [x] 鱼获分享后能在动态墙显示。
- [x] 点赞、评论和删除操作结果与服务端一致。
- [x] 空数据、加载失败、权限拒绝和重试状态清晰。
- [x] 动态页切换不触发 `leave_pond`。

## 验收记录

- 2026-08-19：用户验收通过，动态墙、好友动态、动态卡片 Prefab、分页加载、点赞、评论、删除评论及实时刷新完成。

## 关联文件

- Web 基准：`mobile/app/social.tsx`、`mobile/components/PostCard.tsx`、`mobile/lib/socialApi.ts`
- Unity 入口：现有 `DesktopSocialModalView`、`PanelRouter`
