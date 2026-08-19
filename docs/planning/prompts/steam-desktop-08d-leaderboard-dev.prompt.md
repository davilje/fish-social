# 开发提示词：Steam 桌面端排行榜（STEAM-DESKTOP-08D）

> 状态：**已实现**（2026-08-20 用户终验通过）

你是 Fish Social **Unity Steam 桌面端 Agent**。按规格实现，勿扩需求。

请只实现 `STEAM-DESKTOP-08D`。不要改 08C。不要改 `mobile/`、`server/`、`shared/` 业务逻辑，不新增 `fishing_metrics`。

## 必读

1. `docs/planning/specs/Steam桌面端-08D排行榜.md`（**已实现** / **STEAM-DESKTOP-08D**）
2. `.cursor/rules/unity-desktop-dev-agent.mdc`
3. `mobile/components/LeaderboardPanel.tsx`
4. `mobile/lib/socialApi.ts`
5. `shared/social.ts` 中 `LeaderboardEntry` / `LeaderboardMyRank`

## 顺序

1. 新建独立 `PanelLeaderboard.prefab` 页签，不要塞进 `PanelSocial` / `PanelSocialFeed`。
2. 接入现有 REST（`limit=50`）：日榜、周榜、鱼塘榜、稀有榜、`my-rank`。
3. 上半固定领奖台（Image 占位：昵称、头像、用户数据）；下半仅纵向 `ScrollRect`。
4. 处理加载、空榜、刷新、401/403、超时、协议缺字段；切页不 `leave_pond`。
5. 自检 spec §5。

## 要求

- 四个子榜可切换：日榜、周榜、鱼塘榜、稀有榜。
- 鱼塘榜用当前会话 `pondId`；无塘时明确提示，不要伪造榜单。
- Overlay 可加 `menu_leaderboard` 打开主窗口排行榜页；不要在 Overlay 内画排行榜。
- 领奖台：左 2、中 1、右 3；区域独立不可滑动；缺人显示「虚位以待」。
- 第 4 名起：独立 `ScrollRect`，`horizontal = false`；不要用 ProgressBar。
- 周榜继续展示现有 `weekly-king` 服务端口径，不要在本需求改聚合。

## 禁止

- 不在 Unity 本地重新计算或替代服务端排序。
- 不改排行榜聚合口径。
- 不把领奖台和列表放进同一个可滑动容器。
- 不实现 `FEAT-SOC-03b`（不下线稀有/鱼塘榜，不改周榜为最大鱼）。

## 验收

对照 spec §5；已于 2026-08-20 用户终验通过。

- [x] 四榜切换、固定领奖台、纵向列表、我的排名
- [x] 切榜不离塘、不重建 Overlay
- [x] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
.cursor/rules/unity-desktop-dev-agent.mdc
@docs/planning/prompts/steam-desktop-08d-leaderboard-dev.prompt.md
按此实现 STEAM-DESKTOP-08D
```
