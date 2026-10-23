## 2026-10-23 Unity 开页与右栏时序修复

- 修复正式预制体动效区域空引用；补存 12 页绑定，并统一烘焙与运行时映射，使标题、右栏实际按网页时序淡入。
- 编辑资料两栏同步揭开，取消旧详情栏延迟；资料内编辑／取消使用同页内容刷新，移除多余的跨页退出与横移；照片不再等待父级完全显示后重复入场，异步资料接续当前开页时钟。
- Chrome 7 个开页及编辑／取消 2 个按钮入口实测通过；Unity 专项 130/130，全套 521 通过、4 项既有失败；正式路由帧图与时序证据见 `docs/design/ui/2026-10-23-workspace-motion-unity.md`。

## 2026-10-23 新版工作区动效同步到 Unity

- 共享页面入口在纸页升起后统一揭开内容，移除重复入场及异步加载后的标题／侧栏闪回；同步排行榜、动态与收藏照片选择的 220ms 原地缩放。
- 工作区按钮、头像悬停和选择反馈按网页接入；分享、结算与鱼获选择器统一弹窗时序，遮罩保持独立；减少动效保留 120ms 短淡入。
- 专项回归 111/111；全套回归 498 通过／4 项既有文案与资源断言失败；专项复验及 30 张实际动效帧见 `docs/design/ui/2026-10-23-workspace-motion-unity.md`。

## 2026-10-23 手账网页预览打开动效修复

- 修复两个 Web 预览底栏打开时内容先露出、再隐藏贴入的闪回；保留原始纸页、活页环和卡片动效参数。
- 移除工作区启动及编辑入口的重复渲染，并等待工作区脚本就绪后启动。
- Chrome 导航回归 **22/22** 通过，包含逐帧透明度与脚本加载竞态检查；历史定位与复现方式见 `docs/design/ui/2026-10-23-journal-preview-navigation-fix.md`。

## 2026-10-23 资料与收获四页 Web 方案落地

- 正式 Unity 重写个人资料、编辑资料、分享鱼获与离塘结算；编辑取消／保存保持左卡底部，大头像、八展示位和搜索选择器接入真实数据。
- 分享保留选填附言和公开／好友范围；结算补真实咬钩、脱钩、有效时长、带图片及尺寸重量时间的鱼获记录、筛选排序与库存分享跳转。
- 新增 Unity 回归 **12/12**，全套 **476 通过／5 项旧检查失败**；结构守卫 **PASS**。服务端台账 **4/4**，全套 **49 通过／1 项既有失败**。保留未通过项与 Windows/Steam 待验收状态。
- 24 状态正式截图及六状态 Web/Unity 对照，见 `docs/art/cozy-journal/production/workspace.html`；实施和剩余检查见 `docs/design/ui/2026-10-23-cozy-journal-workspace-unity.md`。

## 2026-10-22 手账 UI 八类缺口修复

- 沿用已认可的正式 UI，补前三名渐变底图和名次标签；修正经验进度圆端、动态文字/入场时机、图鉴/展示柜/成就的插画底、标签、描述与红色印章。
- 好友、好友申请、私信、鱼塘聊天与消息通知分开呈现；保留真实申请/聊天接口，通知按本次登录收件，阅读与接受申请独立。
- 选塘补分类、水域、费用、准入与鱼种摘要；修正世界地图左栏重叠及入塘按钮图文居中。
- Unity EditMode **468/468**、预制体守卫 **PASS**；更新 21 个正式页面状态截图、15 组网页对照、11 对动效采样帧。Windows/Steam/DPI 待平台验收。详见 `docs/design/ui/2026-10-22-cozy-journal-detail-refinements.md`。

## 2026-10-22 沿用已认可的手账 UI 修复版

- 用户认可远端 `be6f2bdf` 的逐项对齐效果，正式入口继续使用 `DesktopShellUi` / `PanelRouter`，结束 JournalV2 完整替换推进。
- 修复自定义图形缺少 CanvasRenderer、页面/发布弹窗 CanvasGroup 初始化、加载后昵称与等级牌排布、鱼塘收费档位底板遮挡；保留已认可的几何与动效参数，正式资源目录无新增修改。
- 同步过时布局断言及测试夹具，Unity 编辑器全套 EditMode **448/448** 通过，包含预制体构建守卫。Windows/Steam 平台验收仍待完成。详见 `docs/design/ui/2026-10-22-cozy-journal-approved-baseline.md`。

## 2026-10-21 原版猫咪钓鱼手账正式 UI 替换

