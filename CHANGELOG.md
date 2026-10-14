?# Changelog

## Unreleased

- **全桌面 UI 统一与预制体接线修复（2026-10-14）**：纠正 ScrollRect/布局组件 GUID、重复组件及四个缺失节点；新增 21 个基础控件与 Variant 库，将 41 个界面/卡片/HUD 资源改为真正 Variant 和嵌套组件，按 23 个页面设计精细重排；WPF Overlay 全面同步 73 张同源 PNG、三态按钮、纸底、聊天和菜单，并保护 HUD 的 32 个 id 与 Unity 导出格式。加入 Player 构建前资源检查。静态检查已执行，Unity/Windows 引擎与联机视觉由用户验收；说明见 `docs/design/ui/unified/README.md`。

- **Unity UI 导入错误修正（2026-10-14）**：修正 `CozyPrefabRepair.cs.meta` 的 33 位无效 GUID，解除修复类被忽略导致的 CS0103；关闭旧 ProfileHub 在脚本导入后的自动 Bake，增加 GUID 唯一性及导入回归测试。递归序列化报错仍需完整 Unity Console 堆栈确认；本机无 Unity，尚未执行 Edit Mode 测试。

- **桌面 UI 全面持久化修复（2026-10-14）**：按确认的樱花鱼塘横版首页恢复七项导航；提供完整首页、钓鱼、选塘、装备、成就与模态窗口 Prefab，主题 PNG 改为真正导入的 Resources Sprite 与 Sliced 九宫格，停止运行时重建布局，保留既有业务与 v18 动画。静态数据/C# 语法检查通过；Unity 编译、运行与联机视觉仍待验收。说明：`docs/design/ui/2026-10-14-cozy-prefab-repair.md`。

- **钓鱼动画 v18 同步（2026-10-13）**：Unity 与 Windows Overlay 接入九层逐帧顺序和时长，新增四帧坐下、六帧空军和两种四帧复位，保留六帧快速收杆、循环发力及待领取保持；加入五种既有配色素材和 Editor 预览。11 项移植像素验证及 23 项源回归通过，C# 构建/联机视觉待验收。说明：`docs/art/animations/2026-10-13-fishing-animation-port.md`。

- **Unity 最终版 UI 移植（2026-10-13）**：接入已确认的奶油纸张/单色雕刻 PNG，重排十一项桌面导航及业务页面，新增真实目录鱼塘选择、复用已有 API 的装备视图，整理切图和 EditMode 测试。保留 Steam、服务端、Socket、Windows Overlay；本机未运行 Unity，视觉/联机验收待执行。接入说明：`docs/design/ui/2026-10-13-cozy-unity-port.md`。

- **BUG-26**：用户验收通过。右键菜单稳住直到左键 Overlay；关菜单后悬停用自有开关 + `GetCursorPos` 恢复，不必点座位（完成时间 2026-09-01）。
- **STEAM-DESKTOP-17**：用户验收通过，占用座位（含机器人）不可点占、不踢该座 bot（完成时间 2026-09-01）。
- **BUG-24**：用户验收通过，登录即拉已保存形象写入 Overlay（完成时间 2026-09-01）。
- **BUG-25**：用户验收通过，打窝按钮只显示 `打窝n/50`（完成时间 2026-09-01）。
- **STEAM-DESKTOP-16**：Overlay 产品右键「窗口大小」三档视口裁切（960×560 / 800×400 / 600×300）；高度切顶、HUD 重锚，禁止整窗比例缩放（完成时间 2026-08-31）。
- **STEAM-DESKTOP-08A**：用户验收通过，世界地图选塘进 Overlay（完成时间 2026-08-17）。
- **STEAM-DESKTOP-08G**：用户验收通过，Overlay 钓位选择、开始钓鱼、收杆、领取鱼获可用（完成时间 2026-08-17）。
- **STEAM-DESKTOP-08F**：用户验收通过，好友申请接受/拒绝可点、列表刷新不离塘（完成时间 2026-08-17）。
- **STEAM-DESKTOP-08H**：用户验收通过。PanelSocial 双页社交布局、好友/私聊切换、
  独立列表行与 Grid item Prefab、响应式布局和统一按钮交互已完成（完成时间 2026-08-17）。
- **STEAM-DESKTOP-08A～08G**：后续功能已确认并登记计划表（设计时间 2026-08-17）。建议顺序 08F → 08G → 08A → 08B，再 08E / 08C / 08D。
- **STEAM-DESKTOP-07F**：用户验收通过，托盘挂机、咬钩通知、收鱼、断线快照恢复与 Development Build 全流程完成（完成时间 2026-08-17）。
- **STEAM-DESKTOP-07**：07A～07G 均已实现。
- **STEAM-DESKTOP-07G**：用户验收通过，独立 WPF Overlay + Named Pipe（完成时间 2026-08-17）。
- **STEAM-DESKTOP-07E**：用户确认主窗口功能页签、好友/聊天、背包、图鉴、设置及 Overlay 层级流程已完成验收（完成时间 2026-08-17；Prefab 为唯一功能页来源）。
- **STEAM-DESKTOP-07A / 07B**：计划状态改为已实现（完成时间 2026-08-14）。
- 07A～07E 工具与已修缺陷说明：`docs/planning/reports/STEAM-DESKTOP-07A-07E-工具与缺陷.md`。

### 新增

