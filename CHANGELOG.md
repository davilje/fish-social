?# Changelog

## Unreleased

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

