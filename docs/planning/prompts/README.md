?# 开发交接提示词（自动生成）

本目录存放从 `specs/*-开发交�?md` 提取�?**开�?Agent 提示�?*，避免每次手抄�?
## 生成

```bash
# 推荐：策划确认后一键登记计划表 + 生成 prompt
npm run planning:confirm -- v0.4.0

# 仅重新提�?prompt（计划表已是最新时可单独跑�?npm run planning:prompt -- v0.4.0
```

输出：`docs/planning/prompts/v0.4.0-dev.prompt.md`  
计划表：仓库根目�?`项目开发需求计划表.xlsx`（`docs/planning/` 为同步副本）

全链路说明：[策划到开发工作流.md](../策划到开发工作流.md)

### 专项 Bug 修复提示词（手维护）

| 文件 | 说明 |
|------|------|
| [bugfix-afk-disconnect-dev.prompt.md](./bugfix-afk-disconnect-dev.prompt.md) | 挂机断线 / 重连后误踢鱼塘（服务�?P0�?|
| [diag-afk-phase2-4-dev.prompt.md](./diag-afk-phase2-4-dev.prompt.md) | 挂机排查阶段 2�?（弱网矩�?/ leave_pond / Admin 时间线） |
| [server-observability-v0.4.4-dev.prompt.md](./server-observability-v0.4.4-dev.prompt.md) | 服务端架构缺陷修补与 observability 第一阶段（P0 埋点�?|
| [v0.4.4-observability-gap-dev.prompt.md](./v0.4.4-observability-gap-dev.prompt.md) | v0.4.4 剩余缺口补完（phase invalid / P2 / pending 慢测�?|
| [v0.4.4-metrics-reconciliation-dev.prompt.md](./v0.4.4-metrics-reconciliation-dev.prompt.md) | v0.4.4-patch2 埋点表与代码对齐（session_rebound / 别名 / xlsx�?|
| [server-architecture-v0.5-phase1-dev.prompt.md](./server-architecture-v0.5-phase1-dev.prompt.md) | v0.5 第一�?P0：鉴�?/ timerRegistry / 咬钩收敛 / checkpoint |
| [server-architecture-v0.5-phase2-dev.prompt.md](./server-architecture-v0.5-phase2-dev.prompt.md) | v0.5 第二�?P1：index 拆分 / SessionRegistry / 优雅停机 / correlationId |
| [server-architecture-v0.5-phase3-dev.prompt.md](./server-architecture-v0.5-phase3-dev.prompt.md) | v0.5 第三�?P2：增量广�?/ metrics 批量 / Admin 安全 / Bot 配置 |
| [pond-navigation-leave-fix-dev.prompt.md](./pond-navigation-leave-fix-dev.prompt.md) | 切页误离塘修复（显式 leave / unmount 不离�?/ reconnect�?|
| [bugfix-session-timer-broadcast-dev.prompt.md](./bugfix-session-timer-broadcast-dev.prompt.md) | 会话计时广播回归（waiting 阶段 sessionFishingMs 卡死�?|
| [data-platform-phase0-dev.prompt.md](./data-platform-phase0-dev.prompt.md) | 三层数据体系 Phase 0（Logger/健康/错误入库/日聚�?备份/日批�?|
| [bugfix-tsx-watch-hang-dev.prompt.md](./bugfix-tsx-watch-hang-dev.prompt.md) | BUG-11：tsx watch 启动挂死�?001 永不监听�?|
| [data-platform-dp-d-dev.prompt.md](./data-platform-dp-d-dev.prompt.md) | 数据平台 DP-D（D-L3-10 �?D-L3-06�?|
| [daily-ops-report-remaining-dev.prompt.md](./daily-ops-report-remaining-dev.prompt.md) | 运营日报剩余（R1→R3：生�?经济/留存/版本/Webhook�?|
| [feat05-c-phase-dev.prompt.md](./feat05-c-phase-dev.prompt.md) | FEAT-05 C 期（C1+C7→C5→C6→C2→C3；C4 不做�?|
| [backend-opt-a-security-dev.prompt.md](./backend-opt-a-security-dev.prompt.md) | BE-OPT-A 安全收口（SEC-01�?6�?|
| [backend-opt-b-perf-dev.prompt.md](./backend-opt-b-perf-dev.prompt.md) | BE-OPT-B 热路径性能（PERF-01�?5�?|
| [backend-opt-c-stability-dev.prompt.md](./backend-opt-c-stability-dev.prompt.md) | BE-OPT-C 停机与稳定性（STAB-01�?6）�?**已实�?* |
| [backend-opt-d-quality-dev.prompt.md](./backend-opt-d-quality-dev.prompt.md) | BE-OPT-D 工程债总包（QUAL-01�?0）�?**已实�?* |
| [backend-opt-d1-vitest-dev.prompt.md](./backend-opt-d1-vitest-dev.prompt.md) | BE-OPT-D 切片 D1 vitest · **已实�?* |
| [backend-opt-d2-pg-dev.prompt.md](./backend-opt-d2-pg-dev.prompt.md) | BE-OPT-D 切片 D2 PG Metrics · **已实�?* |
| [data-platform-phase2-dev.prompt.md](./data-platform-phase2-dev.prompt.md) | 数据平台 Phase2�?*�?DP-C �?*�?|
| [phase2-backend-dev.prompt.md](./phase2-backend-dev.prompt.md) | Phase2 工程轨（N1 ARC-09/10/BUG-08 �?N2 ARC-11�?|
| [daily-ops-report-v1-dev.prompt.md](./daily-ops-report-v1-dev.prompt.md) | 运营日报 v1 MVP（D-L3-01：上海日�?/ summary / HTML / cron�?|
| [admin-observability-v1.1-dev.prompt.md](./admin-observability-v1.1-dev.prompt.md) | Admin 排障增强 v1.1（live-state / Inspector / 重启埋点 / web 映射 / 锚点 BUG�?|
| [admin-observability-v1.2-dev.prompt.md](./admin-observability-v1.2-dev.prompt.md) | Admin 排障增强 v1.2（Timeline 着�?/ 双端 Debug / 人机分列�?|
| [v0.6.0-backend-dev.prompt.md](./v0.6.0-backend-dev.prompt.md) | FEAT-SOC-01/02/03 后端（点赞·评论·排行榜�?|
| [v0.6.0-frontend-dev.prompt.md](./v0.6.0-frontend-dev.prompt.md) | FEAT-SOC-01/02/03 前端（PostCard·排行�?Tab�?|
| [leaderboard-podium-home-dev.prompt.md](./leaderboard-podium-home-dev.prompt.md) | FEAT-SOC-03b 首页入口·领奖台·仅�?�?|
| [架构-R2-3-容量与真人隔�?dev.prompt.md](./架构-R2-3-容量与真人隔�?dev.prompt.md) | R2-3 可选软拒绝（MAX_HUMAN_SOCKETS / capacity 指标�?|
| [bugfix-bi-web-portal-dev.prompt.md](./bugfix-bi-web-portal-dev.prompt.md) | BUG-12：BI latest 404 + 游戏 Web :8082 可达�?|
| [bugfix-session-timer-zero-flash-dev.prompt.md](./bugfix-session-timer-zero-flash-dev.prompt.md) | BUG-13：垂钓中头顶反复显示 0 �?|
| [bugfix-daily-remaining-refresh-dev.prompt.md](./bugfix-daily-remaining-refresh-dev.prompt.md) | BUG-14：今日剩余钓鱼中不刷�?· **已实�?* |
| [bugfix-daily-quota-day-rollover-dev.prompt.md](./bugfix-daily-quota-day-rollover-dev.prompt.md) | BUG-15：今日额度跨日不刷新 / 误显已满 · **已实�?* |
| [bugfix-session-settlement-dev.prompt.md](./bugfix-session-settlement-dev.prompt.md) | BUG-16：断�?离塘未结算丢时长（后端）· **已实�?* |
| [bugfix-leave-pond-navigation-dev.prompt.md](./bugfix-leave-pond-navigation-dev.prompt.md) | BUG-17：离塘导航失效与收杆按钮闪烁（客户端）�?**已实�?* |
| [bugfix-pond-entry-stale-state-dev.prompt.md](./bugfix-pond-entry-stale-state-dev.prompt.md) | BUG-18：进塘首帧状态错误与演示降级 · **已实现** |
| [bugfix-daily-quota-single-source-dev.prompt.md](./bugfix-daily-quota-single-source-dev.prompt.md) | BUG-19：每日额度单一口径重构 · **已实现** |
| [bugfix-quota-remaining-display-regression-dev.prompt.md](./bugfix-quota-remaining-display-regression-dev.prompt.md) | BUG-20：进塘与钓鱼剩余展示回归 · **已实现** |
| [bugfix-desktop-exit-process-dev.prompt.md](./bugfix-desktop-exit-process-dev.prompt.md) | BUG-21：桌面端关闭后进程残留 · **已实现** |
| [bugfix-steam-lobby-permission-state-dev.prompt.md](./bugfix-steam-lobby-permission-state-dev.prompt.md) | BUG-22：Steam Lobby 权限诊断与状态回滚 · **已实现** |
| [steam-desktop-lobby-lifecycle-invite-feedback-dev.prompt.md](./steam-desktop-lobby-lifecycle-invite-feedback-dev.prompt.md) | STEAM-DESKTOP-06：Lobby 生命周期与邀请反馈优化 · **已跳过** |
| [metrics-hook-escape-counters-dev.prompt.md](./metrics-hook-escape-counters-dev.prompt.md) | D-L2-15：咬�?脱钩计数替代全量 tick |
| [metrics-phase-transition-compact-dev.prompt.md](./metrics-phase-transition-compact-dev.prompt.md) | D-L2-16：相位跳转短�?f/t/c · **已实�?* |
| [obs-log-noise-reduction-dev.prompt.md](./obs-log-noise-reduction-dev.prompt.md) | OBS-LOG-1：服务端日志降噪分层 · **已实�?* |
| [ops-portal-ux-dev.prompt.md](./ops-portal-ux-dev.prompt.md) | OPS-UX-1：运营平台今日运维条与深�?· **已实�?* |
| [admin-readable-ops-slim-dev.prompt.md](./admin-readable-ops-slim-dev.prompt.md) | ADMIN-OBS-1.3：Admin 表格化与入口精简 · **已实�?* |
| [admin-tabs-merge-charts-dev.prompt.md](./admin-tabs-merge-charts-dev.prompt.md) | ADMIN-OBS-1.4：页签合�?· 健康折线 · 删入口探�?· **已实�?* |
| [ops-growth-kpi-dashboard-dev.prompt.md](./ops-growth-kpi-dashboard-dev.prompt.md) | OPS-KPI-1：增�?留存/商业化看�?· **已实�?* |
| [ops-catch-inventory-human-bot-dev.prompt.md](./ops-catch-inventory-human-bot-dev.prompt.md) | OPS-CATCH-1：背包产量人机分�?· **已实�?* |
| [admin-business-health-catch-inventory-dev.prompt.md](./admin-business-health-catch-inventory-dev.prompt.md) | OPS-CATCH-1.1：Admin/健康产量对齐背包 · **已实�?* |
| [ops-release-hotupdate-dev.prompt.md](./ops-release-hotupdate-dev.prompt.md) | OPS-RELEASE-1：发版与热更策略 · **已实现** |
| [daily-fishing-shanghai-rollover-dev.prompt.md](./daily-fishing-shanghai-rollover-dev.prompt.md) | FISH-DAILY-1：上海日钓鱼时长重置 · **已实�?* |
| [bot-pool-reuse-cleanup-dev.prompt.md](./bot-pool-reuse-cleanup-dev.prompt.md) | FISH-BOT-1：机器人账号池与清理 · **已实�?* |
| [bot-spawn-pace-dev.prompt.md](./bot-spawn-pace-dev.prompt.md) | FISH-BOT-2：进塘节奏与初始时长 · **已实�?* |
| [fish-spot-manual-select-dev.prompt.md](./fish-spot-manual-select-dev.prompt.md) | FISH-SPOT-1：钓点手动选择 · **已确�?* |
| [pond-social-ui-optimize-dev.prompt.md](./pond-social-ui-optimize-dev.prompt.md) | FEAT-UI-1：鱼塘场景与社交列表 UI · **已实�?* |
| [pond-overlay-backpack-social-dev.prompt.md](./pond-overlay-backpack-social-dev.prompt.md) | FEAT-UI-2：叠加层·榜·bot动态·背�?0�?· **已实�?* |
| [unity-p0-decision-contract-dev.prompt.md](./unity-p0-decision-contract-dev.prompt.md) | UNITY-P0：决策与契约冻结 · **已实�?* |
| [unity-epic-phase-roadmap-dev.prompt.md](./unity-epic-phase-roadmap-dev.prompt.md) | UNITY-EPIC：Unity 移植分阶段需求总表 · **已定稿，P0～P2 已实现，P3～P5 待开发** |
| [steam-desktop-transition-dev.prompt.md](./steam-desktop-transition-dev.prompt.md) | STEAM-DESKTOP-EPIC：Steam 桌面端独立游戏转型与 Unity 并行开发 · **规划已完成，按子需求开发** |
| [steam-desktop-product-ia-dev.prompt.md](./steam-desktop-product-ia-dev.prompt.md) | STEAM-DESKTOP-01：Steam 桌面端产品定位与信息架构 · **规划已完成，转入 STEAM-DESKTOP-07** |
| [steam-desktop-07a-pet-home-dev.prompt.md](./steam-desktop-07a-pet-home-dev.prompt.md) | STEAM-DESKTOP-07A：Unity 主窗口内桌面宠物主视图与鱼塘入口（禁止第二 Unity Player）· **已实现** |
| [steam-desktop-07b-pond-scene-dev.prompt.md](./steam-desktop-07b-pond-scene-dev.prompt.md) | STEAM-DESKTOP-07B：2D 鱼塘环境与自己的猫咪 · **已实现** |
| [steam-desktop-07c-multiplayer-pets-dev.prompt.md](./steam-desktop-07c-multiplayer-pets-dev.prompt.md) | STEAM-DESKTOP-07C：Overlay 同塘玩家 + 本地序列帧 + 现有 Socket 同步 · **已实现** |
| [steam-desktop-07d-context-menu-dev.prompt.md](./steam-desktop-07d-context-menu-dev.prompt.md) | STEAM-DESKTOP-07D：桌面宠物右键菜单 · **已实现** |
| [steam-desktop-07e-main-window-tabs-dev.prompt.md](./steam-desktop-07e-main-window-tabs-dev.prompt.md) | STEAM-DESKTOP-07E：主窗口功能页签（取代弹窗；主窗口高于 Overlay）· **已实现** |
| [steam-desktop-07e-modal-layer-dev.prompt.md](./steam-desktop-07e-modal-layer-dev.prompt.md) | （作废）原 07E 弹窗层提示词，改走上一行 |
| [steam-desktop-07f-main-flow-qa-dev.prompt.md](./steam-desktop-07f-main-flow-qa-dev.prompt.md) | STEAM-DESKTOP-07F：桌面宠物主流程与恢复验收 · **已实现** |
| [steam-desktop-07g-native-overlay-dev.prompt.md](./steam-desktop-07g-native-overlay-dev.prompt.md) | STEAM-DESKTOP-07G：独立原生桌面宠物 Overlay（WPF/Win32 + Named Pipe）· **已实现** |
| [steam-desktop-art-resource-replacement-dev.prompt.md](./steam-desktop-art-resource-replacement-dev.prompt.md) | STEAM-DESKTOP-ART-01：桌面宠物与鱼塘视觉资源替换 · **已确认** |
| [steam-desktop-art-02-overlay-layout-pipeline-dev.prompt.md](./steam-desktop-art-02-overlay-layout-pipeline-dev.prompt.md) | STEAM-DESKTOP-ART-02：Overlay 场景布局管线（Prefab→JSON→像素摆放）· **已确认** |
| [steam-desktop-08a-world-map-dev.prompt.md](./steam-desktop-08a-world-map-dev.prompt.md) | STEAM-DESKTOP-08A：世界地图与鱼塘选择 · **已实现** |
| [steam-desktop-08b-shop-dev.prompt.md](./steam-desktop-08b-shop-dev.prompt.md) | STEAM-DESKTOP-08B：商店与装备 · **已实现** |
| [steam-desktop-08c-social-feed-dev.prompt.md](./steam-desktop-08c-social-feed-dev.prompt.md) | STEAM-DESKTOP-08C：动态墙与好友动态 · **已实现** |
| [steam-desktop-08d-leaderboard-dev.prompt.md](./steam-desktop-08d-leaderboard-dev.prompt.md) | STEAM-DESKTOP-08D：排行榜（四榜 + 固定领奖台）· **已实现** |
| [steam-desktop-08e-profile-dev.prompt.md](./steam-desktop-08e-profile-dev.prompt.md) | STEAM-DESKTOP-08E：个人中心与资料编辑 · **已实现** |
| [steam-desktop-08f-friends-prefab-dev.prompt.md](./steam-desktop-08f-friends-prefab-dev.prompt.md) | STEAM-DESKTOP-08F：好友列表与申请 Prefab · **已实现** |
| [steam-desktop-08g-overlay-fishing-controls-dev.prompt.md](./steam-desktop-08g-overlay-fishing-controls-dev.prompt.md) | STEAM-DESKTOP-08G：Overlay 钓鱼操作栏 · **已实现** |
| [steam-desktop-08h-ui-prefab-migration-dev.prompt.md](./steam-desktop-08h-ui-prefab-migration-dev.prompt.md) | STEAM-DESKTOP-08H：全量 UI Prefab 化与动态内容组件 · **已实现** |
| [steam-desktop-08i-pond-exit-switch-latency-dev.prompt.md](./steam-desktop-08i-pond-exit-switch-latency-dev.prompt.md) | STEAM-DESKTOP-08I：鱼塘退出、跨塘切换与 Overlay 延迟优化 · **已实现** |
| [steam-desktop-09a-overlay-player-menu-dev.prompt.md](./steam-desktop-09a-overlay-player-menu-dev.prompt.md) | STEAM-DESKTOP-09A：Overlay 玩家右键菜单 · **已实现** |
| [steam-desktop-09b-overlay-hover-status-dev.prompt.md](./steam-desktop-09b-overlay-hover-status-dev.prompt.md) | STEAM-DESKTOP-09B：Overlay 悬停钓鱼时长 · **已实现** |
| [steam-desktop-09c-overlay-pond-chat-dev.prompt.md](./steam-desktop-09c-overlay-pond-chat-dev.prompt.md) | STEAM-DESKTOP-09C：Overlay 鱼塘聊天气泡与输入 · **已实现** |
| [steam-desktop-09d-overlay-layout-dev.prompt.md](./steam-desktop-09d-overlay-layout-dev.prompt.md) | STEAM-DESKTOP-09D：Overlay 布局与角色表现优化 · **已实现** |
| [steam-desktop-public-server-url-dev.prompt.md](./steam-desktop-public-server-url-dev.prompt.md) | STEAM-DESKTOP-10：公网联调与可配置 serverBaseUrl · **已实现** |
| [steam-desktop-home-public-server-dev.prompt.md](./steam-desktop-home-public-server-dev.prompt.md) | STEAM-DESKTOP-10A：本机公网映射 · **已废弃（CGNAT）** |
| [feat-prog-01-pond-tiers-growth-dev.prompt.md](./feat-prog-01-pond-tiers-growth-dev.prompt.md) | FEAT-PROG-01：鱼塘分级与玩家成长 · **已实现** |
| [steam-desktop-11-local-onboarding-dev.prompt.md](./steam-desktop-11-local-onboarding-dev.prompt.md) | STEAM-DESKTOP-11：新手引导本地教学关 · **已实现** |
| [feat-gear-01-rods-baits-dev.prompt.md](./feat-gear-01-rods-baits-dev.prompt.md) | FEAT-GEAR-01：钓具与鱼饵配置 · **已实现** |
| [steam-desktop-08a2-map-zones-fee-dev.prompt.md](./steam-desktop-08a2-map-zones-fee-dev.prompt.md) | STEAM-DESKTOP-08A2：世界地图分区与进塘扣费确认 · **已实现** |
| [feat-risk-01-forbidden-police-dev.prompt.md](./feat-risk-01-forbidden-police-dev.prompt.md) | FEAT-RISK-01：禁止钓鱼塘巡警事件 · **已实现** |
| [feat-spot-01-spot-clue-bubbles-dev.prompt.md](./feat-spot-01-spot-clue-bubbles-dev.prompt.md) | FEAT-SPOT-01：坐席后聊天泡 / 表随机 habitat·activity · **已实现** |
| [steam-desktop-12-gameplay-debug-menu-dev.prompt.md](./steam-desktop-12-gameplay-debug-menu-dev.prompt.md) | STEAM-DESKTOP-12：玩法 Debug 菜单 · **已实现** |
| [steam-desktop-shell-dev.prompt.md](./steam-desktop-shell-dev.prompt.md) | STEAM-DESKTOP-04：Unity Windows 桌面端基础壳 · **已实现** |
| [steam-desktop-account-auth-dev.prompt.md](./steam-desktop-account-auth-dev.prompt.md) | STEAM-DESKTOP-02：Steam 身份、账号绑定与安全会话 · **开发中（真实登录已通过，REST/Socket 待验收）** |
| [steam-desktop-pond-offline-ecology-dev.prompt.md](./steam-desktop-pond-offline-ecology-dev.prompt.md) | STEAM-DESKTOP-05：空鱼塘休眠与生态离线补算 · **已确认** |
| [scene-iso-grid-dev.prompt.md](./scene-iso-grid-dev.prompt.md) | FEAT-SCENE-ISO-1/2：等距网格塘+世界 · **已实�?* |
| [scene-ortho-tilemap-dev.prompt.md](./scene-ortho-tilemap-dev.prompt.md) | FEAT-SCENE-TILE-1：星露谷式正�?Tilemap · **已实�?* |
| [scene-tile-camera-hud-dev.prompt.md](./scene-tile-camera-hud-dev.prompt.md) | FEAT-SCENE-TILE-2：相机拖拽与 HUD 分层 · **已实�?* |
| [scene-tile-perf-20ponds-dev.prompt.md](./scene-tile-perf-20ponds-dev.prompt.md) | FEAT-SCENE-TILE-3：性能与二十塘扩容 · **已实�?* |
| [scene-map-fullscreen-layers-dev.prompt.md](./scene-map-fullscreen-layers-dev.prompt.md) | FEAT-SCENE-TILE-4：全屏与层级缩放 · **已实�?* |

