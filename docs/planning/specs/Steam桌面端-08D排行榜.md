# STEAM-DESKTOP-08D：排行榜

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-08D` |
| 类型 | 功能 |
| 状态 | **已确认** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P1 |
| 设计时间 | **2026-08-17** |
| 依赖 | 07E |

## 目标

将 Web 端排行榜迁移到 Steam 社交页的独立 `PanelLeaderboard.prefab`。

## 功能范围

- 日榜、周榜、鱼塘榜、稀有鱼榜。
- Top 50 列表。
- 玩家昵称、头像、排名、分数和当前玩家排名。
- 切换榜单、刷新、加载、空数据和错误状态。

## 入口与约束

- 独立 `PanelLeaderboard.prefab` 新页签；可加 Overlay `menu_leaderboard`。
- 不要把榜单塞进 `PanelSocial`。切页不 `leave_pond`。

## 数据与边界

- 排名和分数完全来自服务端。
- Unity 不重新计算排行榜分数，不本地排序替代服务端结果。
- 不在 Overlay 内显示排行榜；不改 metrics 聚合口径。

## 验收

- [ ] 四类榜单可以切换。
- [ ] Top 50 和我的排名显示正确。
- [ ] 刷新、空数据、断线和服务端错误有清晰反馈。
- [ ] 榜单切换不影响鱼塘、Overlay 和 Socket 会话。

## 关联文件

- Web 基准：`mobile/app/leaderboard.tsx`、`mobile/components/LeaderboardPanel.tsx`
- Unity 入口：`PanelRouter`、社交页签 Prefab