- **ADMIN-OBS-1.3**：Admin 玩家一览默认页 · Timeline/健康卡片�?· bot 默认停相位埋点（`METRICS_BOT_PHASE=1`）�?运营平台实时区只留内�?- **OPS-UX-1 运营平台入口体验**：今日运维条（T-1 KPI/告警/日批状态）· 每日必看 3 �?· Admin `?tab=&playerId=` 深链 · 工程区折�?- **v0.6.0 社交后端（FEAT-SOC-01/02/03�?*：动态点�?评论、排行榜（metrics 聚合，Asia/Shanghai）；`verify:social-v060`
- **运营日报 v1.1（R1–R3�?*：生态快�?· §2/4/5/6 · 留存 D1/D7 · `gold_earn` faucet · 规则版本 · Webhook 告警
- **运营日报 v1 MVP**：`scripts/analytics/date-utils.mjs`（Asia/Shanghai 日界）、`compute-daily-summary.mjs`、`generate-daily-ops-report.mjs`；`analytics:daily` 串联 aggregate �?summary �?compact �?report �?index
- **验收**：`npm run verify:daily-ops-report`；运维说�?`docs/ops/daily-analytics-cron.md`
- **DP-D 合规�?BI**：`playerPrivacy.ts` export/erase API · `export-warehouse.mjs` · `verify:data-platform-dp-d`
- **Admin 排障增强 v1.1**：`GET .../live-state` · Live Inspector 读内�?· `server_start/stop` · checkpoint 计时锚点 · admin-web 映射修复 · `verify:admin-observability`

### 修改

- **BUG-19**：每日额度单一口径重构 — `todayFishingBaseMs`/`sessionStartedAt`/`todayRemainingMs` · checkpoint 不前移展示锚点 · stop 先 finalize · 客户端禁反推 · `verify:bug19-quota`
- **BUG-18**：进塘首帧状态错误与演示降级 — 清空旧塘状态 · 连接代际门禁 · snapshotReady · 取消 10 秒静默演示降级 · 收杆立即幂等结算并返回上海日最终额度

- **BUG-16**：断�?离塘未结算丢时长 �?`settleFishingSession` · 断线�?finalize · 30s 分段落账 · `verify:fish-daily-shanghai`
- **BUG-15**：今日额度跨日不刷新 / 误显已满 �?闲置对齐 DB · 30s `syncHumanQuotaAndEmit` · `addTodayFishingMs` 封顶 · `verify:fish-daily-shanghai`
- **BUG-15 回归�?026-07-28�?*：`safeFishingElapsedMs`；sanitize 不再夹成 now�?h；坏锚点 flush 不写满日
- **OPS-CATCH-1.1**：Admin overview / 业务健康 / `daily_*` 产量对齐 `inventory`（含 bot）；聚合重跑近几日；`verify:ops-catch-inventory-admin`
- **FISH-BOT-2**：启动每�?3�? bot、已钓时长随机回拨；`tickSpawn` 按概率每次最�?+1，可慢补满塘；`verify:fish-bot-spawn-pace`
- **BUG-13**：垂钓中头顶�?秒」闪�?�?Bot/`pond_user_updated` 统一 `emitPondUserUpdated`；enrich �?`fishingStartedAt`；客户端合并秒表/锚点并以 `isFishingActive` 插值；`verify:session-timer-broadcast` 扩展守卫
- `compute-daily-summary.mjs` / `generate-daily-ops-report.mjs` / `daily-pipeline.mjs`：v1.1 全模�?- `verify-daily-ops-report-v1.ts`：R1–R3 断言

- `scripts/aggregate-daily-metrics.mjs`：上海时区日界、排�?`bot-%`
- `scripts/analytics/daily-pipeline.mjs`：全量重写，产出 `summary.json` + 样式�?`report.html`
- `scripts/analytics/build-index.mjs`：最新运营日报置顶卡片（日钓/DAU�?- `scripts/analytics/lib.mjs`：`live-daily` manifest 按日期优先排�?
## v0.5.0 (2026-07-11) �?三层数据体系 Phase 0

### 新增

- **Logger**：pino 统一结构化日志，支持级别策略（error/warn/info/debug）、日轮转（pino-roll）、Docker stdout 模式
- **错误日志 DB 持久�?*：`error_logs` 表替代内�?ring�?0d 保留，Admin API 支持 `?since=`、`?context=`、`?limit=`
- **健康检�?*：`/health`（uptimeSec + version）、`/ready`（db ping + metricsQueueDepth + memoryMb）；优雅停机时返�?503
- **事件字典 Schema**：`shared/metrics-schema.ts`�?3 条事件含 required/optionalFields；dev 环境自动校验
- **日聚合表**：`daily_player_stats` + `daily_pond_stats`；`scripts/aggregate-daily-metrics.mjs`
- **Metrics 归档**：`scripts/archive-metrics.mjs`�?0d 超期导出 jsonl 后删�?- **DB 备份**：`scripts/backup-db.mjs`，gzip 压缩，保留最�?7 份；`docs/ops/db-backup-restore.md`
- **线上日报流水�?*：`scripts/analytics/daily-pipeline.mjs`，产�?compact.json + report.html
- **验收脚本**：`scripts/verify-data-platform-phase0.ts`�?2 项断言全绿

### 修改

- `fishingObservability.ts`：`logStructuredEvent` 改调 pino logger
- `errorLog.ts`：重写为 DB 持久�?+ 50 条内�?ring 快速读
- `db.ts`：slow_query 改用 `logWarn`，注�?error_logs/daily_stats migration
- `createApp.ts`：`/ready` 端点，`isShuttingDown` 标志
- `index.ts`：优雅停机信号联�?`/ready`，logger 替换 console.log
- `admin.ts`：日志查询增强（since/context/limit），新增 `/api/admin/metrics/daily`
- `fishingMetrics.ts`：导�?`getPendingMetricsCount()`，dev 校验钩子
- `scripts/analytics/build-index.mjs`：增加线上日报区（`type: live-daily`�?- `.env.example`：新�?LOG_LEVEL/LOG_DIR/LOG_PRETTY �?7 个变�?
