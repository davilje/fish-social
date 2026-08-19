# STEAM-DESKTOP-08D：排行榜

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-08D` |
| 类型 | 功能 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P1 |
| 设计时间 | **2026-08-17** |
| 完成时间 | **2026-08-20** |
| 依赖 | 07E |
| 目标开发 Agent | Unity Steam 桌面端 |

## 1. 背景与目标

### 1.1 背景

Web 社交已有排行榜接口和页面。Steam 主窗口尚未独立排行榜页。桌面端继续展示四类榜，不沿用 `FEAT-SOC-03b` 的「仅日/周、下线稀有/鱼塘」口径。

### 1.2 目标

将 Web 端排行榜迁到 Steam 主窗口独立 `PanelLeaderboard.prefab`：

- 日榜、周榜、鱼塘榜、稀有榜可切换。
- Top 1–3 固定领奖台；4 名及以后纵向滚动列表。
- 显示玩家昵称、头像、排名、用户数据和当前玩家排名。

### 1.3 非目标

- 不改服务端排行榜聚合口径、缓存或 `fishing_metrics`。
- 不在 Unity 本地重算分数或替代服务端排序。
- 不在 Overlay 内绘制排行榜。
- 不把榜单塞进 `PanelSocial` / `PanelSocialFeed`。
- 不实现 `FEAT-SOC-03b` 的 Web 首页入口、删稀有/钓场 UI、周榜改最大鱼。
- 切页、切榜不 `leave_pond`，不重建 Overlay / Socket。

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 玩家 | 打开排行榜页签或 Overlay 菜单 | 进入主窗口独立排行榜页，鱼塘会话保持 |
| 玩家 | 切换日/周/鱼塘/稀有 | 领奖台与列表刷新对应榜数据 |
| 玩家 | 查看前三 | 固定领奖台显示昵称、头像、用户数据；缺人显示空位 |
| 玩家 | 查看 4 名及以后 | 仅上下滑动列表，不能横向拖动 |
| 玩家 | 无数据 / 断线 / 无当前鱼塘 | 明确空态、错误和重试，不伪造榜单 |

## 3. 功能范围

### 3.1 功能列表

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 独立页签 | P0 | `PanelLeaderboard.prefab`；可加 Overlay `menu_leaderboard` |
| 2 | 四榜切换 | P0 | 日榜、周榜、鱼塘榜、稀有榜 |
| 3 | 固定领奖台 | P0 | Top 1–3，Image 占位，不可滑动 |
| 4 | 纵向列表 | P0 | 第 4 名起，`ScrollRect` 仅垂直 |
| 5 | 我的排名 | P0 | 复用 `/api/leaderboard/my-rank` |
| 6 | 加载/空/错误/重试 | P0 | 含 401/403、超时、协议缺字段 |

### 3.2 交互与 UI

页面分区必须拆开，领奖台不得放进可滚动容器：

```
[ 日榜 ] [ 周榜 ] [ 鱼塘榜 ] [ 稀有榜 ]     ← 固定

┌ 领奖台（固定，不可滑）─────────────────┐
│  Image#2          Image#1          Image#3 │
│  左第2            中第1            右第3   │
│  头像/昵称/数据   头像/昵称/数据   头像/昵称/数据 │
└──────────────────────────────────────────┘