## 交给开�?Agent 的三种方�?
### 方式 A �?@ 引用（推荐，零配置）

1. **新开**开�?Agent 对话（与策划分离�?2. 输入：`@docs/planning/prompts/v0.4.0-dev.prompt.md` 然后发送「按此实现�?
无需复制全文，Cursor 会把文件内容带入上下文�?
### 方式 B �?策划 Agent 内派发子代理（同会话�?
策划结束时说：「请�?Task 启动开发子代理，读�?`prompts/v0.4.0-dev.prompt.md` 并实现。�?
你在同一会话里点一次批准即可，不用剪贴板�?
### 方式 C �?Cursor SDK 全自动（需 API Key�?
```bash
# 先生�?prompt 文件
npm run planning:prompt -- v0.4.0

# 配置后一键派�?Cloud Agent
set CURSOR_API_KEY=...
set GITHUB_REPO_URL=https://github.com/your-org/fish-social
npm run planning:dispatch -- v0.4.0
```

预览不发请求：`npm run planning:dispatch -- v0.4.0 --dry-run`

## 策划侧约�?
每个 `vX.Y.Z-开发交�?md` 必须包含�?
```markdown
## 交接提示词（复制给开�?Agent�?
\`\`\`
（完整提示词�?\`\`\`
```

策划 Agent 收尾时执�?`npm run planning:confirm -- vX.Y.Z`�?