- 正式 uGUI 页面、共享 Library、主壳和辅助界面迁入 journal 主题；增加常驻世界地图入口，保留原生控件和真实业务，统一 1672×941 缩放。
- 地图/选塘共用照片、等级准入、收费档位、重试和入塘状态；修复同塘换档被提前返回、详情与操作区重叠、卡片选中装饰错误。
- 补真实道具库存、排行榜玩家卡、成就分类、个人中心五子页、动态右栏评论和本人分页；新增预制体发布弹窗与160字鱼获附言接口。
- 接入卡片贴入、账号隔离NEW、收获序列、操作印章、单循环调度、隐藏暂停与减少动效；修复旧预制体别名保存造成的嵌套引用丢失。
- Unity EditMode **347/347**、构建守卫 **PASS**、本人动态/发布 HTTP 测试 **8/8**。Windows构建、Steam联机及DPI仍待平台验收。正式截图与实施记录见 `docs/art/cozy-journal/production/` 和 `docs/design/ui/2026-10-21-cozy-journal-unity-implementation.md`。

## 2026-10-20 钓鱼场景与座椅美术更新

- 20 个世界地图鱼塘和独立新手塘接入 7 类共享背景，统一前景水面、低岸与后景陆地；保留原钓位 ID，校准座椅到岸线。
- 导入 12 件独立装饰和树桩、小板凳、青灰石台、折叠椅四种座椅；修正板凳承坐位置，以各素材承坐点对齐猫咪臀部，Unity Sprite、布局 Prefab 与 Overlay 导出资源引用同步。
- 折叠椅 V2 加高靠背并分开四个脚点，扩大显示尺寸、校准坐姿；独立模块尺寸与场景一致，近景预览包含完整椅脚。
- 所有鱼塘及新手塘的 420 个默认座位统一为木质小板凳；树桩、青灰石台、折叠椅完整保留为独立备用素材和 Prefab，同步保留导出资源。
- 修正猫咪与板凳在真实预制体中的错位：承坐点改用当前分层动画的 referenceFoot，统一共享角色与全部钓位；预览直接使用保存的猫咪贴图，并增加真实准备/钓鱼姿势检查，移除旧插画替换。
- 导出保留相对资源路径与逻辑场景尺寸，运行时优先读取布局绑定的共享背景，避免重复绘制背景。清理无引用旧背景与重复座椅贴图，保留有效回退图。

## 2026-10-14 桌面 UI 布局修复

- 修复 Unity 右上关闭/返回、雕刻图标二次染色、动作按钮换行及排行榜/背包/图鉴三栏布局；保留基础 Prefab + Variant。
- 同步 WPF Overlay 菜单独立槽位、原 PNG 雕刻/居中及聊天单行完整显示，运行时与导出配置一致。
- 28 项 Overlay 源回归和预制体静态物化验证通过；Unity/Windows 引擎验收未执行，需重新构建双方输出。

?# Changelog

## Unreleased

- **BUG-55（2026-10-19）**：资料编辑保存银渐层等新猫不再被过期 shared dist 拒绝。服务端 `dev`/`start` 在 `defaultAvatars.ts` 新于 dist 时先编译，听端口前断言九只现行猫；灰猫、暹罗、燕尾仍归一橘猫。已在跑的服务进程需要重启。
- **BUG-56（2026-10-19）**：Overlay 换阶段不再在点击帧解码整段分层图，预热使用英雄猫真实槽位并每帧只解一张。收杆回到坐下后再次收杆不再重播，直到下一次开始钓鱼。需要重新启动 Debug 桌面端后，P50 与 `frame_stall` 才能复测。

- **九宫格素材尺寸适配（2026-10-14）**：三态底板等比缩小为 64×63/border 10，大面板为 128×125/border 20，新增 64×63/border 10 小面板用于经验条底、等级牌与聊天输入区；统一主题 Sliced Image 的 PPU Multiplier 为 1，保持现有布局、Prefab/Variant 继承和素材 GUID。Unity、StreamingAssets 与 WPF 同步切图和导出边距；新增最小控件切片中心/边角检查。静态与源回归结果见 docs/design/ui/unified/nine-slice-sizing-validation.json；本机无 Unity/Windows，引擎编译、网格测试和实际视觉待验收。

- **CozyVisual Image 模式定点修复（2026-10-14）**：在 UI Prefab 管理中增加“仅修复 Image 的 Sliced 模式”，对实际导入的底板组件写入 m_Type、记录 Variant 覆盖并保存重载验证，不调整 border、PPU 或布局；构建校验明确禁止 CozyVisual 使用 Simple/Tiled/Filled，真正的进度条不受影响。补充三种错误模式和 Variant 保存重载测试；本机尚未执行 Unity Edit Mode，需在用户的 Unity 工程内执行模式修复与验收。

- **登录小窗口与九宫格链路修正（2026-10-14）**：LoginRoot 恢复居中 480×320 布局，不改登录后的 1672×941 主界面；带 border 的非 Filled 背景在主题应用时归一为 Sliced，新增 Sprite 导入约束与实际 border/FullRect 构建检查，保留已有非零 border、进度条 Filled 和图标等比显示；补充登录边界与九宫格网格 Edit Mode 回归测试。本机仅执行静态及源回归检查，Unity 编译与实际运行待验收。

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