┌ ScrollRect 仅垂直，horizontal = false ──┐
│  4  头像  昵称  用户数据                 │
│  5  …                                   │
│  …  Top 50                              │
└──────────────────────────────────────────┘
我的排名：#n / 未上榜
```

**领奖台**

- 视觉顺序：左 2、中 1、右 3；中间最高。
- 每位用 Prefab 内 `Image` 做底板占位，不在运行时生成几何体替代 Prefab。
- 每位 Image 内必须有独立绑定占位：
  - 用户昵称
  - 用户头像
  - 用户数据（当前榜的服务端成绩字段）
- 缺人保留空位，显示「虚位以待」，不用假数据填满。

**后续排名**

- 领奖台下方独立 `ScrollRect`。
- 只允许上下滑动；禁用横向滚动（`horizontal = false`）。
- 不要用 ProgressBar；这里是纵向滚动条。
- 行显示名次、头像、昵称、用户数据。

**用户数据字段（只展示服务端值）**

| 榜 | 用户数据 |
|---|---|
| 日榜 | 最大鱼尺寸（`extra.sizeM` / `value`）及鱼种（若有） |
| 周榜 | 现有 `weekly-king` 的 `value`（出售金币口径，本需求不改） |
| 鱼塘榜 | 本塘条数；可附最大鱼尺寸 |
| 稀有榜 | 史诗+ 条数；可附最大鱼尺寸 |

鱼塘榜使用当前会话 `pondId`；无塘时提示先进入鱼塘，不请求伪造榜。

### 3.3 入口与约束

- 主窗口新页签；Overlay 只发 `menu_leaderboard` 打开该页。
- 切页、切榜、打开菜单不得触发 `leave_pond`。

## 4. 技术影响

### 4.1 数据模型

复用 `LeaderboardEntry` / `LeaderboardMyRank`。Unity 侧增加对应 DTO，协议缺字段视为失败。

### 4.2 API

| 类型 | 名称 | 说明 |
|------|------|------|
| REST | `GET /api/leaderboard/daily-biggest?limit=50` | 日榜 |
| REST | `GET /api/leaderboard/weekly-king?limit=50` | 周榜 |
| REST | `GET /api/leaderboard/pond/:pondId?limit=50` | 鱼塘榜 |
| REST | `GET /api/leaderboard/rare?limit=50` | 稀有榜 |
| REST | `GET /api/leaderboard/my-rank?boardType=` | `daily_biggest` / `weekly_king` / `pond` / `rare` |

Steam JWT 鉴权。不新增接口，不改服务端排序。

### 4.3 涉及文件（预估）

- `fish-social-unity/`：`AuthenticatedApiClient`、`DesktopShellUi`、`ShellPanelId`、产品菜单、Prefab 工厂/烘焙
- `desktop-overlay/`：可选 `menu_leaderboard`
- 参考：`mobile/components/LeaderboardPanel.tsx`、`mobile/lib/socialApi.ts`

## 5. 验收标准

- [x] 四类榜单可以切换。
- [x] Top 1–3 固定领奖台：左 2 / 中 1 / 右 3，使用 Image 占位，含昵称、头像、用户数据；缺人为空位。
- [x] 领奖台区域不可滑动；第 4 名及以后在下方纵向 `ScrollRect`，横向滚动关闭。
- [x] Top 50 和我的排名显示正确，成绩来自服务端。
- [x] 刷新、空数据、断线和服务端错误有清晰反馈。
- [x] 榜单切换不影响鱼塘、Overlay 和 Socket 会话。

## 6. 验收记录

| 日期 | 结果 | 说明 |
|------|------|------|
| 2026-08-19 | 通过 | 用户验收：四榜切换、固定 Image 领奖台、纵向列表、我的排名与错误态可用；切榜不离塘 |
| 2026-08-20 | 通过 | 终验：Tab 切换/缓存预拉修复后，反复切榜与进页行为符合 spec §5 |

## 7. 关联文件

- Spec：本文件
- 提示词：`docs/planning/prompts/steam-desktop-08d-leaderboard-dev.prompt.md`
- 实现：`DesktopLeaderboardPanel.cs`、`PanelLeaderboard.prefab`、`LeaderboardRow.prefab`、`menu_leaderboard`
- Web 参考：`mobile/app/leaderboard.tsx`、`mobile/components/LeaderboardPanel.tsx`
- 不在本需求实现：`docs/planning/specs/排行榜-入口与领奖台改版.md`（`FEAT-SOC-03b`）

## 8. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-08-17 | 初稿：Steam 主窗口独立排行榜页，四类榜 + 我的排名 |
| 2026-08-19 | 用户确认：保留日/周/鱼塘/稀有四榜；Top3 固定 Image 领奖台（昵称/头像/用户数据）；4 名及以后仅纵向滚动 |
| 2026-08-19 | 用户验收通过，状态改为「已实现」 |
| 2026-08-20 | 终验通过：Tab 切换无卡死、缓存秒开、后台预拉四榜 |
