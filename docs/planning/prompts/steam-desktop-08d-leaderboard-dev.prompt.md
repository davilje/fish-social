# STEAM-DESKTOP-08D：排行榜

请只实现 `STEAM-DESKTOP-08D`。

## 必读

- `docs/planning/specs/Steam桌面端-08D排行榜.md`
- `mobile/app/leaderboard.tsx`
- `mobile/components/LeaderboardPanel.tsx`

## 要求

- 创建独立 `PanelLeaderboard.prefab` 新页签，不要塞进 `PanelSocial`。
- 支持日榜、周榜、鱼塘榜、稀有鱼榜和我的排名。
- 使用服务端排名和分数，支持刷新、加载、空数据和错误提示。
- 榜单切换不影响鱼塘、Overlay 或 Socket 会话。

## 禁止

- 不在 Unity 本地重新计算或替代服务端排序。
- 不在 Overlay 内显示排行榜。
