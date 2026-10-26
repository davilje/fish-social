## 2026-10-26 — 22H 上钩浮漂与水花落点

- 上钩阶段复用原版浮漂，在当前鱼线与场景水面交点轻沉／回浮／摆动，循环衔接连续；水花改从同一入水点发射，替换旧水下鱼身中心定位。
- 全鱼塘、钓位、猫种与源帧／插值几何回归通过；Web预览同步当前咬钩水花及水下鱼隐藏规则，更新Windows随包程序。Windows实机验收仍待执行。见[修复报告](../../../../engineering/water-effects/2026-10-26-hooked-bobber-water-contact.md)。

## 2026-10-25 — STEAM-DESKTOP-22H 正式采用

- 用户确认“应用该方案”，采用v4简约水魔法造型＋v8鱼塘配色；WPF、Unity及默认预览同步。等待波纹×1.7、其他波纹×1.2，95%峰值及平滑淡出。
- 本机验证通过，重新发布当前源码的Windows验收包；状态回写“已实现”、完成日期2026-10-25，Windows性能／点击穿透及H/I实机验收继续待办。见[采用记录](../../../../engineering/water-effects/2026-10-25-water-effects-22h-adoption.md)。

## 2026-10-24 — STEAM-DESKTOP-22I 当前方案采用与清理

- 按用户要求采用 v5 配乐＋46 条已确认的 v3 音效／猫声，正式 47 条音频与采用哈希一致。
- 移除 169 个旧版音频（101,680,674 字节）、旧试听实现和无效生成工具；旧网址转至 v5，来源／许可清单与历史验证记录留档。
- 生成、安装及校验改用独立音效锁和采用记录，移除旧基线依赖；当前试听不再加载旧版资源。Windows 画声实测仍待执行。详见 `docs/engineering/audio/2026-10-24-audio-22i-adoption.md`。

## 2026-10-24 — STEAM-DESKTOP-22I BGM v5

- 按「旋律过于简单、吉他不清晰」反馈，只修订BGM：主题变奏、弱起／附点／连接乐句与有准备解决的延留；90 BPM、85.333秒循环，21.333秒主题试听。
- 主奏替换9条真实轻拨录音，伴奏保留12条柔和指弹；保留拨弦起音，无额外混响／高频搁架／压缩。来源hash、CC0与固定Gitblob已核验。
- 46条已确认音效、metadata及cue配置保持一致。谱面与PCM技术检查通过，BGM听感及Windows同步仍待验收；报告 `docs/engineering/audio/2026-10-24-audio-22i-v5.md`，试听 `docs/art/audio-22i/v5/`。

## 2026-10-24 — STEAM-DESKTOP-22I 音效确认与 BGM v4

- 用户确认其余音效通过应用，锁定46条v3非音乐资源及配置；验收原文与hash写入 `docs/engineering/audio/audio-22i-sfx-acceptance.json`。
- 仅BGM重写为88 BPM、G大调、32小节A/A′/B/A，明确八小节主题与固定伴奏；全局单旋律、和声边界止音，保留无钢琴方向。87.273秒完整循环及21.818秒主题试听已接入。
- 音效字节与metadata保持一致；独立谱面和PCM格式/循环/混音检查通过，BGM听感与Windows同步仍待验收。试听 `docs/art/audio-22i/v4/`，报告 `docs/archive/2026-10-26/superseded/engineering/2026-10-24-audio-22i-v4.md`。

## 2026-10-24 — STEAM-DESKTOP-22I v3 听感修订

## 2026-10-24 — 22H 动效与实际落点修正

- 水面波纹完整留在水线下，飞溅水滴／水冠单独裁剪并可在线上；咬钩／水下挣扎仅波纹。
- 抛竿／撒饵按真实触水时机及位置发射，落点固定；新增真实收杆触水，鱼实际出水按可见尺寸产生浪花。
- WPF／Unity及共享session同步；本机策略／Unity／Web验证通过，Windows实机及H/I采样仍待验收。详见[更新报告](../../../../engineering/water-effects/2026-10-24-water-effects-22h-v4.md)。

- 按用户反馈延长按钮动作和自然尾声，同 cue 增益有效事件 RMS 提升约 4.6–7.3dB；音乐重写为 86 BPM 无钢琴尼龙吉他双声部，89.302 秒循环，提供暖弦乐候选。
- 28 条水声和脱钩水声换用轻柔实录，去底噪与低频重击，取消强压缩；旧 v2 全资源冻结供直接对照，沿用既有音频事件接入。
- 47 资源格式/hash/容量/循环与 4× true-peak 混音检查通过；技术检查不等于听感或 Windows 实机验收。详见 `docs/archive/2026-10-26/superseded/engineering/2026-10-24-audio-22i-v3.md`。

## 2026-10-24 STEAM-DESKTOP-22H v4风格确认与正式资源

- 用户确认贴合猫咪细节量的v4简约水魔法风格，整理18张正式纹理并接入Overlay／Unity；低饱和色块、少量高光，各档额外水滴最多2颗。
- 三份资源hash一致、8px透明留边、解码0.53125MiB；213预览组合、1180水效断言、51源／资源保护及208Unity回归通过；Windows自包含测试包已更新。
- 仅记录H风格确认，不冒充Windows／H-I实机验收，完成日期仍留空。详见 `docs/engineering/water-effects/2026-10-24-water-effects-22h-v4.md`。

## 2026-10-24 STEAM-DESKTOP-22H 开发接入与本机验证


## 2026-10-24 — STEAM-DESKTOP-22I v2 工程实现

- 新增纸张／铅笔／印章操作音、28 条独立实录分档水声、保留授权猫叫；68 BPM 柔音采样钢琴循环 84.706 秒，另交付同旋律配器 A/B 和九场景试听，v1 冻结可比较。
- Unity 四短音＋一音乐受控播放，WPF 实际源帧 marker、共享 owner／session、稳定 catchId 透传、静默快照与事件去重、后台咬钩和独立音量接入。
- 资源／后端／浏览器／Unity／WPF 工程验证通过；Windows ≥30 事件画声延迟和耳机／扬声器听感未执行，待实机验收。计划只回写 22I 已实现，设计与完成日期 2026-10-24。
- 完整来源、hash、测量、测试边界：`docs/archive/2026-10-26/superseded/engineering/2026-10-24-audio-22i-v2.md`；试听 `docs/art/audio-22i/v2/index.html`。

- 交付猫咪同屏四档预览、18 张原创透明候选及版本／许可／hash，三处随包资源同步；213 个 Web 组合验证通过。
- 接入权威动作／阶段 ID、源帧发布信封、真实水面锚点、24 实例／8 组池、品质设置与 Unity 兜底；收获／脱钩／收线效果无绑定，无真实破水动作时 FX05 静默。
- 可移植行为测试、Unity 回归与 WPF 交叉编译详见工程报告；Windows 实机、H/I 画声及视觉验收待完成，保留 22H 完成日期为空。仅更新 H 进展，不更改 I/J 状态。

## 2026-10-24 22H／22I 方案 v2 用户修订

- 22H改为猫咪画风的六类水面效果，补S/M/L/XL四档、多种圆润水花／水纹；撤销收获确认、脱钩、收线视觉特效。
- 22I操作音优先纸张／翻页等手账物件，音乐柔和轻缓器乐；水花更清晰、自然、响亮并随鱼大小分档。旧试听是v1基线，需按新要求制作和评审。
- 同步规格、共用契约、交接与计划，未制作新素材或标记实现完成。

## 2026-10-24 STEAM-DESKTOP-22K／22L／22M 已实现

- 呼吸、30 分钟权威瞌睡、触碰唤醒与睡眠直接逃鱼已接入 Unity / Overlay / 服务器。
- 狸花猫「丧彪」完整钓鱼动作和头像已接入；家园只覆盖站立／坐姿，其他家园动作不计入本次完成范围。
- Unity 203/203、服务器专项 35/35、运动 24/24、浏览器 616 次采样通过；Windows 联机和美术由用户验收，未标记已验收。
- [规格](../../../../planning/specs/desktop/Steam桌面端-22KLM呼吸瞌睡与狸花猫.md) · [复验交接](../../../../planning/prompts/desktop/steam-desktop-22klm-dev.prompt.md)。

## 2026-10-24 STEAM-DESKTOP-22H／22I／22J 方案设计完善

- 22H：9 类钓鱼特效，明确水面锚点、真实事件、资源规格、多人预算、简化与生命周期。
- 22I：沿用六场景试听，补齐结果去重、真实画面同步、后台咬钩、声部与音乐压低；更新 catchId/resultId 当前基线说明。
- 22J：沿用原版手账外框，明确筛选／地图／详情与入塘操作分区、标记避让、真实数据、费用确认与失败恢复。
- 新增统一评审入口、H/I 共用事件契约与开发交接；状态保持已确认，正式实现和验收未完成。入口：`docs/design/ui/steam-desktop-22hij/README.md`。

# 策划文档变更记录

### [git-sync-and-new-specs] - 2026-10-26

- 对照 10-22 之后的 git：`STEAM-DESKTOP-22G` Overlay 粉彩场景、登录场景和时长提示已入库 → **已实现**（设计 2026-10-24，完成 2026-10-26）。22H/22I 维持已实现。22J 仍未实现。均未标已验收。
- **WORLD-CAT-01** 猫咪世界观与场景描述 → **已确认**。地图和塘介绍不再用中国实景叙事，`pondId` 与鱼种表保留。
- **FISH-ATTR-01** 体长体重步进与公式评判 → **已确认**。体长步进改为 0.05 米，体重展示 0.1 公斤。40 米 / 7 日成长和 40 米脱钩 98.5% 判定为不适合当前尺度，本票不改这些系数。
- **OPS-HOT-1** 桌面资源与数值热更 → **已确认**。在 OPS-RELEASE-1 之外准备数值包和 Overlay 资源包，不热更 exe。

### [bug-46-confirmed] - 2026-10-22

- **BUG-46** 成就列表当前进度 → **已确认**，P1，hotfix。`progressValue` 已在解锁时计算，但 `buildAchievementViews` 不下发。profile-hub 增加 `currentValue`；自己的未完成卡片显示当前/目标。解锁规则、隐藏规则和他人可见范围不变。
- 规格：`docs/planning/specs/bugs/BUG-46成就列表当前进度.md`
- 开发提示词：`docs/planning/prompts/bugs/bug-46-achievement-progress-dev.prompt.md`

### [steam-desktop-20-bug-57-accepted] - 2026-10-22

- **STEAM-DESKTOP-20** 主窗口固定分辨率 → **已验收**（设计 2026-10-21，完成 2026-10-22）。用户验收通过。
- **BUG-57** 桌面设置、世界/鱼塘页、首页形象与装备图 → **已验收**（设计 2026-10-21，完成 2026-10-22）。用户验收通过。

### [bug-59-implemented] - 2026-10-22

- **BUG-59** 手账翻页、标签、右侧详情、排行榜与动态 → **已实现**（设计 2026-10-22，完成 2026-10-22）。用户验收通过。页签再次打开保留已建列表并重播入场与退场；排行榜前三名跟随当前榜。每周钓王仍按这一条的体长换算重量。
- 规格：`docs/planning/specs/bugs/BUG修复-手账翻页标签与排行榜.md`

### [steam-desktop-21-confirmed] - 2026-10-21

- **STEAM-DESKTOP-21** 猫咪钓鱼手账动效 → **已确认**，P1，hotfix。按已确认的手账方案接入按钮回弹、导航浮漂、切页、金币滚动、地图浮标、角落小猫和「减少动效」。不改 Overlay。
- 规格：`docs/planning/specs/desktop/Steam桌面端-21猫咪钓鱼手账动效.md`
- 开发提示词：`docs/planning/prompts/desktop/steam-desktop-21-journal-motion-dev.prompt.md`
- 设计：`docs/design/ui/journal/2026-10-21-cozy-journal-motion.md`

### [bug-58-implemented] - 2026-10-21

- **BUG-58** 桌面鱼塘与设置信息 → **已实现**（设计 2026-10-21，完成 2026-10-21）。实机确认后补了两处：进入鱼塘一次点击生效；鱼塘列表滚动区域覆盖整列，不再只有按住按钮才能滑动。

### [bug-58-confirmed] - 2026-10-21

- **BUG-58** 桌面鱼塘与设置信息 → **已确认**，P1，hotfix。鱼塘卡片与详情改为中文并支持右栏进入；地图标记缩小并按类型着色；设置标出当前分辨率、切换即保存，连接测试不再显示 JSON。
- 规格：`docs/planning/specs/bugs/BUG修复-桌面鱼塘与设置信息.md`
- 开发提示词：`docs/planning/prompts/bugs/bug-58-pond-settings-info-dev.prompt.md`

### [bug-57-confirmed] - 2026-10-21

- **BUG-57** 桌面设置、世界/鱼塘页、首页形象与装备图 → **已确认**，P1，hotfix。设置字段改为互不重叠的单列；探索世界和选择鱼塘外框对齐商店（1370×96 页眉，正文高 496）；首页 HeroCat 不再退回资料头像；鱼饵和钓竿按 id 取同一张图。
- 规格：`docs/planning/specs/bugs/BUG修复-桌面设置世界首页与装备图.md`
- 开发提示词：`docs/planning/prompts/bugs/bug-57-desktop-ui-layout-dev.prompt.md`

### [steam-desktop-20-confirmed] - 2026-10-21

- **STEAM-DESKTOP-20** 主窗口固定分辨率 → **已确认**，P0，hotfix。登录和主壳默认客户区 1920×1080；设置只提供 1280×720、1600×900、1920×1080、2560×1440。Overlay 画布保持 960×560。代码已按规格落地，待验收后再改为已实现。
- 规格：`docs/planning/specs/desktop/Steam桌面端-20主窗口固定分辨率.md`
- 开发提示词：`docs/planning/prompts/desktop/steam-desktop-20-window-presets-dev.prompt.md`

### [status-2026-10-21] - 2026-10-21

- **FEAT-06** 完整状态机需求描述 → **已废弃**（设计 2026-07-04，关闭 2026-10-21）。
- **FEAT-05** C 期调优与状态机 → **已废弃**（设计 2026-07-12，关闭 2026-10-21）。
- **UNITY-P4** 壳层功能迁入 → **已废弃**（设计 2026-07-26，关闭 2026-10-21）。Unity 阶梯里这一段不再计入未完成。
- **FEAT-SPOT-05** 钓位鱼情联动线索 → **已实现**（设计 2026-08-25，完成 2026-10-21）。
- **FEAT-SPOT-03** 钓位标签地形微调 → **已实现**（设计 2026-08-25，完成 2026-10-21）。用户说的「已完成」记入计划表完成态「已实现」。
- **STEAM-DESKTOP-ART-01** 桌面宠物与鱼塘视觉资源替换 → **已实现**（设计 2026-08-13，完成 2026-10-21）。
- **STEAM-DESKTOP-ART-04** 橘猫钓鱼动画 Overlay 七层叠加 → **已实现**（设计 2026-10-11，完成 2026-10-21）。
- **STEAM-DESKTOP-08I** 保持 **已实现**、P0（设计/完成 2026-08-18），类型改为 **Bug修复**，归入 Bug 修复进度。
- **BUG-27～35** 计划表已是 **已验收**（2026-10-16），本次未改。
- **BUG-46** 成就列表没有当前进度：类型改为 **功能**，状态 **待开发**。

### [bug-55-56-implemented] - 2026-10-19

- **BUG-55** 默认头像目录与运行白名单不一致 → **已实现**（设计 2026-10-19，完成 2026-10-19）。`npm run dev/start --workspace=server` 在 dist 旧于 `defaultAvatars.ts` 时先编译；听端口前断言九只现行猫。退役灰猫/暹罗/燕尾归一橘猫。vitest `defaultAvatarCatalog` 5 项通过。已在跑的 tsx 进程需要重启才会换名单。
- **BUG-56** Overlay 阶段切换卡帧与收杆重播 → **已实现**（设计 2026-10-19，完成 2026-10-19）。预热按英雄猫真实槽位、每帧一张图；播放只取缓存。收杆锁在 `Clear` 之后仍忽略第二次收杆，日志 `stop_fishing_ignored`。P50 与 `frame_stall` 尚未用新 Debug 包复测。

### [bug-55-56-confirmed] - 2026-10-19

- **BUG-55** 默认头像目录与运行白名单不一致 → **已确认**，P0，hotfix。服务端加载的 shared/dist/defaultAvatars.js（2026-10-17，旧 6 只）拒绝源码里的银渐层等五只新猫。
- **BUG-56** Overlay 阶段切换卡帧与收杆重播 → **已确认**，P0，hotfix。管道 ipc 已是数毫秒；frame_stall 2.3～5.8 秒。收杆 commandId 6 到 seated 后，commandId 7 又播 stopping。
- 规格：docs/planning/specs/bugs/BUG修复-默认头像目录与运行白名单不一致.md、docs/planning/specs/bugs/BUG修复-Overlay阶段切换卡帧与收杆重播.md

### [bug-54-own-fishing-presenter-implemented] - 2026-10-18

- **BUG-54** → **已实现**（待重打包实机验收 P50/P95）。
- Unity 新增 `OwnFishingPresenter`：take/start/stop/groundbait/leave/bite 立刻推 Overlay；本地 bait 800→cast 600→waiting；stop 250ms 后 seated；ack 失败回滚；`FISH_SOCIAL_OWN_FISHING_PRESENTER=0` 可关。
- Overlay 去掉猜 phase 时间线，只保留落座一帧预测 + 连点禁用；按钮跟 Unity `availableActions`/phase。

### [bug-54-own-fishing-presenter-confirmed] - 2026-10-18

- **BUG-54** Unity 自己钓鱼表现权威 → **已确认**，P0，hotfix。
- 终态方案：自己的钓鱼由 Unity 本地推进（phase / visual / availableActions）；服务端只校验与结算；Overlay 只渲染与转发，退役 BUG-53 猜阶段时间线。
- BUG-53 保留为已实现的过渡 hotfix（socket 线程池等基础设施继续保留）。
- 规格：`docs/planning/specs/bugs/BUG修复-Unity自己钓鱼表现权威.md`
- 开发提示词：`docs/planning/prompts/bugs/bug-54-own-fishing-presenter-dev.prompt.md`

### [bug-53-overlay-action-feedback] - 2026-10-16

- **BUG-53** Overlay 开始钓鱼、收杆、打窝、离席、Debug 强制咬钩、咬钩到期的动画反馈慢 → **已实现**（待打包后运行验收）。
- 根因：Unity socket 读循环从主线程启动，每次收包都要等下一帧，服务器推送一多回执就排队 1.8～8 秒（聊天约 0.3 秒）。
- Unity：socket 收发移到线程池，发送走单队列保序；自己的状态、宠物动画、咬钩、脱钩事件绕过 15Hz 限流立即推送；新增 `frame_stall` 卡帧日志。
- Overlay：点击后立即播放预期动画（抛竿 / 收线 / 打窝 / 起身 / 咬钩），服务端状态一变化即以服务端为准，5 秒未确认或回执失败则回退；咬钩倒计时到点本地切收线。

### [bug-53-overlay-action-feedback-2] - 2026-10-18

- **BUG-53** 补强：上钩 / 自然咬钩路径与点击动画一并压延迟。
- Unity：`fish_bite` / `fish_miss` / `pond_user_updated` 进优先队列；收到 `fish_bite` 立即本地切 `hooked`（含倒计时），不必等下一条 `pond_user_updated`。
- Overlay：自身钓鱼阶段 / 视觉 / 钓位变化用 `Input` 优先级刷状态（不再挤在 Background）；落座后预热抛竿/收线/咬钩等首帧；连点开始→收杆也能跟预测相位切换。

### [bug-53-overlay-action-feedback-3] - 2026-10-18

- **BUG-53** 补强：落座后按钮、连点、装饵→钓鱼、收杆后按钮。
- 落座预测立刻显示开始钓鱼 / 打窝 / 离席（不再等 AvailableActions）。
- 同一动作进行中禁止连点重播；收杆可打断进行中的开始钓鱼。
- 本地按服务端节奏推进 装饵 800ms → 抛竿 600ms → 钓鱼中；收杆约 250ms 后本地回到入座并立刻亮按钮（收线动画可继续播完）。

### [overlay-hud-bugs-implemented] - 2026-10-16

- **BUG-48～52** → **已实现**（待打包后运行验收）。
- BUG-48：Unity 导出按 `CatTheme` 写 12/22/10 九宫格边距，文字色统一墨色；回退原因常驻状态区并写日志。
- BUG-49：删除菜单栏与「打开主界面」（右键菜单新增此项）；状态卡 + 退出鱼塘右下，Debug 88 宽，钓鱼按钮底部居中，聊天栏左下 320 宽；Unity prefab 已迁移并重导出，XAML 回退布局同步。
- BUG-50：聊天展开 10 行；折叠按钮展开时与发送按钮同行；发送按 `commandAcks` 回执清空输入框（旧客户端保留文本匹配兜底）。
- BUG-51：Debug 弹窗内新增结果行；所有 Debug 路径回执成功 / 失败原因；Unity Debug 忙碌 15 秒后自动解锁。
- BUG-52：Overlay 合并 state 并以 Background 优先级处理；日志后台缓冲、每秒采样、10MB 轮转；Unity state 限频 15Hz，回执即时发布；点钓位立即本地落座（3 秒内未确认或失败则回退）。

### [overlay-hud-bugs-registered] - 2026-10-16

- **BUG-48** HUD 导出写回旧九宫格边距（10/20），Overlay 要求 12/22，加载失败退回旧 XAML 布局 → **已确认**，P0。
- **BUG-49** HUD 布局重排：去掉菜单栏与打开主界面，只留右键菜单；状态卡、退出鱼塘右下；钓鱼按钮底部居中；聊天栏左下 320 宽；Debug 只改宽度 → **已确认**，依赖 BUG-48。
- **BUG-50** 聊天栏 10 行、折叠按钮对齐、发送后不清空 → **已确认**。
- **BUG-51** Debug 菜单点击无反应 → **已确认**，Unity 与服务端环节待验证。
- **BUG-52** 操作延迟与卡顿：state 34 条/秒、UI 线程同步写 335MB 日志 → **已确认**，P0。
- 规格：`docs/planning/specs/bugs/BUG修复-OverlayHUD布局与交互.md`

### [bug-47-showcase-detail-rows] - 2026-10-16

- **BUG-47** 展示柜点选详情 → **已实现**。`ShowFishDetail` 改为图标、标签、数值分行；空格子和未选中仍用原来的提示句。
- 预览 `PanelProfileHub-展示柜` 已核对分行。未改成就 DTO，**BUG-46** 仍是已确认。
- BUG-27～35 保持 **已验收**。

### [cozy-desktop-ui-bugs-registered] - 2026-10-16

- **BUG-27～35** Cozy 桌面页布局与字体截字 → 用户已回复「已验收通过」，计划表 **已验收**。
- **BUG-36～45** 白点、鱼获页签变灰、鱼塘选择警告、旧鱼图标、第二次 Apply、12 个 EditMode 失败、选中态与详情行 → **已实现**。EditMode 219/219。尚未做 Development Build 真实账号验收。
- **BUG-46** 成就没有当前进度、**BUG-47** 展示柜点选详情仍是整段文字 → **已确认**，未做。成就条不改 `server/`、`shared/`。
- 规格：`docs/planning/specs/bugs/BUG修复-Cozy桌面页还原.md`

### [steam-desktop-art-04-confirmed] - 2026-10-11

- **STEAM-DESKTOP-ART-04** 橘猫钓鱼动画接入 Overlay → **已确认**。
- Overlay 按工作台 7 层同帧叠加，逐帧使用归档 `layerOrder`；猫爪与钓具分目录。
- 不改服务端钓鱼阶段；Named Pipe 仍只传 `petVisualState`（新增 `bait`）。主窗口不重做七层精灵。

### [steam-desktop-19-accepted] - 2026-09-02

- **STEAM-DESKTOP-19** Overlay 咬钩结算弹窗 → 用户验收通过（设计 2026-09-01，完成 2026-09-02）。
- 计划表与看板已回写 **已实现**。

### [steam-desktop-19-hook-settle-prompt] - 2026-09-01

- **STEAM-DESKTOP-19** Overlay 咬钩结算弹窗 → **已确认**。对齐 Web 钓到/脱钩弹窗；取消「领取鱼获」；出售档与不可回鱼只显示尺寸体重并入包；回鱼档达标显示尺寸、体重、回鱼价格。

### [bug-26-hover-after-menu-accepted] - 2026-09-01

- **BUG-26** Overlay 右键后悬停失效 → 用户验收通过（回归修复）。
- 根因：透明 Overlay 上 `ContextMenu.IsOpen`/`Closed` 会卡住；用「指针不在菜单上」自动关菜单会闪关。
- 落地：`MenuSuppressesHover` 自有开关；菜单 `StaysOpen`；右键抬起打开；左键 Overlay 关闭；`GetCursorPos` 恢复悬停。
- 规格补了 §2.2 / §7 修复过程。计划表仍为 **已实现**（设计/完成 2026-09-01）。

### [steam-desktop-17-bug-24-25-accepted] - 2026-09-01

- **STEAM-DESKTOP-17** 占用座位不可顶机器人、**BUG-24** Overlay 形象冷启动恢复、**BUG-25** 打窝按钮剩余口数 → 用户验收通过（设计/完成均 2026-09-01）。
- 计划表与看板已回写 **已实现**。

### [steam-desktop-15-16-accepted] - 2026-08-31

- **STEAM-DESKTOP-15** 钓鱼栏按钮时序、**STEAM-DESKTOP-16** 窗口视口缩放 → 用户验收通过（设计/完成均 2026-08-31）。
- 计划表与看板已回写 **已实现**。

### [bug-26-18-14e-accepted] - 2026-09-01

- **BUG-26** Overlay 悬停与右键菜单指针命中、**STEAM-DESKTOP-18** 悬停条数与 actor-hint、**STEAM-DESKTOP-14E** 头顶状态图标与进度环 → 用户验收通过（设计/完成均 2026-09-01）。
- 计划表与看板已回写 **已实现**。

### [overlay-unregistered-hotfix] - 2026-09-01

- **BUG-26** Overlay 悬停浮窗与右键菜单 → **已确认**。同一条猫身命中/捕获链：自己可悬停、他人 09A、关菜单后悬停恢复。
- **STEAM-DESKTOP-18** 悬停浮窗条数与 actor-hint 定位 → **已确认**。两行：时长 + `钓到N条!`/`空军`。
- **STEAM-DESKTOP-14E** 头顶状态图标与进度环 → **已确认**。等鱼无图标；环改 actor-ring + ring-bg（修订 14D 套猫）。
- 橘猫序列帧 / status 换图不单独立号。

### [steam-desktop-17-bug-24-25] - 2026-09-01

- **STEAM-DESKTOP-17** 占用座位不可顶机器人 → **已实现**（设计/完成 2026-09-01）。`take_spot` 占用即失败；Overlay 占用座不点占。
- **BUG-24** Overlay 形象进游戏不恢复 → **已实现**。登录预拉 profile；改资料回写塘内 avatar。
- **BUG-25** 打窝按钮剩余口数 → **已实现**。按钮只 `打窝n/50`。

### [steam-desktop-16-overlay-viewport] - 2026-08-31

- **STEAM-DESKTOP-16** Overlay 窗口视口缩放 → **已实现**（设计/完成 2026-08-31）。
- 产品右键「窗口大小」三档：960×560 / 800×400 / 600×300。改显示范围，禁止整窗比例缩放。
- Overlay：底边钉死切顶、左右内收、HUD Relayout、14B 按当前 viewW 钳制、13A `ApplySize`、P1 `%LocalAppData%/FishSocial/overlay-viewport.json`。
- 规格：`docs/planning/specs/desktop/Steam桌面端-16Overlay窗口视口缩放.md`

### [steam-desktop-15-fishing-dock-timing] - 2026-08-31

- **STEAM-DESKTOP-15** Overlay 中央钓鱼栏按钮时序 → **已实现**（设计/完成 2026-08-31）。
- Overlay：有座时主按钮不 `Collapsed`；打窝/离席只在 `seated`/`groundbaiting` 出现；去掉 `stack>0` 保活打窝钮；点击只禁用不提前改显隐。
- Unity：`availableActions` 按相位表；`stopping`/`resolving` 不发离席/打窝/开钓。
- 服务端：`leave_spot` 放行 `groundbaiting`，取消未完成 cast、不叠层、离席清窝。
- 规格：`docs/planning/specs/desktop/Steam桌面端-15Overlay钓鱼栏按钮时序.md`

### [steam-desktop-14abcd-accepted] - 2026-08-30

- **STEAM-DESKTOP-14A** 座位预制体与空位显隐 → **已实现**（设计 2026-08-29 / 完成 2026-08-30）。
- **STEAM-DESKTOP-14B** 横轴场景平移 → **已实现**（设计 2026-08-29 / 完成 2026-08-30）。
- **STEAM-DESKTOP-14C** HUD 正式素材与文字对齐 → **已实现**（设计 2026-08-29 / 完成 2026-08-30）。
- **STEAM-DESKTOP-14D** 状态小图标与上钩圆环 → **已实现**（设计 2026-08-29 / 完成 2026-08-30）。
- **14** 此前已验收（设计/完成 2026-08-29）。14～14D 全部关闭。
- 规格：`docs/planning/specs/Steam桌面端-14*.md`

### [steam-desktop-14-click-through-accepted] - 2026-08-29

- **STEAM-DESKTOP-14** Overlay 透明点击穿透回归 → **已实现**（设计/完成 2026-08-29）。
- 去掉 `SceneFadeHost` `#01000000` 整层填充；`WM_NCHITTEST` 按猫/HUD/可见塘图像素命中，透明处 `HTTRANSPARENT`。
- 13A 四边 40px Absolute 渐隐保留。规格：`Steam桌面端-14Overlay透明点击穿透回归.md`。

### [steam-desktop-14-overlay-ux] - 2026-08-29

- 立项 **STEAM-DESKTOP-14～14D** → **已确认**（设计 2026-08-29）。
- **14** 透明点击穿透回归（保留 13A 渐隐）；**14A** 座位预制体统一与空位显隐（修订：OverlayPondActor 为真相）；**14B** 横轴平移（P1）；**14C** HUD 正式图与文字样式；**14D** 状态小图标与套猫圆环。
- 建议开发顺序：14 → 14D → 14A → 14C；14B 等加宽底图。
- 规格：`docs/planning/specs/Steam桌面端-14*.md`

### [steam-desktop-13-overlay-hud-accepted] - 2026-08-29

- 登记并验收 **STEAM-DESKTOP-13 / 13A / 13B / 13C**（设计/完成均为 2026-08-29）。
- 13：聊天栏一行向上展开、Prefab 像素对齐、嵌套 HUD 导出修复。
- 13A：场景层四边 40px 半透明渐隐（Absolute mapping，底部有效）。
- 13B：宠物名/状态徽章、右键他人菜单、登录窗 scale=0。
- 13C：打窝按钮仅次数；成功文案 `txt_error`；`txt_error` 在钓鱼条上方。
- 规格：`Steam桌面端-13OverlayHUD聊天栏与预制体对齐.md` 等四份；看板：根目录 `策划进度看板.html`。

### [overlay-pet-size-hover-docs] - 2026-08-29

- Overlay 猫咪口径对齐：**显示 64×64**、序列帧源图 **256×256**（不再写 128 显示槽）。
- 悬停热区改为仅猫身 64×64；时长浮窗约 80×28，水平居中对齐猫身。
- 同步 spec / Overlay README / pets README / 09B·09D。

### [steam-desktop-art-02-03-accepted] - 2026-08-29

- **STEAM-DESKTOP-ART-02** Overlay 场景布局管线 → **已实现**（设计 2026-08-16 / 完成 2026-08-29）。
- **STEAM-DESKTOP-ART-03** 分塘底图、猫咪序列帧与 HUD 同步 → **已实现**（设计 2026-08-28 / 完成 2026-08-29）。
- 用户验证通过：每塘 Prefab 导出钓位像素表；分塘底图、六姿势序列帧、HUD Prefab 与 Overlay 一一对应。
- 规格：[Steam桌面Overlay场景布局管线.md](../../../../planning/specs/desktop/Steam桌面Overlay场景布局管线.md)、[Steam桌面Overlay分塘底图与HUD同步.md](../../../../planning/specs/desktop/Steam桌面Overlay分塘底图与HUD同步.md)

### [steam-desktop-art-03-pond-pet-hud] - 2026-08-28

- 立项 **STEAM-DESKTOP-ART-03** 分塘底图、猫咪姿势序列帧、Unity HUD 与 Overlay 一一对应 → **已确认**（P0，设计 2026-08-28）。
- 与 ART-02 边界：ART-02 只管塘内场景（背景装饰 + 钓位像素）；ART-03 管每塘独立底图、六姿势动画夹、HUD Prefab 导出同步。
- 规格：[Steam桌面Overlay分塘底图与HUD同步.md](../../../../planning/specs/desktop/Steam桌面Overlay分塘底图与HUD同步.md)
- 开发 prompt：[steam-desktop-art-03-pond-pet-hud-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-art-03-pond-pet-hud-dev.prompt.md)

### [feat-pool-02-ecology-tuning-accepted] - 2026-08-27

- **FEAT-POOL-02** 塘人口与品质权重调参 → **已实现**（设计/完成 2026-08-27）。
- 定稿：新手塘 `1/1/1` 钓完不补；公开塘上限/初值 **100**；补鱼/迁徙基准 **60min**；老手 `30/20/20/15/10/3/2`；野外·水库 `45/10/10/10/15/5/5`；禁止 `15/15/20/30/10/7/3`；品质表含中文类型名。
- 验证：`game-data:build` / `export` + `reset-pond-ecology --apply` 重种分布通过。
- 规格：[塘人口与品质权重调参.md](../../../../planning/specs/gameplay/塘人口与品质权重调参.md)
- prompt（结案归档）：[feat-pool-02-ecology-tuning-dev.prompt.md](../../../../planning/prompts/gameplay/feat-pool-02-ecology-tuning-dev.prompt.md)

### [feat-return-05-weight-tiers-accepted] - 2026-08-27

- **FEAT-RETURN-05** 回鱼体重分档 → **已实现**（设计/完成 2026-08-27）。
- 定稿：准入改为市斤体重 **>10 斤**（废弃紫品 / 体长比 / 满尺寸拦截）；回鱼金分档 **>100 斤 ×3**、**>10 斤 ×1.5**。
- 自检：`npm run verify:feat-return-01`、`npm run verify:feat-return-02`
- 规格：[回鱼体重分档.md](../../../../planning/specs/gameplay/回鱼体重分档.md)
- supersede 说明：RETURN-03 准入与统一 1.5× 倍率；见 RETURN-01/03 变更记录

### [steam-desktop-12b-debug-fish-inspect] - 2026-08-26

- **STEAM-DESKTOP-12B** Overlay Debug 鱼塘/钓位查看与强制上钩 → **已实现**。
- Overlay Debug 改为 3 列、宽度 ×1.3（468）；塘内/钓位鱼列表与属性；钓位数据；waiting 强制上钩。
- 规格：[Steam桌面端-Debug鱼塘钓位查看.md](../../../../planning/specs/desktop/Steam桌面端-Debug鱼塘钓位查看.md)
- 开发 prompt：[steam-desktop-12b-debug-fish-inspect-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-12b-debug-fish-inspect-dev.prompt.md)

### [feat-return-04-ux-accepted] - 2026-08-25

- **FEAT-RETURN-04** 自动回鱼体验闭环（RETURN-02 补强）→ **已实现**（设计/完成 2026-08-25）。
- 交付：回鱼档钓到即结算自动回鱼；`fish_catch_settled` 即时反馈；离塘 `pond_session_summary` + `PanelPondSettlement`；Overlay Debug 顶层弹窗；澄清 Debug 发鱼不自动回塘。
- 自检：`npm run verify:feat-return-02`
- 规格：[自动回鱼体验闭环.md](../../../../planning/specs/gameplay/自动回鱼体验闭环.md)
- 关联修订：[双价塘与自动回鱼.md](../../../../planning/specs/gameplay/双价塘与自动回鱼.md)

### [v0.7-r3-pool-fish-cn-accepted] - 2026-08-25

- **FEAT-POOL-01** 塘生态数值表驱动 → **已实现**（设计 2026-08-24 / 完成 2026-08-25）。
- **FEAT-FISH-CN-01** 中国鱼种与区域分布 → **已实现**（设计 2026-08-24 / 完成 2026-08-25）。
- 交付：50 种本土鱼库；20 塘真实钓场名；`pond_fish_pool` 表驱动出鱼；品质/咬钩/脱钩基数入表；废弃 `STOCK_TEMPLATES`。
- 自检：`npm run verify:feat-pool-01`、`npm run verify:fish-cn-01`
- 规格：[塘生态数值表驱动.md](../../../../planning/specs/gameplay/塘生态数值表驱动.md)、[中国鱼种与区域分布.md](../../../../planning/specs/gameplay/中国鱼种与区域分布.md)

### [feat-spot-03/04/05 follow-up] - 2026-08-25

- 立项 **FEAT-SPOT-03** 钓位标签地形微调 → **已确认**（P2，xlsx `pond_spot_tags` 420 点人工审校）
- 立项 **FEAT-SPOT-04** 线索文案策划审校 → **已确认**（P2，`spot_clue_texts` 扩写 + §4.2 禁区）
- 立项 **FEAT-SPOT-05** 钓位鱼情联动线索 → **已确认**（P1，生态 v2 + `activitySignal` 实时 tier）
- 规格：[钓位标签地形微调.md](../../../../planning/specs/gameplay/钓位标签地形微调.md)、[线索文案策划审校.md](../../../../planning/specs/gameplay/线索文案策划审校.md)、[钓位鱼情联动线索.md](../../../../planning/specs/gameplay/钓位鱼情联动线索.md)

### [feat-spot-02-tag-clue-library] - 2026-08-25

- 立项 **FEAT-SPOT-02** 钓位标签与线索库 v2 → **已实现**（R3 P1）。
- 22 类标签、`pond_spot_tags` 420 行、154 条合规线索、`activitySignal` 预留；坐席按标签过滤；体长 XP 指数 0.85。
- 规格：[钓位标签与线索库-v2.md](../../../../planning/specs/gameplay/钓位标签与线索库-v2.md)
- 开发 prompt：[feat-spot-02-tag-clue-library-dev.prompt.md](../../../../planning/prompts/gameplay/feat-spot-02-tag-clue-library-dev.prompt.md)

### [feat-spot-01 drop spot_tags] - 2026-08-25

- **FEAT-SPOT-01**：剔除 `spot_tags`；坐席线索改为全库加权随机（可保留等级/`pondCategory` 过滤）；`spotTag` 列废弃忽略。
- 规格：[钓位点位线索文字泡.md](../../../../planning/specs/gameplay/钓位点位线索文字泡.md)

### [feat-fish-cn-01 / feat-pool-01 定稿修订] - 2026-08-25

- **FEAT-FISH-CN-01 / FEAT-POOL-01** 正文与开发 prompt 对齐实现定稿（状态仍为 **已确认**，待验收）：
  - 废弃 `fish_species_habitat`；出鱼权威仅为 `pond_fish_pool`（每塘×种）。
  - 种池**不含**品质列；品质 = `pond_category_quality_weights` ∩ 种 `qualityMin`/`qualityMax`（下限恒 1，稀有度抬上限）。
  - `typical*` 仅图鉴；体重 `W=a×L^b` 展示不落库。
  - **`pond_ecology` 并入 `ponds`**（`maxPopulation` / `minPopulation` / `initialPopulation`）。
  - **`fish_sell` 并入 `fish_quality_stats`**；卖价公式去掉钓组 `SPECIES_MULT`（钓组仅竿适配）。
- 规格：[中国鱼种与区域分布.md](../../../../planning/specs/gameplay/中国鱼种与区域分布.md)、[塘生态数值表驱动.md](../../../../planning/specs/gameplay/塘生态数值表驱动.md)
- prompt：[feat-fish-cn-01-species-regions-dev.prompt.md](../../../../planning/prompts/gameplay/feat-fish-cn-01-species-regions-dev.prompt.md)、[feat-pool-01-ecology-table-dev.prompt.md](../../../../planning/prompts/gameplay/feat-pool-01-ecology-table-dev.prompt.md)

### [feat-fish-cn-01-species-regions] - 2026-08-24

- 立项 **FEAT-FISH-CN-01** 中国鱼种与区域分布 → **已确认**（R3 P0）。
- 用户确认：塘名改为真实钓场（pondId 不变）；鱼种扩充约 50 种并**全面替换**外来主 ID。
- 附录名录：[中国鱼种名录-v1.md](../../../../planning/specs/appendix/中国鱼种名录-v1.md)
- 规格：[中国鱼种与区域分布.md](../../../../planning/specs/gameplay/中国鱼种与区域分布.md)
- 开发 prompt：[feat-fish-cn-01-species-regions-dev.prompt.md](../../../../planning/prompts/gameplay/feat-fish-cn-01-species-regions-dev.prompt.md)
- 与 **FEAT-POOL-01** 同迭代：本票填种/区/塘名；POOL-01 做表接线。

### [feat-pool-01-ecology-table] - 2026-08-24

- 立项 **FEAT-POOL-01** 塘生态数值表驱动 → **已确认**（R3 P0）。
- 定稿：① `pond_fish_pool` 表驱动出鱼；② `pond_category_quality_weights` 按塘类型品质权重；③ 尺寸上限仍按品质（`fish_quality_stats.sizeCapM`）；④ 咬钩/体长/脱钩基数入表；⑤ **咬钩脱钩公式不变**。
- 废弃接线：`STOCK_TEMPLATES`、`pond_fish_size_cap` override。
- 规格：[塘生态数值表驱动.md](../../../../planning/specs/gameplay/塘生态数值表驱动.md)
- 开发 prompt：[feat-pool-01-ecology-table-dev.prompt.md](../../../../planning/prompts/gameplay/feat-pool-01-ecology-table-dev.prompt.md)

### [feat-return-03-rules] - 2026-08-24

- **FEAT-RETURN-03** 回鱼规则调优 → **已实现**（设计/完成 2026-08-24）。
- 定稿三条规则：**仅达标鱼回塘**（紫+、体长比≥75%；不达标留包可卖）；**回鱼金 = 卖价×1.5**；**仅收费且 `allowsAutoReturn` 的塘**可回（免票塘 `POND_NO_RETURN`）。
- 同步修订 `FEAT-RETURN-01` / `FEAT-RETURN-02` 正文与验收项。
- 自检：`npm run verify:feat-return-01`、`verify:feat-return-01-server`、`verify:feat-return-02`
- 规格：[回鱼规则调优.md](../../../../planning/specs/gameplay/回鱼规则调优.md)、[回鱼机制.md](../../../../planning/specs/gameplay/回鱼机制.md)、[双价塘与自动回鱼.md](../../../../planning/specs/gameplay/双价塘与自动回鱼.md)

### [steam-desktop-10b-windows-deploy] - 2026-08-24

- 补充 **STEAM-DESKTOP-10B** Windows 云主机部署手册：[`docs/ops/windows-cloud-deploy.md`](../../../../ops/windows-cloud-deploy.md)
- 阿里云对照清单 + Steam 独立游戏后端选型说明：[`docs/ops/windows-cloud-aliyun-checklist.md`](../../../../ops/windows-cloud-aliyun-checklist.md)
- 启动脚本：`scripts/ops/windows-cloud-start.bat`
- 实施仍可暂缓；客户端继续仅改 `server.json` 切换。

### [v0.7-r2-accepted] - 2026-08-24

- **R2 全部验收结案**：`FEAT-RETURN-01` / `FEAT-RETURN-02` / `FEAT-GROUND-01` / `FEAT-ALBUM-01` / `BUG-23` → **已实现**。
- `FEAT-GROUND-01` 完成时间 **2026-08-24**；Epic 增 **R3 候选**（巨物/夜钓/船QTE/活动社交/AI照/数值表接线）。
- 自检：`npm run verify:feat-ground-01`、`verify:feat-ground-01-server`
- 规格：[钓鱼玩法扩展-v0.7-Epic.md](../../../../planning/specs/gameplay/钓鱼玩法扩展-v0.7-Epic.md)、[打窝机制.md](../../../../planning/specs/gameplay/打窝机制.md)

### [feat-return-02-accepted] - 2026-08-24

- **FEAT-RETURN-02** 用户验收通过 → **已实现**（设计 2026-08-23 / 完成 2026-08-24）。
- 交付：进塘双价选择（`sell_only` / `auto_return`）；扣费按档；达标鱼自动回塘（复用 RETURN-01）；出售档禁用回鱼；埋点 `return_fee_mode_selected` / `fish_auto_returned`。
- 自检：`npm run verify:feat-return-02`
- 规格：[双价塘与自动回鱼.md](../../../../planning/specs/gameplay/双价塘与自动回鱼.md)

### [feat-album-01-accepted] - 2026-08-23

- **FEAT-ALBUM-01** 用户验收通过 → **已实现**（设计/完成 2026-08-23）。
- 交付：个人中心壳（资料/展示柜/图鉴/相册/成就）；`PanelProfileHub` Prefab；相册自动入墙（N=12）、双卡满铺分页、纪念照；成就表驱动；`GET /api/players/:id/profile-hub`。
- 自检：`npm run verify:feat-album-01`
- 规格：[钓鱼相册与成就.md](../../../../planning/specs/gameplay/钓鱼相册与成就.md)

### [bug-23-quota-daycap + return-02] - 2026-08-23

- **BUG-23** 已修复并 **已实现**：收费塘扣满 4 次 / 日满 8h 后不可 `start_fishing`；仍可落座；自动续钓与 tick 停钓对齐 Web。自检 `npm run verify:quota-daycap`。
- **FEAT-RETURN-01** 用户验收通过 → **已实现**（设计/完成 2026-08-23）。
- 立项 **FEAT-RETURN-02** 双价塘与自动回鱼（**已确认**）：进塘二选一收费；可回鱼档达标自动回鱼。
- 规格：`BUG修复-日额度满后仍可开钓.md`、`双价塘与自动回鱼.md`
- 开发提示词：`feat-return-02-dual-fee-auto-return-dev.prompt.md`

### [v0.7-r2-specs] - 2026-08-23

- **EPIC-FISH-V07** 增 R2 章节：R1 已交付说明；开题三票。
- 立项并 **已确认**：
  - **FEAT-RETURN-01** 回鱼机制（回塘增重、准入、金≈卖价×0.7）
  - **FEAT-GROUND-01** 打窝机制（Overlay 并列循环、50 层非线性、0.5~1.5%/层曲线、附近鱼小幅尺寸）
  - **FEAT-ALBUM-01** 钓鱼相册与成就（个人中心大改 UI、相册墙、成就）
- 规格：`docs/planning/specs/gameplay/回鱼机制.md`、`打窝机制.md`、`钓鱼相册与成就.md`
- 开发提示词：`feat-return-01-return-fish-dev.prompt.md`、`feat-ground-01-groundbait-dev.prompt.md`、`feat-album-01-profile-codex-album-dev.prompt.md`
- UI 草案：[steam-profile-hub.md](../../../../design/ui/profile/steam-profile-hub.md)

### [feat-spot-01-accepted] - 2026-08-22

- **FEAT-SPOT-01** 用户验收通过，状态改为 **已实现**（设计 2026-08-21 / 完成 2026-08-22）。
- 交付：坐席后 09C 私有观察泡；`spot_clue_texts` + `spot_tags` 表加权随机 habitat/activity；换点重抽；无点位常驻字；换座泡跟随宠物定位。
- 自检：`npm run verify:feat-spot-01`
- 规格：[钓位点位线索文字泡.md](../../../../planning/specs/gameplay/钓位点位线索文字泡.md)

### [steam-desktop-12-accepted] - 2026-08-22

- **STEAM-DESKTOP-12** 用户验收通过，状态改为 **已实现**（设计 2026-08-22 / 完成 2026-08-22）。
- 交付：Overlay「Debug」+ Unity F8；`POST /api/debug/gameplay`（升级/满级/塘熟练度/加钱/出警/鱼获/+2h）；埋点 `gameplay_debug_action`。
- 自检：`npm run verify:steam-desktop-12`、`npm run verify:steam-desktop-12-server`
- 规格：[Steam桌面端-玩法Debug菜单.md](../../../../planning/specs/desktop/Steam桌面端-玩法Debug菜单.md)

### [feat-spot-01-revise-chat-bubble] - 2026-08-22

- 修订 **FEAT-SPOT-01**：取消钓位旁常驻字 → **坐席后**用 **09C 聊天文字泡**（仅本玩家）。
- 机制：全部文案入 `spot_clue_texts` 表；点位**不固定**文案，**加权随机**；类型 `habitat`（鱼喜环境）+ `activity`（鱼情观察）；约 30 条现实经验种子文案。
- 规格：[钓位点位线索文字泡.md](../../../../planning/specs/gameplay/钓位点位线索文字泡.md)
- **开发已按修订落地**（Unity 抽选 + Overlay 私有观察泡）；自检 `npm run verify:feat-spot-01`；规格仍为 **已确认**，待验收。

### [steam-desktop-12-impl] - 2026-08-22

- **STEAM-DESKTOP-12** 开发落地：统一玩法 Debug 菜单。入口为开发版 Overlay 右上「Debug」与 Unity **F8**（Editor 托盘仍占 F9）；Release 默认无入口，需 `FISH_SOCIAL_GAMEPLAY_DEBUG=1`。服务端 `POST /api/debug/gameplay`，生产需 `GAMEPLAY_DEBUG=1`。
- 动作：升级 / 满级 / 当前塘熟练度 +1 或锁顶 / +100 万金 / 一键出警（与 RISK-01 同源）/ 发放鲫鱼·灰·0.18m / 入场费进度 +2h。
- 埋点：`gameplay_debug_action`
- 自检：`npm run verify:steam-desktop-12`、`npm run verify:steam-desktop-12-server`
- 规格仍为 **已确认**，待用户验收后再改 **已实现**。

### [steam-desktop-12-gameplay-debug] - 2026-08-22

- 立项 **STEAM-DESKTOP-12**：开发版统一玩法 Debug 菜单（呼出/关闭；升级；满级；塘熟练度；+100万金；一键出警；获得鱼获；钓鱼时长 +2h 测扣费）。
- 规格：[Steam桌面端-玩法Debug菜单.md](../../../../planning/specs/desktop/Steam桌面端-玩法Debug菜单.md)
- 开发提示词：[steam-desktop-12-gameplay-debug-menu-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-12-gameplay-debug-menu-dev.prompt.md)

### [feat-spot-01-impl] - 2026-08-22

- **FEAT-SPOT-01** 开发落地：Unity 读 `spot_clues`，按玩家等级 + 当前塘熟练度过滤后经 IPC 下发；Overlay 钓位旁粘性文字泡（复用聊天气泡样式，仅本玩家可见）。换塘 / 领鱼刷新进度。文案占位，不暴露 bite/escape。
- 自检：`npm run verify:feat-spot-01`
- 规格仍为 **已确认**，待用户验收后再改 **已实现**。
- 规格：[钓位点位线索文字泡.md](../../../../planning/specs/gameplay/钓位点位线索文字泡.md)

### [feat-risk-01-accepted] - 2026-08-22

- **FEAT-RISK-01** 用户验收通过，状态改为 **已实现**（设计 2026-08-21 / 完成 2026-08-22）。
- 交付：禁止塘钓鱼中按表概率出警；10s 内离塘免罚并 2h 禁入；超时扣金（不足归零）+ 当日禁钓；Overlay 气泡「巡警来了！快跑！」；禁止塘 Overlay「一键出警」。
- 规格：[禁止钓鱼塘巡警事件.md](../../../../planning/specs/gameplay/禁止钓鱼塘巡警事件.md)

### [feat-risk-01-impl] - 2026-08-22

- **FEAT-RISK-01** 开发落地：禁止塘钓鱼中按表概率出警；10s 内离塘免罚并 2h 禁入；超时扣金（不足归零）+ 当日禁钓；Overlay 气泡「巡警来了！快跑！」；开发版禁止塘 Overlay「一键出警」。
- 埋点：`forbidden_pond_fine` / `forbidden_pond_escaped`。
- 规格仍为 **已确认**，待用户验收后再改 **已实现**。

### [feat-risk-01-debug-police] - 2026-08-22

- **FEAT-RISK-01** Debug「一键出警」入口改到禁止塘 Overlay 右上；设置面板不再放置。Release 不暴露。
- 规格：[禁止钓鱼塘巡警事件.md](../../../../planning/specs/gameplay/禁止钓鱼塘巡警事件.md) §2.1
- 提示词已同步：[feat-risk-01-forbidden-police-dev.prompt.md](../../../../planning/prompts/gameplay/feat-risk-01-forbidden-police-dev.prompt.md)

### [steam-desktop-08a2-accepted] - 2026-08-22

- **STEAM-DESKTOP-08A2** 用户验收通过，状态改为 **已实现**（设计 2026-08-21 / 完成 2026-08-22）。
- 交付：世界地图读 `ponds.json` 六类分区与锁态；新手不上图；巨物暂闭；收费塘详情 + 二次确认进塘；免票直进；拒绝原因展示。
- 规格：[Steam桌面端-08A2世界地图分区与进塘扣费.md](../../../../planning/specs/desktop/Steam桌面端-08A2世界地图分区与进塘扣费.md)

### [feat-gear-01-accepted] - 2026-08-22

- **FEAT-GEAR-01** 用户验收通过，状态 **已实现**（设计 2026-08-21 / 完成 2026-08-22）。
- 竿读表弱加成与超规格销毁；无可用竿不可开钓；首次读装备发竹竿。
- 饵按等级解锁、按次扣金、不进货；船具可买不可用。
- Steam 商店 Prefab 三栏（鱼饵 / 钓竿 / 船具），详情展示表内字段中文说明。

### [feat-prog-01-accepted] - 2026-08-21

- **FEAT-PROG-01** 用户验收通过，状态 **已实现**（设计/完成 2026-08-21）。
- 七类塘准入、双熟练度、每 2h 扣费、卖价公式、数值表导出、野外/水库咬钩乘子已落地；新手教学关见 **STEAM-DESKTOP-11**。
- 验收接受过渡期：生态鱼种仍硬编码 STOCK 轮转（`pond_fish_pool` JSON 已导出未驱动 spawn）。商店读表仍属 **FEAT-GEAR-01**。

### [steam-desktop-11-accepted] - 2026-08-21

- **STEAM-DESKTOP-11** 用户验收通过，状态 **已实现**（设计/完成 2026-08-21）。
- 关联 **FEAT-PROG-01** 仍为已确认：地图类型/锁态、巨物暂闭、背包参考价已补代码，商店读表属 **FEAT-GEAR-01**。

### [steam-desktop-11-onboarding-ux] - 2026-08-21

- **STEAM-DESKTOP-11** 修订：引导气泡随换位/离席跟随；去掉 Overlay 顶栏与主窗口重复文案；引导中锁定世界地图等功能栏。
- 领鱼对齐 Web：自动入包 + 鱼获弹窗（确认或 5 秒）→ 完成确认后离开教学关；phase 显示装饵/抛竿/等待/上钩/收鱼。

### [steam-desktop-11-local-onboarding] - 2026-08-21

- 立项 **STEAM-DESKTOP-11**：新手引导改为 Overlay **本地教学关**（5 秒必上钩 / 5 秒圆圈），不 `join pond-novice`、不走正式塘咬钩循环。
- 服务端只权威保存 `onboardingCompleted`；领取打 `complete-onboarding`。
- 同步修订 **FEAT-PROG-01** §3.4 与 Epic #12。
- 规格：[Steam桌面端-新手引导本地教学关.md](../../../../planning/specs/desktop/Steam桌面端-新手引导本地教学关.md)
- 开发提示词：[steam-desktop-11-local-onboarding-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-11-local-onboarding-dev.prompt.md)

### [feat-prog-01-impl] - 2026-08-21

- **FEAT-PROG-01** 代码落地（待用户验收后再改 spec 为已实现）：
  - `钓鱼玩法固定数值表.xlsx` + `npm run game-data:export` → `shared/generated/game-data/` + Unity `Resources/GameData/`
  - 七类塘准入、`pond-novice`、双熟练度、每 2h 扣费、新卖价公式、野外/水库乘子
  - Unity：首登强制 Overlay 新手引导
  - 自检：`npm run verify:feat-prog-01`

### [fish-v07-r1-split] - 2026-08-21

- 拆分 **EPIC-FISH-V07** 钓鱼玩法扩展 R1：Epic 索引 + 五张已确认功能票。
- 编号：`FEAT-PROG-01`、`FEAT-GEAR-01`、`STEAM-DESKTOP-08A2`、`FEAT-RISK-01`、`FEAT-SPOT-01`（均 **已确认**，设计 2026-08-21）。
- 建议实现序：PROG → GEAR → 08A2 → RISK → SPOT；固定数值表驱动，不进云库。
- 规格：[钓鱼玩法扩展-v0.7-Epic.md](../../../../planning/specs/gameplay/钓鱼玩法扩展-v0.7-Epic.md) 及关联子 spec。
- 开发提示词：`feat-prog-01-*` / `feat-gear-01-*` / `steam-desktop-08a2-*` / `feat-risk-01-*` / `feat-spot-01-*`。

### [steam-desktop-10b-cloud-reserved] - 2026-08-20

- 登记 **STEAM-DESKTOP-10B** 云服务器联调为 **已确认（保留/暂缓）**。
- **STEAM-DESKTOP-10A** 因 WAN CGNAT（`100.111.98.148`）→ **已废弃**。
- 当前版本基线：**本机 / 局域网联机**；上云时仅改 `server.json` 即可切换。
- 规格：[Steam桌面端云服务器联调保留.md](../../../../planning/specs/desktop/Steam桌面端云服务器联调保留.md)

### [steam-desktop-10a-home-public] - 2026-08-20

- 立项 **STEAM-DESKTOP-10A**：本机公网映射联调（方案 A）；后因 CGNAT **已废弃**。
- 设置页保存/`health` 测试与本机检查脚本仍保留给局域网使用。
- 规格：[Steam桌面端本机公网映射联调.md](../../retired/planning/specs/Steam桌面端本机公网映射联调.md)

### [steam-desktop-10-accepted] - 2026-08-20

- **STEAM-DESKTOP-10** 用户验收通过，状态改为 **已实现**（设计/完成：2026-08-20）。
- Windows Build 日志：`serverBaseUrl=http://localhost:3001 source=file:.../Builds/Windows64/server.json`
- 设置页显示当前服务器；Steam 登录正常。
- 公网迁云与外地联调仍按规格 §6 手册执行，不阻塞本需求结案。

### [steam-desktop-10-public-server-url] - 2026-08-20

- 登记 **STEAM-DESKTOP-10**：公网联调与服务器地址配置（**已确认**）。
- 最优方案：本机开发与验收 → 需要外网时再上云；本机服可迁云。
- P0 开发：Unity 读取 `server.json` / `FISH_SOCIAL_SERVER_URL`，统一注入 Steam 登录、Lobby、Socket。
- 新增规格：[Steam桌面端公网联调与服务器地址配置.md](../../../../planning/specs/desktop/Steam桌面端公网联调与服务器地址配置.md)
- 开发提示词：[steam-desktop-public-server-url-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-public-server-url-dev.prompt.md)

### [steam-desktop-09c-accepted] - 2026-08-20

- `STEAM-DESKTOP-09C` Overlay 鱼塘聊天气泡与输入用户验收通过，计划状态改为 **已实现**。
- 交付：左下公屏最近消息气泡 + 紧凑输入（≤200 字）；IPC `recentChats[]` / `send_pond_chat`；Unity 环形缓冲 + 现有 `SendChat`。
- **STEAM-DESKTOP-09（09A–09D）** 四条 Overlay 扩展需求全部 **已实现**。

### [steam-desktop-09c-overlay-pond-chat] - 2026-08-20

- **STEAM-DESKTOP-09C** Overlay 鱼塘公屏聊天气泡与输入 **开发交付**。
- Overlay：左下最近 20 条公屏气泡 + 底部紧凑输入（placeholder「说点什么…」，≤200 字，空消息不可发）。
- IPC：状态 `recentChats[]`；命令 `send_pond_chat { text }`。Unity 订阅 `chat_message` 环形缓冲并走现有 `SendChat`；Overlay 不直连 Socket。
- **07E 扩展**：完整历史 / 私聊 / 好友列表仍在主窗口 `PanelSocial`；Overlay 仅为挂机路径轻量公屏。
- 涉及：`desktop-overlay/`、`fish-social-unity/Assets/Scripts/Desktop/`（DTO / `OverlayPondStateBuilder` / `DesktopAppBootstrap` / `SocialPondSessionController`）。

### [steam-desktop-09b-accepted] - 2026-08-20

- `STEAM-DESKTOP-09B` Overlay 悬停状态与钓鱼时长用户验收通过，计划状态改为 **已实现**。
- 交付：IPC `fishingPhase` / `sessionFishingMs` / `hookDeadlineMs`；悬停 ≥300ms Tooltip **仅**本局时长或收杆剩余。
- 验收修复：悬停双卡片残留；Bot 钓鱼时长计时异常。
- 涉及：`desktop-overlay/`（悬停 Tooltip）、`fish-social-unity/Assets/Scripts/Desktop/`（DTO / `PondUserMerge` / tick 合并）。

### [steam-desktop-09a-accepted] - 2026-08-20

- `STEAM-DESKTOP-09A` Overlay 玩家右键菜单用户验收通过，计划状态改为 **已实现**。
- 交付：同塘玩家右键 ContextMenu（查看资料 / 添加好友 / 私聊 / 点赞互动）；IPC `player_*` + `playerId`；Unity `OverlayPlayerSocialBridge` 路由主窗口资料/私聊与 REST 社交 API。
- 涉及：`desktop-overlay/OverlayPetActor.cs`、`IpcProtocol.cs`、`fish-social-unity/Assets/Scripts/Desktop/OverlayPlayerSocialBridge.cs` 等。

### [steam-desktop-09d-accepted] - 2026-08-20

- `STEAM-DESKTOP-09D` Overlay 布局与角色表现优化用户验收通过，计划状态改为 **已实现**。
- 交付：Overlay **960×560**；宠物 **64×64**；左上收纳菜单（含 08G 钓鱼操作）；默认昵称/状态/上钩圆环；悬停 ≥300ms 仅时长 Tooltip；Bot 无「·机」。
- IPC：`fishingPhase` / `sessionFishingMs` / `hookDeadlineMs` / `ownNickname` 已落地（09B 字段合并进本需求）。
- 涉及：`desktop-overlay/`、`fish-social-unity/Assets/Scripts/Desktop/`（Overlay DTO、Socket `session_timer_tick`、启动参数）。

### [steam-desktop-09d-overlay-layout] - 2026-08-20

- 新增 **STEAM-DESKTOP-09D** Overlay 布局与角色表现优化（**已确认** / P0）：
  - 窗口 **960×560**；宠物 **64×64**（原 50%）
  - 塘内每位用户昵称默认在头像上方；Bot 不显示「·机」
  - 默认显示钓鱼状态 + 上钩圆环；**悬停仅显示钓鱼时长**（09B 收窄）
  - 底部横栏与右上「打开主界面」合并为**左上纵向收纳菜单**（位于状态/鱼塘/钓位之上）
- 修订 **STEAM-DESKTOP-09B** spec/prompt：不再沿用 Web「他人默认隐藏状态」
- Prompt：`steam-desktop-09d-overlay-layout-dev.prompt.md`

### [steam-desktop-09-overlay-social] - 2026-08-19

- 新增 Overlay 社交扩展三条 **已确认** 需求（登记计划表 + 开发提示词）：
  - **STEAM-DESKTOP-09A** 玩家右键菜单（资料 / 加好友 / 私聊 / 点赞）
  - **STEAM-DESKTOP-09B** 悬停状态与钓鱼时长 Tooltip（对齐 FEAT-UI-1）
  - **STEAM-DESKTOP-09C** 鱼塘公屏聊天气泡 + 紧凑输入（修订 07E「聊天仅主窗口」为轻量 Overlay 公屏）
- Spec：`Steam桌面端-09A/B/C*.md`；Prompt：`steam-desktop-09a/b/c-*-dev.prompt.md`

### [feat-soc-03b-accepted] - 2026-08-19

- `FEAT-SOC-03b` 排行榜入口与领奖台改版用户验收通过，计划状态改为 **已实现**。
- Web：首页 `LeaderboardButton` → `/leaderboard`；仅日/周分栏；Top3 领奖台（2-1-3）；4+ 纵向列表；社交中心排行 Tab 跳转独立页。
- 服务端：`weekly-king` 改为本周单条最大鱼（`pickBestPerPlayer` + `sizeM`），与 daily-biggest 同结构。
- P1 首页 Top3 预览条未做，不影响验收。

### [steam-desktop-08d-accepted-final] - 2026-08-20

- `STEAM-DESKTOP-08D` 排行榜终验通过，计划完成时间更新为 **2026-08-20**。
- 修复 Tab 切换协程竞态、客户端缓存与后台预拉；四榜切换、领奖台、纵向列表、我的排名与 `menu_leaderboard` 入口均符合 spec。

### [steam-desktop-08d-accepted] - 2026-08-19

- `STEAM-DESKTOP-08D` 排行榜用户验收通过，计划状态改为 **已实现**。
- 完成日/周/鱼塘/稀有四榜、固定 Image 领奖台、纵向列表、我的排名，以及主窗口/Overlay `menu_leaderboard` 入口。
- 验证切榜不触发 `leave_pond`，不重建 Overlay / Socket。

### [steam-desktop-08d-podium-layout] - 2026-08-19

- 用户确认 `STEAM-DESKTOP-08D` UI：保留日榜、周榜、鱼塘榜、稀有榜四类，不沿用 `FEAT-SOC-03b` 的「仅日/周」。
- Top 1–3 为固定 Image 领奖台（左 2 / 中 1 / 右 3），占位含昵称、头像、用户数据；缺人空位。
- 第 4 名及以后在下方独立纵向 `ScrollRect`，禁用横向滚动；切页不离塘。

### [steam-desktop-08c-accepted] - 2026-08-19

- `STEAM-DESKTOP-08C` 动态墙与好友动态用户验收通过，计划状态改为 **已实现**。
- 完成公共/好友动态切换、服务端分页、动态卡片与评论行 Prefab、点赞、评论、删除评论、图片加载限流和实时刷新。
- 动态页切换不触发 `leave_pond`，关闭页面时释放动态卡片实例。

### [steam-desktop-08e-accepted] - 2026-08-18

- `STEAM-DESKTOP-08E` 个人中心与资料编辑用户验收通过，计划状态改为 **已实现**。
- 验证个人资料服务端回读、昵称/简介/头像/展示鱼获编辑、失败重试，以及编辑期间不离开鱼塘、不重建 Overlay。
- 恢复 SteamID 到既有玩家记录后，确认 2 条背包鱼获、1 条好友关联和 2659 条钓鱼记录可见。

### [steam-desktop-08i-latency-plan] - 2026-08-18

- 根据 Unity `Player.log` 与 Overlay 延迟日志补充 `STEAM-DESKTOP-08I` 修复方案。
- 明确主窗口隐藏时 5 FPS 后台节流会阻塞 Overlay 交互，要求 Overlay 活跃期间保持交互帧率。
- 明确 Socket ACK/错误/连接状态优先级、状态序号持久递增、重复状态丢弃和高频广播下的队列保护。
- 增加 Overlay → Unity → Socket → ACK → 状态回传的时间戳关联规则，以及 P50/P95 延迟验收指标。
- `STEAM-DESKTOP-08I` 已完成运行时复测，用户验收通过，状态改为 **已实现**。

### [steam-desktop-08i-accepted] - 2026-08-18

- `STEAM-DESKTOP-08I` 用户验收通过，退出鱼塘、离席、跨塘切换及 Overlay 延迟优化全部完成。
- 验证 Overlay 活跃期间 Unity 交互帧率、ACK 优先队列、持久状态序号、重复状态丢弃及三段链路时间戳日志。

### [steam-desktop-08b-accepted] - 2026-08-18

- `STEAM-DESKTOP-08B` 商店与装备用户验收通过，计划状态改为 **已实现**。
- `PanelShop`、服务端商品/装备同步、购买/装备失败处理及主窗口/Overlay 菜单入口完成验收。
- 下一阶段：`STEAM-DESKTOP-08E` 个人中心与资料编辑（08 剩余 P1：08E → 08C → 08D）。

### [steam-desktop-08i-pond-exit-switch] - 2026-08-18

- 新增 `STEAM-DESKTOP-08I`：鱼塘退出、离席、跨塘切换与 Overlay 反馈延迟优化。
- 明确 `leave_spot`、`leave_pond`、收杆/领鱼/离席事务，以及世界地图跨塘切换的生命周期边界。
- 明确 Overlay 点击钓位直接入座、底部新增离席/退出鱼塘按钮和 Named Pipe 命令优先级优化。
- 登记计划表并生成开发交接提示词，状态为 **已确认**。

### [steam-desktop-08g-accepted] - 2026-08-17

- `STEAM-DESKTOP-08G` 用户验收通过，计划状态改为 **已实现**。
- 完成 Overlay 钓位选择、开始钓鱼、收杆、领取鱼获、状态同步、错误回显及拖动命中区域验收。
- 下一阶段：`STEAM-DESKTOP-08A` 世界地图与鱼塘选择。

### [steam-desktop-08a-07a-accepted] - 2026-08-17

- `STEAM-DESKTOP-08A` 世界地图与鱼塘选择用户验收通过，计划状态改为 **已实现**。
- Web/shared 鱼塘目录已导入 Unity 世界地图，包含 20 个鱼塘及 `pondId`、名称、`regionId`、主题、坐标和容量。
- Unity UI Prefab 管理工具验收通过，支持查看、新增、修改高亮、更新和删除无效 Prefab。
- `STEAM-DESKTOP-07A` 桌面宠物主视图与鱼塘入口用户验收通过，继续保持 **已实现**。
- 下一阶段：`STEAM-DESKTOP-08B` 商店与装备。

### [steam-desktop-08f-accepted] - 2026-08-17

- `STEAM-DESKTOP-08F` 用户验收通过，计划状态改为 **已实现**。
- 下一阶段：`STEAM-DESKTOP-08G` Overlay 钓鱼操作栏。

### [steam-desktop-08h-ui-prefab-migration] - 2026-08-17

- 新增 `STEAM-DESKTOP-08H`：将 Steam 桌面端壳层、功能页、列表行和 Grid item
  统一迁移为合法 Unity Prefab。
- 双栏社交结构确定由 `PanelSocial.prefab` 统一承载，删除未被运行时加载的
  `PanelFriends`、`PanelFriendRequests`、`PanelDirectMessages`、`PanelPondChat` 副本。
- 明确好友申请接受/拒绝、Steam 好友邀请、私聊和移除按钮的独立 Button 与统一尺寸。
- 明确背包格、图鉴格、好友行、聊天行和私聊行的 Prefab 分类，运行时代码只绑定数据和事件。
- 登记计划表并刷新进度看板，交接提示词为
  `steam-desktop-08h-ui-prefab-migration-dev.prompt.md`。

### [steam-desktop-08-feature-split] - 2026-08-17

- 将 07A～07G 完成后的后续 Steam 功能拆分为 7 个独立需求：
  `08A` 世界地图、`08B` 商店、`08C` 动态墙、`08D` 排行榜、`08E` 个人中心、`08F` 好友列表 Prefab、`08G` Overlay 钓鱼操作栏。
- 明确新增页面均使用独立 Prefab；好友申请接受/拒绝按钮单独布局，修复重叠不可点击问题。
- 明确世界地图使用可拖动/缩放的大图和与最终美术绑定的 `pondId` 坐标。
- 确认 Overlay 可承载选择钓位、开始钓鱼、收杆和领取鱼获，业务仍由 Unity/服务端权威执行。
- 已登记计划表并刷新看板。建议开发顺序：`08F` → `08G` → `08A` → `08B`，然后 P1：`08E` → `08C` → `08D`。

### [steam-desktop-07f-accepted] - 2026-08-17

- `STEAM-DESKTOP-07F` 用户验收通过，计划状态改为 **已实现**（设计 2026-08-13，完成 2026-08-17）。
- Windows Development Build 全流程通过：登录、进塘、Overlay、钓鱼、托盘挂机、咬钩通知、恢复收鱼、页签不离塘、断线按快照恢复；进塘不启动第二个 Unity Player。
- 父需求 `STEAM-DESKTOP-07` 改为 **已实现**（07A～07G 均完成）。美术 `ART-01` / `ART-02` 仍为已确认。

### [steam-desktop-07g-accepted] - 2026-08-17

- `STEAM-DESKTOP-07G` 用户验收通过，计划状态改为 **已实现**（设计 2026-08-14，完成 2026-08-17）。
- 独立 `FishSocialOverlay.exe`（WPF `960×480`），Named Pipe 收状态/发命令，不启动第二个 Unity Player，不连 Socket、不传图。
- `STEAM-DESKTOP-07` 表现层父需求与 07A～07G 子需求均已完成；后续开发转入 `STEAM-DESKTOP-08` 系列。

### [steam-desktop-07a-07e-tools-bugs] - 2026-08-17

- 整理 `STEAM-DESKTOP-07A`～`07E` 开发工具与已修缺陷：[`reports/STEAM-DESKTOP-07A-07E-工具与缺陷.md`](../../../../planning/reports/STEAM-DESKTOP-07A-07E-工具与缺陷.md)。
- `07A` / `07B` 计划状态改为 **已实现**（完成时间 2026-08-14）；`07E` 完成时间更新为 **2026-08-17**（Prefab 为唯一功能页来源，用户确认完全正确）。
- 父需求 `STEAM-DESKTOP-07` 仍为 **已确认**（07F 未完成）。

### [steam-desktop-07e-accepted] - 2026-08-16

- `STEAM-DESKTOP-07E` 用户验收通过，计划状态改为 **已实现**。
- 好友/聊天、背包、图鉴、设置为主窗口页签；Overlay/底栏菜单切页，不用功能弹窗。
- 从 Overlay 菜单唤起后主窗口高于 Overlay；回托盘后 Overlay 恢复置顶。
- 父需求 `STEAM-DESKTOP-07` 仍为 **已确认**（07F 未完成）。
- 下一阶段：`STEAM-DESKTOP-07F` 桌面宠物主流程与恢复验收。

### [steam-desktop-07e-main-window-tabs] - 2026-08-16

- `STEAM-DESKTOP-07E` 改口径：好友/聊天、背包、图鉴、设置改为主窗口页签，取消功能弹窗。
- Overlay / 底栏右键菜单只 `Show` 对应 `ShellPanelId`，不 `Open` `DesktopModalHost` 整页。
- **补充：** 从 Overlay 菜单唤起后，主窗口必须高于 Overlay；回托盘后 Overlay 恢复置顶。
- 主窗口壳与功能页收为 Prefab。权威设计：[`Steam桌面端Web功能对齐设计.md`](../../../../planning/specs/desktop/Steam桌面端Web功能对齐设计.md)。
- 开发提示词：[`steam-desktop-07e-main-window-tabs-dev.prompt.md`](../../../../planning/prompts/desktop/steam-desktop-07e-main-window-tabs-dev.prompt.md)。

### [steam-desktop-art-02-overlay-layout] - 2026-08-16

- 新增美术后续需求 `STEAM-DESKTOP-ART-02`：Unity `960×480` Canvas Prefab 导出布局 JSON，Overlay 按像素表摆图与钓位。
- Overlay 仍不加载 Prefab/uGUI；Named Pipe 不传图；有布局表的塘停用 `MapToScene` 自动缩放。
- 规格：[`Steam桌面Overlay场景布局管线.md`](../../../../planning/specs/desktop/Steam桌面Overlay场景布局管线.md)；提示词：[`steam-desktop-art-02-overlay-layout-pipeline-dev.prompt.md`](../../../../planning/prompts/desktop/steam-desktop-art-02-overlay-layout-pipeline-dev.prompt.md)。
- 不阻塞 `STEAM-DESKTOP-07E` / `07F`。

### [steam-desktop-07e-web-parity-design] - 2026-08-16

- 新增 `STEAM-DESKTOP-07E-DESIGN`，明确 Web→Steam 的功能与数据对齐范围。
- 细化好友/聊天、背包、图鉴、设置弹窗的字段、权限、REST/Socket 事件和异常状态。
- 明确 Steam 版采用桌面布局，不直接迁移 Web 路由；动态墙、Feed、排行榜和商店后置。
- 更新 `STEAM-DESKTOP-07E` 开发提示词、表现层验收项和规格索引。

### [steam-desktop-07d-accepted] - 2026-08-16

- `STEAM-DESKTOP-07D` 用户验收通过，计划状态改为 **已实现**。
- 主窗口与 Overlay 鱼塘区域右键菜单提供当前鱼塘、好友与聊天、鱼获/背包、图鉴、设置、隐藏到托盘、退出；菜单视图只分发事件。
- 不拦截 Windows 桌面系统右键；隐藏到托盘/退出走现有生命周期；菜单操作不 `leave_pond`。弹窗内部仍属 07E。
- 父需求 `STEAM-DESKTOP-07` 仍为 **已确认**（07E～07F 未完成）。
- 下一阶段：`STEAM-DESKTOP-07E` 桌面宠物功能弹窗层。

### [steam-desktop-07c-accepted] - 2026-08-15

- `STEAM-DESKTOP-07C` 用户验收通过，计划状态改为 **已实现**。
- Overlay 以服务端 `pond_snapshot` / `pond_user_joined/left/updated` 显示同塘玩家（含机器人），`fishingPhase` → `petVisualState`，本地序列帧，IPC 不传图。
- 按 `playerId` 复用对象；无钓位时岸边等待位排列；打开主界面不离塘。
- 父需求 `STEAM-DESKTOP-07` 仍为 **已确认**（07D～07F 未完成）。
- 下一阶段：`STEAM-DESKTOP-07D` 桌面宠物右键菜单。

### [steam-desktop-07c-overlay-frames-sync] - 2026-08-15

- 确认 Steam 桌面与 web/RN **权威相同、表现不同**：不平移移动端页面，不迁移旧档。
- Overlay **支持序列帧**，且必须本地播放：Unity 只推 `petVisualState`，不经 Named Pipe 传图。
- 同塘同步沿用现有 Socket（`pond_snapshot` / `pond_user_*`），Overlay 不连网；07C 只扩展状态 DTO。
- 已写入 [`Steam桌面宠物与多人鱼塘表现层.md`](../../../../planning/specs/desktop/Steam桌面宠物与多人鱼塘表现层.md)、[`Steam原生桌面宠物Overlay.md`](../../../../planning/specs/desktop/Steam原生桌面宠物Overlay.md)、[`Steam桌面端产品定位与信息架构.md`](../../../../planning/specs/desktop/Steam桌面端产品定位与信息架构.md) 与 07C 开发提示词。

### [steam-desktop-07g-native-overlay-architecture] - 2026-08-14

- 根据第二 Unity Player + `UniWindowController` 实测出现的全屏、Skybox、主窗口阻塞、窗口样式损坏和资源占用问题，停止继续修补该方案。
- `STEAM-DESKTOP-07A`～`07F` 明确只运行 Unity 主窗口，进入鱼塘不得启动第二个 Unity Player。
- 新增 `STEAM-DESKTOP-07G`：使用独立 WPF/Win32 原生 Overlay，通过 Named Pipe 接收状态和发送命令。
- 新增开发提示词：[steam-desktop-07g-native-overlay-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-07g-native-overlay-dev.prompt.md)。

### [steam-desktop-07a-sprite-state-machine] - 2026-08-14

- `STEAM-DESKTOP-07A` 首版动画方案收口为“序列帧 + 宠物状态机”。
- 状态机覆盖待机、钓鱼、咬钩、收鱼、拖动和离线状态。
- 渲染器与状态机解耦，后续可替换为 Spine，不新增需求编号。

### [steam-desktop-03-accepted-07a-placeholder] - 2026-08-14

- `STEAM-DESKTOP-03` 核心链路已验收：Lobby 创建/加入、邀请与 `pondId` 映射、权限拒绝、Lobby 失效后重新进塘、房主离开不删除鱼塘。
- 双 Steam 账号联调因缺少第二测试账号跳过，不阻塞后续开发；计划状态改为 **已实现**。
- 下一阶段从 `STEAM-DESKTOP-07A` 开始，先使用可替换的空白正方形 2D 猫咪占位 UI。

### [steam-desktop-03-acceptance-and-07a-kickoff] - 2026-08-14

- `STEAM-DESKTOP-03` 核心链路验收完成：Lobby 创建/加入、邀请与 `pondId` 映射、权限拒绝、Lobby 失效后重新进塘、房主离开不删除鱼塘。
- 双 Steam 账号联调因缺少第二测试账号跳过，不阻塞后续开发；需求状态改为 **已实现**。
- 下一阶段从 `STEAM-DESKTOP-07A` 开始，先使用可替换的空白正方形 2D 猫咪占位 UI，后续再替换正式资源。

### [steam-desktop-01-desktop-pet-ia] - 2026-08-13

### 产品定位深化

- 将 **STEAM-DESKTOP-01** 明确为“2D 多人社交桌面宠物”产品
- 补充桌面宠物主视图、2D 鱼塘环境、同塘玩家宠物显示
- 补充产品区域右键菜单和好友/背包/图鉴/设置弹窗结构
- 补充启动、进塘、挂机、通知、弹窗、托盘和异常恢复主流程
- 更新开发提示词：[steam-desktop-product-ia-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-product-ia-dev.prompt.md)

### [steam-desktop-07-pet-visual] - 2026-08-13

### 开发拆分

- 将 `STEAM-DESKTOP-EPIC` / `STEAM-DESKTOP-01` 拆分为 **STEAM-DESKTOP-07**
- 新增桌面宠物、2D 多人鱼塘、同塘玩家宠物、右键菜单、弹窗和主流程验收范围
- 新增开发提示词：[steam-desktop-pet-visual-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-pet-visual-dev.prompt.md)
- 明确复用已完成的 Steam 认证、Lobby、鱼塘会话、桌面壳和离线生态能力，不重复开发

### 规划状态收口

- `STEAM-DESKTOP-EPIC` 与 `STEAM-DESKTOP-01` 的产品规划文件标记为 **已文档化**
- 实际开发任务统一转入 `STEAM-DESKTOP-07`
- 看板类型统计将“已文档化”视为规划类项目已关闭，不再显示为未完成开发项

### STEAM-DESKTOP-07 细分

- 移除单独的“功能开发”分类项
- 将 STEAM-DESKTOP-07 拆为六个独立“功能”项：07A～07F
- 每个子项分别登记计划状态、依赖、验收范围和开发提示词

### UI 任务线修正

- 修正此前将“UI 功能拆分”和“程序 UI / 美术 UI 分工”混为一体的问题
- UI 需求改为 `STEAM-UI-PROG-*` 与 `STEAM-UI-ART-*` 两类计划项
- 程序 UI 负责 Unity 交互、状态绑定、Prefab 和弹窗生命周期
- 美术 UI 负责视觉资源、图标、动画、布局规范和资源清单

### UI 分类再次收口

- 取消程序 UI / 美术 UI 一对一的 14 项拆分
- 程序 UI 并入 `STEAM-DESKTOP-07A～07F` 的功能开发项
- 新增 `STEAM-DESKTOP-ART-01`，统一承载猫咪、鱼塘和基础视觉资源替换
- Unity `P0～P3` 统一归入 `架构`，不再单独形成 Unity 类型

### [bugfix-steam-lobby-permission-state] - 2026-08-13

### 排障登记

- 登记 **BUG-22**：Steam Lobby 创建权限拒绝与状态残留
- 新增需求文档：[BUG修复-SteamLobby创建权限与状态残留.md](../../../../planning/specs/bugs/BUG修复-SteamLobby创建权限与状态残留.md)
- 新增开发提示词：[bugfix-steam-lobby-permission-state-dev.prompt.md](../../../../planning/prompts/bugs/bugfix-steam-lobby-permission-state-dev.prompt.md)
- 同步 `项目开发需求计划表.xlsx` 与 `策划进度看板.html`

### 后续优化登记

- 登记 **STEAM-DESKTOP-06**：Steam Lobby 生命周期与邀请反馈优化
- 根据用户决策跳过本需求，不进入当前版本开发排期
- 修复范围：服务端稳定错误码与 Steam 绑定诊断、Unity 创建失败回滚、Lobby 与鱼塘生命周期边界

### 范围调整

- **STEAM-DESKTOP-06** 标记为 **已废弃**
- 后续如确认正式 Lobby 产品方案，再以新需求重新立项

### 开发进展

- 已提交服务端创建权限诊断字段、稳定错误码和绑定/AppID/player 校验
- 已提交 Unity 错误码展示、创建/加入失败回滚及 Steam Lobby 离开清理
- 已提交 Steam 创建者自动触发 `LobbyEntered` 的回调防误判处理
- 已修复测试夹具未隔离 `STEAM_APP_ID` 导致的环境相关 403 误报；定向测试恢复 2/2 通过
- 完整服务端测试 33/33 通过
- 用户已完成真实 Unity Windows Development Build + Steam 联调，BUG-22 验收通过

### [steam-desktop-03-social-lobby] - 2026-08-12

### 策划

- 新增 [Steam好友Lobby邀请与鱼塘映射.md](../../../../planning/specs/desktop/Steam好友Lobby邀请与鱼塘映射.md)（**已确认** / **STEAM-DESKTOP-03**）
- 明确好友、Lobby、邀请、`pondId` 映射和 Node 权限校验边界
- 明确先使用简化版功能 UI，后续可独立替换正式 UI，不重写社交业务逻辑
- 开发提示词：[steam-desktop-social-lobby-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-social-lobby-dev.prompt.md)
- 架构复核：Lobby 关闭/房主离开不得删除或关闭 `pondId`；空鱼塘按 `last_simulated_at` 休眠与补算

### 实现登记

- 完成 Steam 好友、Lobby、邀请、版本校验和 `pondId` 映射实现。
- 明确“离开 Lobby”与“离开鱼塘”分离：Lobby 关闭不删除鱼塘，空鱼塘继续离线积累。
- 按要求跳过双 Steam 账号真实验收；当前记录代表实现完成，不代表双账号联调已通过。

### [unity-p1-p2-network-thin-client] - 2026-08-12

### 验收

- **UNITY-P1 / UNITY-P2：已实现**
- 完成 Unity C# 网络 DTO、Engine.IO/Socket.IO 薄客户端、JWT Socket 鉴权和鱼塘会话控制器。
- 通过真实 Windows Development Build 验证进塘、选钓位、开始/收杆、咬钩、领取鱼获、背包更新、服务端重启和断线重连。
- 本地 `FISHING_TEST_MODE=instant` 已验证快速钓鱼闭环，并补齐空钓位测试鱼，避免快速验收永久停留在 `waiting`。
- Debug 记录与过程：[UNITY-P1-P2-验收与Debug记录-2026-08-12.md](../../../../planning/reports/UNITY-P1-P2-验收与Debug记录-2026-08-12.md)

### [bugfix-desktop-exit-process] - 2026-08-12

### 实现

- 修复 Unity 桌面端关闭按钮被拦截后进程继续驻留的问题
- 增加桌面端单实例互斥和托盘线程/图标退出清理
- 登记 **BUG-21** 并同步计划表与进度看板

### [steam-desktop-05-offline-ecology] - 2026-08-12

### 实现

- 完成空鱼塘休眠、活跃鱼塘实时 Tick 和进塘前事务化唤醒补算
- 增加 `last_simulated_at` 迁移、成长绝对时间推进、迁移/补充幂等保护和大跨度压缩日志
- 新增离线生态测试；服务端 28 项测试与 `verify:engineering` 全部通过

### 策划

- 新增 [空鱼塘休眠与生态离线补算.md](../../../../planning/specs/gameplay/空鱼塘休眠与生态离线补算.md)（**已确认** / **STEAM-DESKTOP-05**）
- 明确活跃鱼塘实时 Tick、空鱼塘休眠、成长/迁移/补充补算、事务幂等和大跨度性能保护
- 开发提示词：[steam-desktop-pond-offline-ecology-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-pond-offline-ecology-dev.prompt.md)

### [steam-desktop-02-auth] - 2026-08-12

### 策划

- 新增 [Steam身份账号绑定与安全会话.md](../../../../planning/specs/desktop/Steam身份账号绑定与安全会话.md)（**已确认** / **STEAM-DESKTOP-02**）
- 明确 Steam Ticket 验证、`SteamID64 ↔ playerId`、JWT、账号新档和安全审计边界
- 列出真实 Steam 验收所需的 App ID、测试账号、SDK/插件、Web API Key 和测试环境参数
- 明确商店页面不是前置；Steamworks App、AppID、测试权限、Windows Depot/Build 和服务端 Publisher Web API Key 才是实际联调条件
- 固定调用链：`GetAuthTicketForWebApi` → HTTPS → `AuthenticateUserTicket` → `SteamID64` → 项目 JWT
- 开发提示词：[steam-desktop-account-auth-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-account-auth-dev.prompt.md)

### 真实联调验收

- 使用已配置的 Steam AppID、服务端 Publisher Web API Key 和测试账号完成真实登录。
- 首次登录创建 Steam 玩家档案，重复登录复用原 `playerId`。
- JWT REST、Socket 鉴权、进塘、钓鱼、鱼获、背包更新和断线重连均验证通过。
- **STEAM-DESKTOP-02：已实现**。

### [steam-desktop-04-shell] - 2026-08-11

### 策划

- 新增 [Unity Windows桌面端基础壳.md](<../../../../planning/specs/desktop/Unity Windows桌面端基础壳.md>)（**已实现** / **STEAM-DESKTOP-04**）
- 拆分 04A～04F：工程基线、窗口生命周期、托盘后台、主界面占位、设置通知、性能发布验证
- 明确本阶段不接 Steam 登录、Lobby、Networking、Node 实时业务和正式鱼塘玩法
- 开发提示词：[steam-desktop-shell-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-shell-dev.prompt.md)

### 实现（04A～04F）

- `fish-social-unity/`：DesktopMain、窗口管理、Win32 托盘、运行时 UGUI 四入口、通知偏好与模拟事件
- 关闭进托盘；隐藏降帧；构建菜单 `Fish Social → Build Windows Development Player`
- 冒烟清单：`fish-social-unity/Docs/STEAM-DESKTOP-04-smoke.md`（2026-08-12 全部 PASS）

### 调试收口记录

- 窗口模式：修复普通/无边框/全屏来回切换混乱；统一由 Win32 控制窗口样式和尺寸，普通窗口恢复保存尺寸并允许手动调整。
- 全屏/无边框：全屏铺满显示器，无边框铺满工作区并保留任务栏；重复点击同一模式不重复应用。
- 托盘：修复关闭进托盘、托盘显示恢复、真正退出和托盘线程消息窗口生命周期。
- 通知：修复总开关、免打扰、单项通知即时生效；关闭通知或对应类型后模拟事件完全静默；启用时只显示单条提示。
- 后台：托盘隐藏后使用低帧率运行，保留后续真实会话生命周期接口。

### [steam-desktop-01-review] - 2026-08-11

### 策划

- 新增 [Steam桌面端产品定位与信息架构.md](../../../../planning/specs/desktop/Steam桌面端产品定位与信息架构.md)（**评审中** / **STEAM-DESKTOP-01**）
- 明确 Steam 桌面端第一阶段主循环：登录→进塘→挂机→收鱼→聊天→最小化
- 整理窗口行为、通知、鱼塘、好友 Lobby、账号安全和数据迁移问题，统一等待用户确认
- 开发提示词：[steam-desktop-product-ia-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-product-ia-dev.prompt.md)
- 用户统一确认窗口生命周期、通知、入口、自动收鱼、手动钓点、Lobby、人数、Windows 范围、Rich Presence 和 Steam 新档策略；STEAM-DESKTOP-01 → **已确认**

### [steam-desktop-transition] - 2026-08-11

### 策划

- 新增 [Steam桌面端独立游戏转型计划.md](../../../../planning/specs/desktop/Steam桌面端独立游戏转型计划.md)（**已确认** / **STEAM-DESKTOP-EPIC**）
- 目标切换为 Steam 独立游戏 + Unity Windows 桌面助手：面向上班族挂机、好友交流和低打扰互动
- 规划 Steam Ticket/SteamID64 账号绑定、好友 Lobby/邀请、Node 权威服务和空鱼塘离线生态补算
- 明确 Unity 桌面壳、Steam 账号契约、服务端生态补算可并行；Steam Networking/Relay 后置评估
- 开发提示词：[steam-desktop-transition-dev.prompt.md](../../../../planning/prompts/desktop/steam-desktop-transition-dev.prompt.md)

### [acceptance ARC/DP-C/OPS] - 2026-08-10

### 验收收口

| 编号 | 状态 | 验证 | 备注 |
|------|------|------|------|
| ARC-06 | **已实现** | `npm run verify:deploy` | Docker CLI 本机缺失 → SKIP，不伪造通过 |
| ARC-07 | **已实现** | `npm run verify:deploy` | SecureStore / JWT / API / Socket auth |
| ARC-08 | **已实现** | `npm run verify:engineering` | gameState facade + import boundary |
| ARC-09 | **已实现** | `npm run verify:engineering` | 无裸 logInfo/Warn；结构化日志 |
| ARC-10 | **已实现** | `npm run verify:engineering` | 限流 / 连接上限 / dev-token localhost |
| ARC-11 | **已实现** | `npm test` + `verify:engineering` + CI | 门禁改为 `isFishingActive` 与 SESSION_TIMER_PHASES 语义对齐 |
| D-L3-02 | **已实现** | `verify:data-platform-dp-c` | live-vs-sim + deviationPct；sim 非长期 fallback |
| D-L3-09 | **已实现** | `verify:data-platform-dp-c` | manifest `live-daily` + 日批样本；warehouse/latest 可访问 |
| OPS-RELEASE-1 | **已实现** | 文档验收 | [发版与热更-单机Runbook.md](../../../../planning/architecture/发版与热更-单机Runbook.md) |

风险：近期线上日报 catches=0（环境无产量）；`warehouse/latest` 的 `dateKey` 可能落后最新 `daily/`；Docker CLI 未装无法做 compose 真启。

### [bug-18 / bug-19 / bug-20] - 2026-08-10

### 策划（本批次）

- 立项 [BUG修复-进塘首帧状态与演示降级.md](../../../../planning/specs/bugs/BUG修复-进塘首帧状态与演示降级.md)（**已实现** / **BUG-18**）
- 立项 [BUG修复-每日额度单一口径重构.md](../../../../planning/specs/bugs/BUG修复-每日额度单一口径重构.md)（**已实现** / **BUG-19**）
- 立项 [BUG修复-进塘与钓鱼剩余展示回归.md](../../../../planning/specs/bugs/BUG修复-进塘与钓鱼剩余展示回归.md)（**已实现** / **BUG-20**）
- 定责链：进塘旧态/演示降级（18）→ 额度字段与 settle 结构冲突（19）→ 19 后插值对消与未选点满额展示（20）
- 开发 prompt：
  - [bugfix-pond-entry-stale-state-dev.prompt.md](../../../../planning/prompts/bugs/bugfix-pond-entry-stale-state-dev.prompt.md)
  - [bugfix-daily-quota-single-source-dev.prompt.md](../../../../planning/prompts/bugs/bugfix-daily-quota-single-source-dev.prompt.md)
  - [bugfix-quota-remaining-display-regression-dev.prompt.md](../../../../planning/prompts/bugs/bugfix-quota-remaining-display-regression-dev.prompt.md)

### 实现（BUG-18）

- 进塘/重连清空旧 users；禁静默 DEMO；快照前额度门禁；收杆 ack 最终额度

### 实现（BUG-19）

- `settleFishingSession`：`checkpoint` 只前移内部 `quotaCheckpointAt`；`finalize` 清空展示锚点
- `enrichPondUser`：派生 `todayFishingBaseMs` / `todayRemainingMs` / 兼容 `todayFishingMs`
- `stop_fishing`：先 finalize 再 stopping；ack 返回 base + remaining
- 客户端：删除反推基线；只读 base/remaining + session 插值
- `npm run verify:bug19-quota` · `verify:fish-daily-shanghai`

### 实现（BUG-20）

- 钓鱼中：开钓冻结 base + 墙钟 elapsed；禁止 session tick 差值对消 remaining
- 未选钓点：`join_pond` ack 额度种子 + snapshot/joined/updated 合并 upsert；闲置取 max
- 底栏：ack 已带额度即可显示；`verify-bug14-daily-remaining` 增补未选点用例

### [bug-18] - 2026-08-10（初稿备注）

### 策划

- 初稿曾标「已确认」；本批次验收后统一为 **已实现**（见上）
- 完整范围：当前上海日额度快照、收杆幂等结算、最终额度 ack、跨日 00:00 重置、连接代际门禁、显式演示开关

### [bug-16 / bug-17] - 2026-07-28

### 策划

- 立项 [BUG修复-断线离塘未结算丢失钓鱼时�?md](../../../../planning/specs/BUG修复-断线离塘未结算丢失钓鱼时�?md)�?*已实�?* / **BUG-16**�?  - 根因：`handleDisconnect` �?`syncStatus` 先清 `fishingStartedAt` 且不 flush；`removeDisconnectedUser` 兜底条件恒不成立；缺分段落账导致「一次写�?8h」与「一秒不记」两�?  - 修复方向：统一 `settleFishingSession`、断线前结算�?0s 分段落账
- 立项 [BUG修复-离塘导航失效与收杆按钮闪�?md](../../../../planning/specs/BUG修复-离塘导航失效与收杆按钮闪�?md)�?*已实�?* / **BUG-17**�?  - 根因：先�?`leave_pond` �?`router.back()`（无历史�?no-op�? `leftPondRef` 一次性闩�?�?死屏「请先加入鱼塘」；相位中间态（`seated` / `stopping` 200ms）直接驱动按�?  - 修复方向：先导航后离�?+ 死态自愈重�?+ 按钮 pending 收敛
- 开�?prompt：[bugfix-session-settlement-dev.prompt.md](../../../../planning/prompts/bugs/bugfix-session-settlement-dev.prompt.md) · [bugfix-leave-pond-navigation-dev.prompt.md](../../../../planning/prompts/bugs/bugfix-leave-pond-navigation-dev.prompt.md)

### 实现（BUG-16�?
- `settleFishingSession`：sanitize �?safeElapsed �?addToday �?finalize 清空 / advance 前移锚点；结构化日志 `fishing_session_settled`
- `handleDisconnect`：转 `disconnected` **之前** finalize
- `removeDisconnectedUser` / leave / take_spot / stop：统一结算（幂等）
- `syncHumanQuotaAndEmit`：在�?30s 分段落账
- `resumeAfterReconnect`：活跃相位锚点取 `now`（断线期不计�?- `npm run verify:fish-daily-shanghai` 通过（断线结�?/ 幂等 / 分段�?
### 实现（BUG-17�?
- `handleLeaveToMap`：`canGoBack` �?`back`，否�?`replace('/')`；`requestLeaveOnUnmount` 在卸�?cleanup �?`leave_pond`
- `rejoinPond`：复位闩锁；`me` 丢失 / 「请先加入鱼塘」时自愈
- 底栏：`开钓中…` / `收杆中…`�?s 超时），避免相位闪烁
- 社交切页仍不 leave（BUG-06）；`verify:pond-navigation` 已更�?
### [fish-spot-1] - 2026-07-28

### 策划

- 立项 [钓点手动选择.md](../../../../planning/specs/gameplay/钓点手动选择.md)�?*已确�?* / **FISH-SPOT-1**�?- 取消「开始钓鱼」自�?`freeSpot` 占点；场景点选落座后再开钓；建议拆分 `take_spot` / `start_fishing`
- 开�?prompt：[fish-spot-manual-select-dev.prompt.md](../../../../planning/prompts/gameplay/fish-spot-manual-select-dev.prompt.md)

### [bug-15] - 2026-07-27

### 策划

- 立项 [BUG修复-今日额度跨日不刷�?md](../../../../planning/specs/BUG修复-今日额度跨日不刷�?md)�?*已实�?* / **BUG-15**�?- 根因：内�?`todayFishingMs` �?`daily_fishing` 脱节；闲置跨日无 enrich/推送；写入缺封顶（历史 >8h / 1970-01-01�?- 修复方向：未在钓强制对齐 DB；日�?emit；`addTodayFishingMs` 封顶与非法锚点防�?- 开�?prompt：[bugfix-daily-quota-day-rollover-dev.prompt.md](../../../../planning/prompts/bugs/bugfix-daily-quota-day-rollover-dev.prompt.md)

### 实现

- `pondUserManager`：同日闲置读库对�?· `sanitizeFishingStartedAt` · `addFishingMsForDate` 封顶 · `syncHumanQuotaAndEmit`
- `serverLoops`�?0s 扫描闲置/跨日推送额�?- `verify-fish-daily-shanghai-rollover` 扩展脏内存纠�?/ 闲置跨日 / 写入封顶
- `npm run verify:fish-daily-shanghai` 通过
- **2026-07-28 回归**：禁�?sanitize 夹成 now�?h 后入账；`safeFishingElapsedMs`；leave/flush/disconnect 统一（坏锚点不写满日�?
### [bug-14] - 2026-07-27

### 策划

- 立项 [BUG修复-今日剩余时长不刷�?md](../../../../planning/specs/BUG修复-今日剩余时长不刷�?md)�?*已实�?* / **BUG-14**�?- 根因：底栏只�?`todayFishingMs`，tick 不更新今日；修复：开�?baseline + 本局 elapsed 插�?- 开�?prompt：[bugfix-daily-remaining-refresh-dev.prompt.md](../../../../planning/prompts/bugs/bugfix-daily-remaining-refresh-dev.prompt.md)

### 实现

- `mobile/lib/fishingDuration.ts`：开钓基�?+ 本局 elapsed，避�?enrich 双计
- `pond/[id].tsx`：钓鱼中 250ms 刷新底栏「今日剩余」；未钓鱼仍�?`todayFishingMs`
- 不改 `session_timer_tick` / BUG-13 头顶秒表语义

### [feat-scene-tile-4] - 2026-07-27

### 策划

- 立项 [世界地图全屏与层级缩�?md](../../../../planning/specs/世界地图全屏与层级缩�?md)�?*已实�?* / **FEAT-SCENE-TILE-4**�?- 地图全屏；顶�?> HUD > 场景；修缩放与画�?UI 坐标不一�?- 开�?prompt：[scene-map-fullscreen-layers-dev.prompt.md](../../../../planning/prompts/world/scene-map-fullscreen-layers-dev.prompt.md)

### 实现

- `index`：L0 顶栏置顶；其�?`MapStage` flex:1 全屏；去掉底栏占�?- `WorldMapView`：L2 场景 + L1 HUD absoluteFill；删叠字大标题；角标提示
- `TileCameraView`：滚�?捏合/点击统一用视口客户区坐标（`measureInWindow`）；`transformOrigin: 0 0`；全屏后重算 panable
- `PondScene`：同�?L1/L2 分层（可选项一并落地）

### [feat-scene-tile-3] - 2026-07-27

### 策划

- 立项 [Tilemap性能与二十塘扩容.md](../../../../planning/specs/world/Tilemap性能与二十塘扩容.md)�?*已实�?* / **FEAT-SCENE-TILE-3**�?- 低节点绘�?+ 去世界钓点图�?+ 更大地图 + 20 个非矩形塘位
- 开�?prompt：[scene-tile-perf-20ponds-dev.prompt.md](../../../../planning/prompts/world/scene-tile-perf-20ponds-dev.prompt.md)

### 实现

- `MergedTileLayer`（SVG 合并色块�? `tileMerge`；世�?鱼塘不再每格 View/Pressable
- `TileCameraView`：rAF 节流 `onViewChange` + `onTapWorld` 单点命中
- `worldTileMap`：边�?**144**�?0 多矩形不规则足迹；世界层�?🎣 图标
- `pondCatalog` / `PONDS` / 生态库存模板扩�?20 塘；塘内网格 28、水面多矩形、每�?20 spot
- bot 默认 `BOT_BOOT` 1�?、`BOT_SPAWN_CHANCE` 0.2，缓�?20 塘压力；`verify:server-boot` 通过

### [feat-scene-tile-2] - 2026-07-27

### 策划

- 立项 [Tilemap相机拖拽与HUD分层.md](../../../../planning/specs/world/Tilemap相机拖拽与HUD分层.md)�?*已实�?* / **FEAT-SCENE-TILE-2**�?- 修塘内拖拽锁死、去 dock、扩世界、整塘热区、Scene/HUD 分层与动态字�?- 开�?prompt：[scene-tile-camera-hud-dev.prompt.md](../../../../planning/prompts/world/scene-tile-camera-hud-dev.prompt.md)

### 实现

- `TileCameraView`：`panable` 初始缩放 + 内容≤视�?overscroll；Pan 与点击错开
- `PondScene`：移�?dock；状态字提到相机�?HUD
- `worldTileMap`：边�?96，四塘拉开；`WorldMapView` 整塘热区 + HUD 塘名/人数

### [feat-scene-tile-1] - 2026-07-27

### 策划

- 立项 [星露谷式正交Tilemap场景.md](../../../../planning/specs/world/星露谷式正交Tilemap场景.md)�?*已实�?* / **FEAT-SCENE-TILE-1**�?- 纠偏 ISO-1/2：正交密铺硬边方格，禁圆角扁矩形与椭圆稀疏大�?- 开�?prompt：[scene-ortho-tilemap-dev.prompt.md](../../../../planning/prompts/world/scene-ortho-tilemap-dev.prompt.md)

### 实现

- `shared/tileMath` · `pondTileMap` · `worldTileMap`�?4×64 稠密矩形陆地/水面�?- `TileLayer`（`borderRadius: 0`）�?`TileCameraView`（直角视口）
- `PondScene` / `WorldMapView` 改正交密铺；钓位仍为 `*-spot-1..20`

### [ops-release-1] - 2026-07-27

### 策划

- 立项 [发版与热更策�?md](../../../../planning/specs/发版与热更策�?md)�?*已确�?* / **OPS-RELEASE-1**�?- 拆分配置热更 / 服务端发�?/ 客户�?OTA；本迭代交付单机 runbook，不�?OTA
- 开�?prompt：[ops-release-hotupdate-dev.prompt.md](../../../../planning/prompts/data-ops/ops-release-hotupdate-dev.prompt.md)

### [feat-scene-iso] - 2026-07-27

### 策划

- 立项 [等距网格世界与鱼塘场�?md](../../../../planning/specs/等距网格世界与鱼塘场�?md)�?*已实�?* / **FEAT-SCENE-ISO-1** · **FEAT-SCENE-ISO-2**�?- RN 等距色块：先塘内网格+相机，再世界陆地板块与进塘入口格；暂�?Unity
- 开�?prompt：[scene-iso-grid-dev.prompt.md](../../../../planning/prompts/world/scene-iso-grid-dev.prompt.md)

### 实现

- `shared/isoMath` · `isoPondGrids` · `isoWorldGrid`；钓�?id 仍为 `*-spot-1..20`
- `IsoCameraView`：拖拽平�?+ 滚轮/捏合缩放
- `PondScene`：等距色块水�?+ y 排序角色 + �?Overlay 气泡/飘字
- `WorldMapView`：四陆地板块、入口格弹窗进塘、视�?culling

### [unity-p0] - 2026-07-26

### 策划

- **UNITY-P0 已实�?*：[`Unity迁移决策记录.md`](../../../../planning/architecture/Unity迁移决策记录.md) + [`Unity契约冻结清单-v0.md`](../../../../planning/architecture/Unity契约冻结清单-v0.md)
- 策划进度看板改为双页签：**完成千人运营目标** / **Unity 版本开�?*（`build-producer-progress-html.py`�?- 计划�?UNITY-P0 �?已实现（设计/完成 2026-07-26�?
### [unity-epic] - 2026-07-26

### 策划

- �?[REF-UNITY-1](../../../../planning/product/Unity移植工程路径蓝图.md) 拆分 [Unity移植-分阶段需求清�?md](../../../../planning/specs/Unity移植-分阶段需求清�?md)
- 立项 **UNITY-EPIC** + **UNITY-P0～P5**；P0 已确认可开工，P1～P5 / EPIC 已定稿排�?- 开�?prompt：[unity-p0-decision-contract-dev.prompt.md](../../../../planning/prompts/desktop/unity-p0-decision-contract-dev.prompt.md)

### [ref-unity-1] - 2026-07-26

### 策划

- 新增参考文�?[Unity移植工程路径蓝图.md](../../../../planning/product/Unity移植工程路径蓝图.md)�?*已文档化** / **REF-UNITY-1**�?- 架构事实：mobile/server/shared 切线；推�?Unity 2D 客户�?+ Node 权威服；Phase 0�? 工程路径
- 关联 [REF-SCENE-1](../../../../planning/product/钓鱼世界与鱼塘场景优化策�?md) 场景体验目标

### [ref-scene-1] - 2026-07-26

### 策划

- 新增参考文�?[钓鱼世界与鱼塘场景优化策�?md](../../../../planning/product/钓鱼世界与鱼塘场景优化策�?md)�?*已文档化** / **REF-SCENE-1**�?- 批判钓鱼世界 / 鱼塘现状；P0 可读 · P1 世界入口 · P2 角色钓点�?*非开发需�?*，后�?FEAT 另立�?
### [feat-ui-2] - 2026-07-26

### 策划

- 新增 [鱼塘叠加层与背包社交收口.md](../../../../planning/specs/gameplay/鱼塘叠加层与背包社交收口.md)�?*已实�?* / **FEAT-UI-2** / P0�?- 飘字/气泡统一高层；排行榜�?inventory（含 bot）；bot 仅史�?发动态；背包至少 80 格纵�?- 提示词：[pond-overlay-backpack-social-dev.prompt.md](../../../../planning/prompts/desktop/pond-overlay-backpack-social-dev.prompt.md)

### 实现

- `PondScene` Overlay：全�?`FishingFloatText` + 状态气�?自己徽章提到最高层；角色仅保留交互与上钩圆�?- `leaderboard.ts` �?周榜�?`inventory` 最�?`size_m`（缓�?key `*_inv`）；�?bot
- `bots.ts` 删除 `BOT_SHARE_CATCH_CHANCE` 普通分享分�?- `BackpackModal`：`slotCount = max(80, items.length)` 纵向滚动
- `verify-social-v060` 改为 inventory 口径并断言 bot 可上�?
### [feat-ui-1] - 2026-07-25

### 策划

- 新增 [鱼塘场景与社交列表UI优化.md](../../../../planning/specs/world/鱼塘场景与社交列表UI优化.md)�?*已实�?* / **FEAT-UI-1** / P0�?- 状态默认仅自己 · 悬停气泡 · 两档 icon · 抛竿飘字 · 上钩圆环 · 统一弹窗 · 榜含 bot · 领奖�?�?0恒显 · 在线纵滚竖屏 · 评论区修�?- 提示词：[pond-social-ui-optimize-dev.prompt.md](../../../../planning/prompts/gameplay/pond-social-ui-optimize-dev.prompt.md)

### 实现

- `PondCharacter`：仅自己常驻状态；他人 Web 悬停 / 点按气泡；上�?`HookCountdownRing`；去掉整身变色主反馈
- `fishing_float_text` kind=`cast` + `emitFishingCastFloatText`（进�?casting 广播�?- `AppNoticeModal`：塘内分�?饵尽等替换系�?Alert；`CatchFishModal` 仍作获鱼母版
- `leaderboard.ts` 去掉 bot 过滤；`leaderboard.tsx` 领奖台三�?+ 4�?0 空位恒显
- `PondSocialPanel` 纵向列表；竖屏在�?聊天 Tab
- `PostCard` 评论区可�?+ 错误重试 + overflow 修复

### [ops-catch-1.1] - 2026-07-19

### 策划

- 新增 [Admin与业务健康产量对齐背�?md](../../../../planning/specs/Admin与业务健康产量对齐背�?md)�?*已实�?* / **OPS-CATCH-1.1** / P0�?- Admin 玩家钓获、业务健�?/ `daily_*` catch �?OPS-CATCH-1 一致（inventory，含 bot�?- 提示词：[admin-business-health-catch-inventory-dev.prompt.md](../../../../planning/prompts/data-ops/admin-business-health-catch-inventory-dev.prompt.md)

### 实现

- `adminPlayersOverview.catchCount` �?窗口�?`inventory`（含 bot�?- `aggregate-daily-metrics`：`catch_count` �?inventory 分玩�?分塘；hook/escape �?metrics
- `businessHealth`：上海日 `dateKey`；产量含 bot；活跃人数排�?bot；`verify:ops-catch-inventory-admin`

### [fish-bot-2] - 2026-07-15

### 策划

- 新增 [机器人进塘节奏与初始时长.md](../../../../planning/specs/gameplay/机器人进塘节奏与初始时长.md)�?*FISH-BOT-2** · **已实�?*）：启动 3�? · 已钓时长随机 · 慢补至满�?- 开发交接：[机器人进塘节奏与初始时长-开发交�?md](../../../../planning/specs/机器人进塘节奏与初始时长-开发交�?md)
- 提示词：[bot-spawn-pace-dev.prompt.md](../../../../planning/prompts/gameplay/bot-spawn-pace-dev.prompt.md)

### 实现

- `bootstrapBots`：每�?uniform(3,6)；~75% 开�?+ 5�?5min `fishingStartedAt` 回拨（不预写 `daily_fishing`�?- `tickSpawn`：按 `BOT_SPAWN_CHANCE` 每塘最�?+1，可慢补�?`MAX_BOTS_PER_POND`；无 `BOT_SOFT_CAP`
- `startBotFishing(..., { elapsedMs })`；`verify:fish-bot-spawn-pace`

### [bug-13] - 2026-07-15

### 策划

- 新增 [BUG修复-垂钓中头�?秒闪�?md](../../../../planning/specs/BUG修复-垂钓中头�?秒闪�?md)�?*已确�?* / **BUG-13** / P0�?- 根因：整替换冲秒表、Bot 广播�?enrich、PERF-03b 省略 `fishingStartedAt`、客户端插值与闸门不一�?- 提示词：[bugfix-session-timer-zero-flash-dev.prompt.md](../../../../planning/prompts/bugs/bugfix-session-timer-zero-flash-dev.prompt.md)

### [fish-bot-1] - 2026-07-15

### 策划

- 新增 [机器人账号池与清�?md](../../../../planning/specs/机器人账号池与清�?md)�?*已实�?* / **FISH-BOT-1** / P0�?- 清历�?bot 明细；固�?100 池复用进塘；扩名；看板历史不回写
- 补充：允�?bot 填满鱼塘（默认每塘上�?容量 20�?- 提示词：[bot-pool-reuse-cleanup-dev.prompt.md](../../../../planning/prompts/gameplay/bot-pool-reuse-cleanup-dev.prompt.md)

### 实现

- `ensureBotPool(100)` + `enterPondFromPool`（禁 UUID 新建）；`BOT_NAMES` 105；`MAX_BOTS_PER_POND` 默认 20
- `npm run ops:cleanup-bot-pool`（可 `--dry-run`）；`verify:fish-bot-pool`

### [fish-daily-1] - 2026-07-15

### 策划

- 新增 [每日钓鱼时长-上海日重�?md](../../../../planning/specs/每日钓鱼时长-上海日重�?md)�?*已实�?* / **FISH-DAILY-1** / P0�?- 8h 额度按上�?0 点换日；跨日仍在钓只刷新剩余时长、不重置钓鱼状�?- 提示词：[daily-fishing-shanghai-rollover-dev.prompt.md](../../../../planning/prompts/gameplay/daily-fishing-shanghai-rollover-dev.prompt.md)

### 实现

- `todayKey`→Asia/Shanghai；`ensureFishingDayRollover`（跨�?flush 旧日+重锚，不改相�?钓位）；`verify:fish-daily-shanghai`

### [ops-catch-1] - 2026-07-15

### 策划

- 新增 [看板产量人机分列-背包口径.md](../../../../planning/specs/data-ops/看板产量人机分列-背包口径.md)�?*已实�?* / **OPS-CATCH-1** / P0�?- 日钓获权威口径改�?`inventory` 入库；看板展示总量 + 真人 + 机器�?- 提示词：[ops-catch-inventory-human-bot-dev.prompt.md](../../../../planning/prompts/data-ops/ops-catch-inventory-human-bot-dev.prompt.md)

### 实现

- summary/compact/日报/增长看板/今日运维：总量+人机分列；告警与目标对照用人人；`verify:daily-ops-report`

### [feat-soc-03b-leaderboard-podium] - 2026-07-14

### 策划

- 新增 [排行�?入口与领奖台改版.md](../../../../planning/specs/排行�?入口与领奖台改版.md)�?*已确�?* / **FEAT-SOC-03b**�?- 首页入口 · 独立�?· 领奖�?· 仅每�?每周 · 删稀�?UI · 周榜改为「本周最大鱼�?- 提示词：[leaderboard-podium-home-dev.prompt.md](../../../../planning/prompts/gameplay/leaderboard-podium-home-dev.prompt.md)

### [admin-obs-1.4] - 2026-07-14

### 策划

- 新增 [Admin页签合并与看板图表化.md](../../../../planning/specs/data-ops/Admin页签合并与看板图表化.md)�?*已实�?* / **ADMIN-OBS-1.4** / P0�?- Timeline+Live→玩家详情；Fishing Debug→鱼塘；玩家列表中文�?钓位；业务健康折线；删运营平台「服务探活�?- 提示词：[admin-tabs-merge-charts-dev.prompt.md](../../../../planning/prompts/data-ops/admin-tabs-merge-charts-dev.prompt.md)

### 实现

- Admin 四页签；`PlayerDetailPage` / 合并 `PondsPage`；overview `pondName`/`spotName`；业务健�?Chart.js；运营平台今日条 RSS/真人/机器人；`verify:ops-portal-links`

### [ops-kpi-1] - 2026-07-14

### 策划

- 新增 [运营增长与商业化指标看板.md](../../../../planning/specs/data-ops/运营增长与商业化指标看板.md)�?*已实�?* / **OPS-KPI-1**�?- 新进/DAU/粘性；留存 D1/D3/D7/D10/D14/D30 矩阵+可筛折线；商业化 CPI/ARPU/eCPM 等灰态预留；并补充社�?漏斗/经济/生态等词典
- 提示词：[ops-growth-kpi-dashboard-dev.prompt.md](../../../../planning/prompts/data-ops/ops-growth-kpi-dashboard-dev.prompt.md)

### 实现

- `docs/analytics/growth/{series,retention,commercial,index}`；`npm run analytics:growth`；日批钩子；运营平台「增长与经营看板」；`verify:ops-portal-links` 覆盖

### [admin-obs-1.3] - 2026-07-14

### 策划

- 新增 [Admin可读化与运营入口精简.md](../../../../planning/specs/data-ops/Admin可读化与运营入口精简.md)�?*已实�?* / **ADMIN-OBS-1.3** / P0�?- 范围：运营平台实时区只留 Admin 内嵌；Admin JSON→表格；默认全员（真人）一�?筛�?精查；bot 默认不停写相位埋�?- 提示词：[admin-readable-ops-slim-dev.prompt.md](../../../../planning/prompts/data-ops/admin-readable-ops-slim-dev.prompt.md)

### [ops-ux-1] - 2026-07-14

### 策划

- 新增 [运营平台入口体验增强.md](../../../../planning/specs/data-ops/运营平台入口体验增强.md)�?*已实�?* / **OPS-UX-1** / P0�?- 范围：今日运维条（昨日日报深�?· KPI · 告警 · 日批状�?· 服务摘要）→ 每日必看 3 �?+ 工程区折�?�?Admin `?tab=&playerId=` 深链 �?抛光
- 提示词：[ops-portal-ux-dev.prompt.md](../../../../planning/prompts/data-ops/ops-portal-ux-dev.prompt.md)
- 契约：`docs/analytics/daily-batch-status.json` · compact `alertCount*` · 报告 `#alerts`

### 实现

- `运营平台.html` 今日运维�?+ 分区折叠；`daily-pipeline` / `run-daily-analytics.ps1` 写状态；Admin 深链；`verify:ops-portal-links` 扩展

### [v0.6.0-frontend-social] - 2026-07-13

### 实现（前端）

- **FEAT-SOC-01/02/03** 客户端：`PostCard` 操作�?❤️/💬（乐观点赞、评论区展开/发�?P1 删除�?- `LeaderboardPanel` + 社交 Tab「排行榜」（子榜 + 前三 + 我的排名 + 点行开资料�?- `socialApi` 点赞/评论/排行榜；Socket `post_liked` / `post_commented` / `post_comment_deleted`
- `shared`：`SocialPost` 扩展字段、`PostComment`、`LeaderboardEntry`、Socket 事件类型
- **依赖**：后�?API（`v0.6.0-backend-dev.prompt.md`）尚未落地前，写接口会失�?
### [feat-soc-01-02-03-spec-refresh] - 2026-07-13

### 策划

- 修订 [社交互动-动态点�?md](../../../../planning/specs/社交互动-动态点�?md)�?*FEAT-SOC-01**）：JWT 鉴权、可见性、`likedByMe`、隐私导�?- 修订 [社交互动-动态评�?md](../../../../planning/specs/社交互动-动态评�?md)�?*FEAT-SOC-02**）：删除权限与非目标对齐、防刷、Socket 删评
- 修订 [排行榜系�?md](../../../../planning/specs/排行榜系�?md)�?*FEAT-SOC-03**）：**metrics 收鱼事件为权威源**（禁止只�?inventory）、Bot 不上榜、卖鱼仍计分、上海日�?- 同步开发提示词与计划表编号 FEAT-SOC-01/02/03

### [d-l2-16-phase-compact-impl] - 2026-07-13

### 实现

- **D-L2-16** �?**已实�?*：`shared/phaseCodes.ts`�?�?）�?metrics payload �?`{f,t,c}` · `phase_transition_invalid` 同步 · 可�?`METRICS_SKIP_BOTS` · schema + 埋点�?xlsx

### [obs-log-1-impl] - 2026-07-13

### 实现

- **OBS-LOG-1** �?**已实�?*：生�?fanout 默认�?Prometheus（`FANOUT_LOG_INFO=1` 可恢复）· perf info 慢阈�?· `ECOLOGY_VERBOSE` · tap 忽略 `session_timer_tick`/`pond_ecology_updated` · reconnect 校验合并顶层 `playerId`/`pondId`

### [obs-log-1-noise] - 2026-07-13

### 策划

- 新增 [服务端日志降噪与分层输出.md](../../../../planning/specs/backend/服务端日志降噪与分层输出.md)�?*已确�?* / **OBS-LOG-1**�?- 提示词：[obs-log-noise-reduction-dev.prompt.md](../../../../planning/prompts/data-ops/obs-log-noise-reduction-dev.prompt.md)
- 默认安静：fanout / perf info / 补鱼 console / tap 高频；修 reconnect metrics-validation 假告�?
### [perf-03b-session-timer-slim] - 2026-07-13

### 实现

- **PERF-03b** �?**已实�?*：`session_timer_tick` 仅广�?`userId` + `sessionFishingMs`（去掉重复的 phase/startedAt�?- Spec：[会话计时tick仅必要字�?md](../../../../planning/specs/会话计时tick仅必要字�?md)；验�?`verify:session-timer-broadcast`
- 后续可选：按塘批打包（方案 B）、降频（方案 C�?
### [d-l2-16-phase-compact] - 2026-07-13

### 策划

- 新增 [埋点优化-相位跳转短码.md](../../../../planning/specs/data-ops/埋点优化-相位跳转短码.md)�?*已确�?* / **D-L2-16**�?- 开发提示词：[metrics-phase-transition-compact-dev.prompt.md](../../../../planning/prompts/data-ops/metrics-phase-transition-compact-dev.prompt.md)
- 新增 Skill：`.cursor/skills/metrics-catalog-sync/SKILL.md`（需求开发须同步埋点表）
- `planning-progress-sync` / `AGENTS.md` 已交叉引用；`npm run planning:metrics-xlsx` 已更�?transition 行契�?
### [be-opt-d-quality] - 2026-07-12

### 实现

- **BE-OPT-D** �?**已实�?*：QUAL-01�?0
  - D1：vitest `auth` / `sessionRegistry` / `humanCapacity`
  - D2：PG 多行 `insertBatch` + pending drain；`METRICS_READ_FROM=postgres` 启动拒绝
  - D3：`docs/ops/server-env.md` · compose 禁默�?`*` · Dockerfile Node20/tsx 债注�?· 双容量说�?  - D4：生产默认日志掩�?· `OPS_STATIC_ENABLED`
  - D5：`adminEcologyRoutes` 拆分 · fishing-debug cache 上限
- **已知债（QUAL-06�?*：Docker runner 仍用 `tsx` �?TS，未 emit server dist
- **BE-OPT-00** �?**已实�?*（A～D 收口；E 千人另立项）

### [be-opt-c-stability] - 2026-07-12

### 实现

- **BE-OPT-C** �?**已实�?*：STAB-01�?6（session flush · await OTEL/PG · fatal→shutdown · socket 限流 · Admin REST �?query key / SSE 例外 · `/health` draining）；`verify:backend-opt-c`

### [backend-opt-c-d-kickoff] - 2026-07-12

### 策划

- **BE-OPT-B** 已实现后，细化下一批并 Kickoff�?  - 新增 [后端优化-Kickoff-C与D.md](../../../../planning/specs/backend/后端优化-Kickoff-C与D.md)
  - 更新 [后端优化-C-停机与稳定�?md](../../../../planning/specs/后端优化-C-停机与稳定�?md)（代码锚�?· SSE query 例外 · verify 建议�?  - 更新 [后端优化-D-工程债与部署.md](../../../../planning/specs/backend/后端优化-D-工程债与部署.md)（PR 切片 D1～D5�?  - 提示词：强化 `backend-opt-c-stability-dev` · `backend-opt-d-quality-dev`；新�?`backend-opt-d1-vitest-dev` · `backend-opt-d2-pg-dev`
- 当时下一刀�?BE-OPT-C（现已实现）

### [be-opt-b-perf] - 2026-07-12

### 实现

- **BE-OPT-B** �?**已实�?*：PERF-01�?5（生�?`getPondEcologySummary` emit · 按塘生态事�?· `session_timer_tick` + 客户端合�?· dirty �?drain · spot weight 内存缓存）；`verify:session-timer-broadcast`

### [be-opt-a-security] - 2026-07-12

### 实现

- **BE-OPT-A** �?**已实�?*：SEC-01�?6（注册禁冒充、requireSelf 读鉴权、DM、client-logs�?0、pepper 生产硬失败）；`verify:backend-opt-a`

### [backend-opt-batches-design] - 2026-07-12

### 策划

- 新增后端再排查分批需求（**已确�?*，设计时�?2026-07-12）：
  - [后端优化-问题汇总与分批计划.md](../../../../planning/specs/backend/后端优化-问题汇总与分批计划.md)�?*BE-OPT-00**�?  - [后端优化-A-安全收口.md](../../../../planning/specs/backend/后端优化-A-安全收口.md)�?*BE-OPT-A** · P0�?  - [后端优化-B-热路径性能.md](../../../../planning/specs/backend/后端优化-B-热路径性能.md)�?*BE-OPT-B** · P1�?  - [后端优化-C-停机与稳定�?md](../../../../planning/specs/后端优化-C-停机与稳定�?md)�?*BE-OPT-C** · P1�?  - [后端优化-D-工程债与部署.md](../../../../planning/specs/backend/后端优化-D-工程债与部署.md)�?*BE-OPT-D** · P2�?  - [后端优化-E-千人多机前置.md](../../../../planning/specs/backend/后端优化-E-千人多机前置.md)�?*BE-OPT-E** · **已定�?*未排期）
- 开发提示词：`backend-opt-a-security-dev.prompt.md` · `backend-opt-b-perf-dev.prompt.md` · `backend-opt-c-stability-dev.prompt.md` · `backend-opt-d-quality-dev.prompt.md`
- 执行顺序�?*A �?B �?C �?D**；与 FEAT-05 玩法轨错开；E 另立�?
### [admin-obs-1.2-r2-3-impl] - 2026-07-12

### 实现

- **ADMIN-OBS-1.2 P1** �?**已实�?*：SOP Timeline 着�?· 双端 activeFishers · 仅真人筛�?· Inspector 强化
- **R2-3 I1–I4** �?**已实�?*：`MAX_HUMAN_SOCKETS` · join 软拒�?· `/ready` capacity · `capacity_reject` · `verify:capacity-limit`

### [admin-obs-1.2-and-r2-3-design] - 2026-07-12

### 策划

- **ADMIN-OBS-1.1** �?**已实�?*（`verify:admin-observability` 通过；设�?完成时间 2026-07-12�?- 新增 [Admin-排障增强-v1.2.md](../../../../planning/specs/data-ops/Admin-排障增强-v1.2.md)�?*ADMIN-OBS-1.2** · **已确�?*）：P1 Timeline/双端 Debug/人机分列；P2 诊断包等
- 新增 [架构-单实例容量与真人隔离-R2-3.md](../../../../planning/specs/backend/架构-单实例容量与真人隔离-R2-3.md)�?*R2-3** · **已确�?*）：替代路线图三条摘要；澄清「真实玩家隔离�? 容量+人机分账
- 开发提示词：[`admin-observability-v1.2-dev.prompt.md`](../../../../planning/prompts/data-ops/admin-observability-v1.2-dev.prompt.md) · [`架构-R2-3-容量与真人隔�?dev.prompt.md`](../../../../planning/prompts/架构-R2-3-容量与真人隔�?dev.prompt.md)

### [d-l2-15-hook-escape-counters] - 2026-07-12

### 实现

- **D-L2-15** �?**已实�?*：默认停�?`bite_tick_*`；`bite_hook`/`escape` 带会话累计；日报改上钩频�?脱钩�?获鱼率；`METRICS_BITE_TICK_PERSIST`

### 策划

- 新增 [埋点优化-咬钩脱钩计数替代tick.md](../../../../planning/specs/data-ops/埋点优化-咬钩脱钩计数替代tick.md)�?*已实�?* / **D-L2-15**�?- 开发提示词：[metrics-hook-escape-counters-dev.prompt.md](../../../../planning/prompts/data-ops/metrics-hook-escape-counters-dev.prompt.md)
- 运营日报 §5.4 咬钩命中率口径改为上�?脱钩/获鱼（废�?tick hit/miss�?
### [bug-12-bi-web-portal] - 2026-07-12

### 实现

- **BUG-12** �?**已实�?*：warehouse `latest/index.html`；运营平�?:8082 探活；`打开运营平台.bat` 默认可拉 Web；`verify:ops-portal-links`

### [data-platform-dp-d-handoff] - 2026-07-12

### 策划

- 新增 [数据平台-DP-D-BI与合规交�?md](../../../../planning/specs/数据平台-DP-D-BI与合规交�?md)�?*已确�?*）：D-L3-10 �?D-L3-06
- 开发提示词：[data-platform-dp-d-dev.prompt.md](../../../../planning/prompts/data-ops/data-platform-dp-d-dev.prompt.md)
- 数据平台 Phase2 开放项收窄为这两项（另：FEAT-05 玩法轨仍为已确认�?
### [daily-ops-remaining-handoff] - 2026-07-12

### 策划

- 新增 [运营日报-剩余需求交�?md](../../../../planning/specs/运营日报-剩余需求交�?md)�?*已确�?*）：D-L3-03/04/05/07/08 + §4/5/6 缺口
- 开发提示词：[daily-ops-report-remaining-dev.prompt.md](../../../../planning/prompts/data-ops/daily-ops-report-remaining-dev.prompt.md)
- 顺序 R1（生态→经济）→ R2（留存）�?R3（版�?Webhook）；BI/合规仍属 DP-D

### [admin-observability-gap-design] - 2026-07-12

### 策划

- 新增 [Admin-能力不足分析与补充方�?md](../../../../planning/specs/Admin-能力不足分析与补充方�?md)�?*已确认（设计�?*�?- 覆盖：Live Inspector 非内存态、缺 `fishingStartedAt`、无进程重启埋点、admin-web 字段错位；MVP=live-state API + Inspector 重写 + Debug 补字�?- 开发提示词：[admin-observability-v1.1-dev.prompt.md](../../../../planning/prompts/data-ops/admin-observability-v1.1-dev.prompt.md)（含并行 BUG：checkpoint 补锚点）

### [phase2-next-iter-n1-dpc-c1] - 2026-07-12

### 策划

- **ARC-09/10/11** · **BUG-08** �?**已实�?*�?026-07-12�?- **D-L3-02** · **D-L3-09**（DP-C1）→ **已实�?*
- FEAT-05 子任�?**C1+C7** 阶段性交付（整包 FEAT-05 仍为已确认）

### 实现

- ARC-09/10/11 · BUG-08 · DP-C live-vs-sim · C1/C7 灰度看板与热更扩�?- 新增 `verify:data-platform-dp-c` · `verify:feat05-c1`；扩�?`verify:engineering`

### [ops-daily-report-spec] - 2026-07-12

### 策划

- 新增 [运营日报-v1.md](../../../../planning/specs/data-ops/运营日报-v1.md)�?*已确�?*）：按自然日查看线上运营数据的产品需�?- 覆盖�? 大模块指标口径、Asia/Shanghai 日界、静�?HTML 归档、与模拟目标对照、MVP/P1/P2 分期与验�?- 关联需求：**D-L3-01**（主）�?D-L3-02/05/08/09；对照现�?`aggregate-daily-metrics.mjs` / `daily-pipeline.mjs` 雏形与缺�?- 更新 [specs/README.md](specs/README.md) 架构与可观测性索�?
### [phase2-next-iteration-plan] - 2026-07-12

### 策划

- 新增 [Phase2-下一迭代开发计�?md](../../../../planning/specs/Phase2-下一迭代开发计�?md)�?*已确�?*）：�?ARC-08/DP-B 已实现为起点，排�?14 �?- 工程 N1：ARC-09/10 + BUG-08 �?N2：ARC-11；数�?DP-C→D；玩�?FEAT-05（C1…C3�?- 更新 prompts：`phase2-backend-dev` · `data-platform-phase2-dev`（DP-C）�?新增 `feat05-c-phase-dev`
- Kickoff / 计划表文档路径指向下一迭代专文

### [producer-progress-html] - 2026-07-12

### 策划

- 新增可视化看�?[`策划进度看板.html`](../../../../planning/策划进度看板.html)：计划表进度条、四阶容量阶梯（含千人差距）、开放待办、运维速查
- 生成脚本：`scripts/planning/build-producer-progress-html.py`
- `npm run planning:master-xlsx` **同时更新** xlsx + 看板；Skill `planning-progress-sync` / `verify-accept` 已要求验收必刷新

### [arc08-dp-b-engineering] - 2026-07-12

### 策划

- **ARC-08** gameState 拆分 �?**已实�?*（完成时�?**2026-07-12**�?- 数据平台 **DP-B** 三项 �?**已实�?*（完成时�?**2026-07-12**�?- D-L2-10 eventId 幂等 · D-L2-14 admin-web · D-L2-04 MetricsStore 抽象
- 新增 `verify:engineering` · `verify:data-platform-dp-b`

### 实现

- `pondSession.ts` / `pondUserManager.ts` / `pondChat.ts`；`gameState.ts` �?re-export
- `metricsStore.ts` + `sqliteMetricsStore.ts` + `postgresMetricsStore.ts`；`dedup_key` 幂等
- `admin-web/` Vite+React 运维台（timeline / debug / health / live inspector�?
### [phase2-remaining-design-risks] - 2026-07-12

### 策划

- DP-A **已实�?*后：新增 [Phase2-剩余事项设计与风�?md](../../../../planning/specs/Phase2-剩余事项设计与风�?md)�?*已确�?*，设计时�?2026-07-12�?- ARC-08~11 · FEAT-05 · BUG-08 �?**已确�?*；计划表产品 Phase2 无「未开始�?- 双轨排期 + 当前风险 / 实现后风险；Kickoff �?data-platform prompt 改为 **DP-B** + Sprint 2

### [data-platform-dp-a-otel-tap-health] - 2026-07-12

### 策划

- 数据平台 **DP-A** 三项 �?**已实�?*（完成时�?**2026-07-12**�?- D-L1-10 OpenTelemetry · D-L1-12 Socket Tap · D-L2-09 业务健康看板
- 新增 `verify:data-platform-dp-a`；计划表三项已同�?
### 实现

- `server/src/otelTracing.ts`：OTLP + span 缓冲；join/bite/disconnect 链路
- `server/src/socketEventTap.ts`：onAny/onAnyOutgoing 采样与脱�?- `server/src/businessHealth.ts` + Admin API + `AdminMetricsPanel` 7 日趋�?
### [data-platform-phase2-design] - 2026-07-12

### 策划

- 对照历史三层 Phase0/1/2 与计划表：Phase0 全完成；Phase1 主体完成，L3 日报批延后；�?Phase2 + 延后 L3 �?**15 项已确认**
- 新增 [数据平台-Phase2-稳定增长.md](../../../../planning/specs/data-ops/数据平台-Phase2-稳定增长.md)（设计时�?**2026-07-12**）�?Sprint DP-A~D
- 开发提示词：[data-platform-phase2-dev.prompt.md](../../../../planning/prompts/data-ops/data-platform-phase2-dev.prompt.md)
- 计划表：修正 L3 命名；补 Phase1「完成时间」；D-L* Phase2 行设计时�?2026-07-12
- Skill [planning-progress-sync](../../../../.cursor/skills/planning-progress-sync/SKILL.md)：明�?**创建填设计时间、完成填完成时间**

### [phase2-sprint1-deploy-jwt] - 2026-07-12

### 策划

- Phase 2 Sprint 1�?*ARC-06 Docker 容器�?*�?*ARC-07 Mobile JWT** �?**已实�?*
- 新增 `verify:deploy`；`docker:build` / `docker:up` / `docker:down` npm scripts
- 计划�?ARC-06/07 状态已同步�?*完成时间 2026-07-12**

### 实现

- **ARC-06**：根目录 `Dockerfile`（node:20-alpine 多阶段）、`docker-compose.yml`（SQLite �?`fish_social_data`）、扩�?`.env.example`
- **ARC-07**：`mobile/lib/jwtToken.ts`（SecureStore + 5 分钟提前续期）、`apiClient.ts`（Bearer + dev-token）；登录/socket/API 全链路携�?JWT

### [planning-progress-sync-skill] - 2026-07-12

### 策划

- 新增 Cursor Skill [`.cursor/skills/planning-progress-sync/SKILL.md`](../../../../.cursor/skills/planning-progress-sync/SKILL.md)：功能验收后同步计划表进�?checklist
- 更新 Opencode Skill [`.opencode/skills/verify-accept/SKILL.md`](../../../../.opencode/skills/verify-accept/SKILL.md)：结案必�?`planning:master-xlsx`
- 核对计划表：**BUG-11 已实�?*；`verify:server-boot` 通过；Kickoff 门禁开�?Sprint 1

### [phase2-kickoff-gate-bug11] - 2026-07-12

### 策划

- 新增 [phase2-开发Kickoff.md](../../../../planning/specs/roadmaps/phase2-开发Kickoff.md)：断线重�?Phase0·1 结论 + Phase 2 交接�?*BUG-11 为硬性门�?*
- 更新 [phase2-开发计�?md](../../../../planning/specs/phase2-开发计�?md) 前置条件；[phase2-backend-dev.prompt.md](../../../../planning/prompts/versions/phase2-backend-dev.prompt.md) 增加门禁必读

### [bug-11-tsx-watch-hang] - 2026-07-12

### 策划

- 新增 [BUG修复-tsx-watch启动挂死.md](../../../../planning/specs/bugs/BUG修复-tsx-watch启动挂死.md) �?**已实�?*（编�?**BUG-11**，P0�?- 根因：`tsx watch` + Phase 0/1 模块循环依赖导致 API 永不 listen（preview 红点 / 未连接）
- 修复方案：A �?`tsx` �?watch · B 拆循环依�?· C Logger 同步 stdout · D 补全 clean-ports
- 开发提示词：[bugfix-tsx-watch-hang-dev.prompt.md](../../../../planning/prompts/bugs/bugfix-tsx-watch-hang-dev.prompt.md)
- 已写�?[`项目开发需求计划表.xlsx`](../../../../planning/项目开发需求计划表.xlsx)

### 实现

- `server/package.json`：`dev` 改为 `tsx src/index.ts`（移�?`tsx watch`�?- `sessionRegistry.ts`：`logStructuredEvent` 改为动�?import，打�?observability �?- `logger.ts` / `db.ts`：`isDebugSampled` / `logWarn` 延迟 import；开发默认同�?stdout
- `scripts/clean-ports.mjs`：委�?`ports.mjs free --force`
- 新增 `verify:server-boot`

### [phase1-observability-platform] - 2026-07-11

### 实现

- **Phase 1 可观测性增强与运维平台�?0 项需求全部实现）**

**P1-A1 correlationId 入库�?Admin 过滤**
- Migration: fishing_metrics 新增 correlation_id �?+ 索引
- fishingMetrics.ts: writeMetric �?fields.correlationId 提取并写�?- admin.ts: GET /api/admin/metrics/fishing?correlationId=xxx 支持过滤; GET /api/admin/logs?correlationId=xxx 支持过滤

**P1-A2 动�?DEBUG 采样 per playerId**
- 新建 debugSampler.ts: DebugSampler 内存采样�? 60s TTL 清理, start/stop/list/history
- audit_log �? 记录 start/stop/expired 审计事件
- logger.ts: maskSensitiveFields, isDebugSampled 集成, 强制输出 debug 采样日志

**P1-B1 Loki 集中日志平台**
- 新建 logTransportLoki.ts: pino-loki transport, 条件初始�?(LOKI_ENABLED), 批量推�?- logger.ts: Loki 侧通道写入, 不可用时不阻�?- docker/docker-compose.monitoring.yml: loki + grafana

**P1-B2 日志合规**
- scripts/cleanup-old-logs.mjs: 超期日志清理 (默认 30 �?
- logger.ts: maskSensitiveFields 屏蔽 nickname/text 字段
- docs/compliance/log-audit-checklist.md: 合规审计清单文档

**P1-C1 Prometheus RED 指标**
- 安装 prom-client, 新建 metricsPrometheus.ts: Counter/Histogram/Gauge 全面覆盖
- createApp.ts: /metrics 端点条件挂载 (METRICS_PROMETHEUS_ENABLED)
- serverLoops.ts: 阶段计时, 咬钩检�? 生�?tick 写入 Prometheus 直方�?- sessionRegistry.ts: 连接数更�?Gauge

**P1-C2 Grafana 看板**
- docs/monitoring/grafana-dashboards/fish-social-overview.json (12 面板)
- docs/monitoring/grafana-dashboards/fish-social-pond-detail.json (3 面板 + 模板)
- docs/monitoring/README.md: 导入文档

**P1-C3 告警通知**
- docs/monitoring/alert-rules/prometheus.yml (5 条规�?
- docs/monitoring/alert-rules/loki.yml (1 条规�?
- server/src/alertWebhook.ts: 通用 JSON/钉钉/企微 Webhook
- scripts/test-alert.sh: 测试脚本

**P1-D1 Live Session Inspector (SSE)**
- 新建 liveSessionInspector.ts: SSE 推�? 每秒读取玩家 state, 最�?5 并发
- admin.ts: GET /api/admin/live-session?playerId=xxx SSE 端点

**P1-D2 客户端日志上�?*
- Migration: client_logs �?(id, player_id, ts, level, event_type, fields, created_at)
- createApp.ts: POST /api/client-logs 批量写入
- admin.ts: GET /api/admin/client-logs?playerId=&since=&limit= 查询

**P1-E1 Admin RBAC**
- 新建 adminRbac.ts: viewer/operator/admin 三级角色
- requireRole(minRole) 中间�? 无配置时 ADMIN_SECRET 向后兼容
- admin.ts: 敏感操作 requireRole('admin'), 写操�?requireRole('operator'), 只读 requireRole('viewer')

**新增文件**
- server/src/debugSampler.ts, logTransportLoki.ts, metricsPrometheus.ts, adminRbac.ts, liveSessionInspector.ts, alertWebhook.ts
- server/src/migrations/correlation_id.ts, client_logs.ts, audit_log.ts
- scripts/verify-phase1-core.ts, cleanup-old-logs.mjs, test-alert.sh
- docs/monitoring/README.md, grafana-dashboards/*.json, alert-rules/*.yml
- docs/compliance/log-audit-checklist.md
- docker/docker-compose.monitoring.yml

**修改文件**
- server/src/logger.ts: maskSensitiveFields, debug sampler, Loki side-channel
- server/src/fishingMetrics.ts: correlation_id 列写�?- server/src/admin.ts: 新增 10+ API 端点, RBAC 集成
- server/src/createApp.ts: /metrics 端点, /api/client-logs 端点
- server/src/db.ts: �?migration 调用
- server/src/serverLoops.ts: Prometheus metrics 集成
- server/src/sessionRegistry.ts: socketConnectionsGauge 更新
- server/package.json: prom-client, pino-loki 依赖
- package.json: verify:phase1-core, logs:cleanup 脚本

**验证**
- npm run verify:phase1-core: 15/15 全部通过
- API/DB/模块断言全部通过


### [data-platform-phase0-prompt] - 2026-07-11

### 策划

- 新增 [data-platform-phase0-dev.prompt.md](../../../../planning/prompts/data-ops/data-platform-phase0-dev.prompt.md)：三层数据体�?Phase 0 开发交接（10 条需求�?0 项任务、验收脚本）

### [data-platform-v0.6-reconciliation] - 2026-07-10

### 策划

- 更新 [`三层数据体系-开发需求清�?xlsx`](../../../../planning/reports/三层数据体系-开发需求清�?xlsx)�?1 �?**36 �?*需�?- 增补 **D-L1-10~12**（OTel、动�?DEBUG 采样、Socket 全事�?Tap）�?*D-L2-13~14**（Live Session Inspector、admin-web�?- 新增 [三层数据体系-可观测性补�?v0.6.md](../../../../planning/specs/三层数据体系-可观测性补�?v0.6.md)：对话方案与 xlsx 映射、不重复建设说明
- 勘误 D-L1-06/08 现状（v0.5 correlationId 部分落地；`/health` 已存在但过浅�?- xlsx 新增「方案对齐说明」sheet；`scripts/build-data-platform-roadmap-xlsx.py` 为单一生成来源

### [planning-dev-workflow] - 2026-07-09

### 策划

- 新增 [策划到开发工作流.md](../../../../planning/workflow/策划到开发工作流.md)：策划确�?�?计划�?xlsx �?开�?prompt �?验收 �?`planning:accept` 结案
- 新增 CLI：`scripts/planning/planning_workflow.py`（`confirm` / `accept` / `export` / `status`�?- 新增 [templates/验收清单模板.md](../../../../planning/templates/验收清单模板.md)
- `package.json` 增加 `planning:confirm`、`planning:accept`、`planning:export`、`planning:status`、`planning:sync-status`

### [docs-status-sync] - 2026-07-09

### 策划

- 批量同步 `docs/planning/specs/` �?**37 �?* spec 的「状态」字段，�?CHANGELOG、`项目开发需求计划表.xlsx` 对齐
- 重写 [specs/README.md](specs/README.md) 文档索引（分组、补�?v0.3~v0.5 条目�?- 新增 `scripts/planning/sync_spec_status.py` 供后续重复执�?
### [0.5.1-session-timer-broadcast] - 2026-07-09

### 策划

- 新增 [BUG修复-会话计时广播回归](../../../../planning/specs/bugs/BUG修复-会话计时广播回归.md) �?**已实�?*
- 根因：v0.5 R2-1 `consumeDirtyUsers` 误用�?1s 会话广播，waiting 阶段�?`sessionFishingMs` 推�?- 方案 A：会话计时每秒全�?fishing 用户 `enrichPondUser` + `pond_user_updated`，与 dirty 解�?- 开发提示词：[bugfix-session-timer-broadcast-dev.prompt.md](../../../../planning/prompts/bugs/bugfix-session-timer-broadcast-dev.prompt.md)

### 实现

- `serverLoops.ts`�?s `sessionTimer` 遍历 `SESSION_TIMER_PHASES` 用户�?`enrichPondUser` 广播，不再走 `consumeDirtyUsers`
- 新增 `verify:session-timer-broadcast`

### [0.5.1-pond-navigation-leave] - 2026-07-09

### 策划

- 新增 [BUG修复-切页误离塘与计时中断](../../../../planning/specs/bugs/BUG修复-切页误离塘与计时中断.md) �?**已实�?*
- 修订 [排查-挂机断线诊断阶段2-4](../../../../planning/specs/gameplay/排查-挂机断线诊断阶段2-4.md) §阶段3：社�?Debug 不再预期 `leave_pond`
- 需求表 [切页误离塘修复方�?v0.5.1.xlsx](../../../../planning/reports/切页误离塘修复方�?v0.5.1.xlsx) L1–L6 �?**已实�?*
- 开发提示词：[pond-navigation-leave-fix-dev.prompt.md](../../../../planning/prompts/gameplay/pond-navigation-leave-fix-dev.prompt.md)

### 实现

- `mobile/app/pond/[id].tsx`：社�?资料跳转不再 `leave_pond`；仅「← 地图」保�?`navigation_back`
- `mobile/lib/usePondSocket.ts`：unmount cleanup �?`socket.disconnect()`，移�?`leave_pond(unmount)`
- 新增 `verify:pond-navigation`；`verify-afk-diag` 改为 `navigation_back` 离塘断言

### [0.5-server-architecture] - 2026-07-09

### 策划

- 新增 [服务器架构优化路线图-v0.5](../../../../planning/specs/backend/服务器架构优化路线图-v0.5.md) �?**第一、二期已实现**
- 基于 [服务器架构问题与修复方案-v0.5.xlsx](../../../../planning/reports/服务器架构问题与修复方案-v0.5.xlsx)�?4 项）制定三期路线�?- **第一�?P0**：R0-1 鉴权 JWT · R0-3 timerRegistry · R0-4 咬钩收敛 · R0-2 session checkpoint
- **第二�?P1**：index 拆分 · SessionRegistry · 优雅停机 · correlationId
- **第三�?P2**：增�?snapshot · metrics 批量 · Admin/CORS · Bot 配置 · 单实例文档化
- 开发提示词�?  - [phase1](../../../../planning/prompts/backend/server-architecture-v0.5-phase1-dev.prompt.md)
  - [phase2](../../../../planning/prompts/backend/server-architecture-v0.5-phase2-dev.prompt.md)
  - [phase3](../../../../planning/prompts/backend/server-architecture-v0.5-phase3-dev.prompt.md)

### 实现

- **R0-1**：`auth.ts` JWT、`io.use` 校验、`requireAuth` 挂载社交/商城写接口、`POST /api/auth/dev-token`、`verify:auth`
- **R0-3**：`timerRegistry.ts`；disconnect_grace / pending_expire 迁入；leave/disconnect/clearSession 统一 `cancelByUser`/`cancelBySocket`
- **R0-4**：移�?`hookStateByUser` �?bot 独立 hook timer；bot 共用 `processWaitingBiteTick` + `tickFishingPhases`
- **R0-2**：`player_pond_session` + `pending_catch_locks` migration；checkpoint �?恢复；`verify:session-checkpoint`
- xlsx 总览 R0-1~R0-4 �?**已实�?*
- **R1-1**：拆�?`createApp` / `socketLifecycle` / `socketPondHandlers` / `serverLoops`；`index.ts` 收敛为编排入�?- **R1-2 + R1-3**：新�?`sessionRegistry`，替�?`playerSockets` 直接�?Map；社�?商城/gear/bot 统一�?registry
- **R1-4**：优雅停机分阶段 `stopLoops -> cancelAll timers -> close io/http/db`，并新增 shutdown phase 日志
- **R1-5**：连接生�?`socket.data.correlationId`，`logStructuredEvent` 自动附加 correlationId（socket/ALS�?- 新增/更新验收：`verify-auth`、`verify-server-observability`（适配 phase2 拆分�?- xlsx 总览 R1-1~R1-5 �?**已实�?*
- **R2-1**：`gameState` 新增 dirty/waiting 索引�?s 广播改为 dirty 增量，bite loop 仅遍�?waiting 用户
- **R2-2**：`fishingMetrics` 改为内存队列 + 1s/50 条批量落库；停机阶段 flush queue；`tickAllPonds` 合并单事�?- **R2-4**：production 强制 `ADMIN_SECRET`；新�?`ALLOWED_ORIGINS` 白名单策略；破坏�?admin 路由新增 `[admin_audit]`
- **R2-5**：新�?`MAX_BOTS_PER_POND` �?`BOT_EVICT_POLICY` 运行时配置；Admin overview 展示 botCount/humanCount/botRatio
- **R2-3**：路线图补充单实例容量建议与多实例前置条件（文档化）

### [0.4.4-metrics-reconciliation] - 2026-07-08

### 策划

- 新增 [v0.4.4-埋点缺口复核与补全](../../../../planning/specs/v0.4.4-埋点缺口复核与补�?md) �?**已实�?*
- 对照 `v0.4.4-埋点表清�?xlsx`�?3 条）复核代码落地�? 项真缺失�? �?eventType 别名�? 项字�?验收缺口�? �?xlsx 状态滞�?- 决策：新写入使用表内 canonical eventType（`socket_disconnect`、`pending_catch_accept`），summary 兼容旧名；`sqlite_query_slow` 为主、`slow_query` 为日志前缀兼容
- 确立 xlsx 手工版为后续埋点表格式标准（列顺序、必填字段换行、wrap_text�?- 开发提示词：[v0.4.4-metrics-reconciliation-dev.prompt.md](../../../../planning/prompts/versions/v0.4.4-metrics-reconciliation-dev.prompt.md)

### 实现

- `index.ts`：`session_rebound`（register_player / join_pond_reconnect）；`pending_catch_accept` + `sizeM`
- `fishingStateMachine`：metric/log eventType 对齐�?`socket_disconnect`
- `fishingMetrics`：summary 兼容 `disconnect`/`socket_disconnect`、`catch_accept`/`pending_catch_accept`
- `gameState.buildSnapshot`：节流输�?`snapshot_build_duration_ms`（共�?`PERF_LOG_INTERVAL_MS`�?- `db.timedDbQuery`：canonical `sqlite_query_slow` + `rows`；保�?`[slow_query]` 前缀
- `verify-server-observability` 扩展别名�?session_rebound / pending-timeout 产物断言
- 同步 `v0.4.4-埋点表清�?xlsx` 状态列�?4 条）
### [0.4.4-observability-gap] - 2026-07-08

### 策划

- [v0.4.4-未完成项补完](../../../../planning/specs/gameplay/v0.4.4-未完成项补完.md) �?**已实�?*
- 明确 v0.4.4 遗留 3 项：`phase_transition_invalid`、P2 性能埋点、`pending_catch_expired` 慢测 verify
- 新增补丁级实施与验收清单（P1/P2 分层�?- 开发提示词：[v0.4.4-observability-gap-dev.prompt.md](../../../../planning/prompts/versions/v0.4.4-observability-gap-dev.prompt.md)

### 实现

- `fishingStateMachine` 增加合法迁移白名单与 `phase_transition_invalid` 告警（日�?+ metrics�?- `fishingMetrics` timeline summary 新增 `phaseTransitionInvalidCount`
- `index.ts` 增加 P2 性能日志：`tick_fishing_phases_duration_ms`、`bite_check_loop_duration_ms`、`ecology_tick_duration_ms`、`socket_broadcast_fanout`
- `db.ts` 增加 `timedDbQuery`，超阈值输�?`slow_query`
- `admin.ts` 增加 `admin_route_duration_ms` �?`admin_route_slow`
- 新增 `scripts/verify-pending-timeout.ts` �?`verify:pending-timeout`（默认快测，`--slow` 真实等待�?
### [0.4.4-server-observability] - 2026-07-08

### 策划

- [服务器架构缺陷与埋点设计-v0.4.4](../../../../planning/specs/data-ops/服务器架构缺陷与埋点设计-v0.4.4.md) �?**已实现（P0 + 部分 P1�?*
- 梳理当前服务端“单进程 + 内存实时�?+ SQLite 持久化”的架构现状与风�?- 新增埋点分层：连�?会话、钓位、phase transition、咬钩产出、系统性能
- P0 实现 `join_pond_*`、`socket_connect*`、`fishing_phase_transition`
- 补充一�?P1：`spot_take_*`、`spot_release`、`pond_full_reject`、`bot_evicted_for_human`、`bite_tick_*`、`pending_catch_created`、`bait_depleted`
- 开发提示词：[server-observability-v0.4.4-dev.prompt.md](../../../../planning/prompts/data-ops/server-observability-v0.4.4-dev.prompt.md)

### 实现

- 新增 `server/src/fishingObservability.ts`，统一结构化日志与 `fishing_phase_transition`
- `index.ts`：补 `socket_connect` / `socket_connect_error` / `join_pond_attempt/success/fail`
- `gameState.ts` + `fishingStateMachine.ts`：补占座、断线、重连、hooked 恢复�?phase 迁移�?- `fishingMetrics.ts`：扩�?timeline summary，支�?join/socket/phase transition 事件
- `scripts/verify-server-observability.ts`：验�?join 事件、phase transition 与部�?P1 事件 summary

### [0.4.3-afk-diag] - 2026-07-08

### 策划

- [排查-挂机断线诊断阶段2-4](../../../../planning/specs/gameplay/排查-挂机断线诊断阶段2-4.md) �?**已实�?*
- **阶段 2**：客户端 Socket/AppState 生命周期日志 + 后台/弱网测试矩阵 M1–M9
- **阶段 3**：`leave_pond` 扩展 `reason` 埋点，排除导航切页误归因
- **阶段 4**：`GET /api/admin/metrics/fishing/player/:playerId` 时间�?+ SQLite 索引与排�?SOP
- 开发提示词：[diag-afk-phase2-4-dev.prompt.md](../../../../planning/prompts/bugs/diag-afk-phase2-4-dev.prompt.md)

### 实现

- `leave_pond` 支持 `{ pondId, reason }`；服务端 `[leave_pond]` 日志 + `fishing_metrics.leave_pond`
- `getPlayerFishingTimeline` + Admin API + `AdminMetricsPanel` 玩家时间�?- `idx_fishing_metrics_player_time` migration
- `mobile/lib/pondLifecycleLog.ts`：`[pond-socket]` / `[pond-app]` 结构化日�?- `usePondSocket`：`leavePondWithReason`、连接探针；鱼塘页导�?reason
- `scripts/verify-afk-diag.ts`：时间线 summary 验收

### [0.4.2-afk-disconnect] - 2026-07-08

### 策划

- [BUG修复-挂机断线离位](../../../../planning/specs/bugs/BUG修复-挂机断线离位.md) �?**已实�?*
- **P0**：重连时调用 `restoreDisconnectedUser`，取�?60s 断线清场定时�?- **P0**：`disconnect` / `reconnect` 结构化日志与 `fishing_metrics` 事件
- **P1（已实现�?*：`hooked` 阶段重连后按 `hookEndsAt` 续接 `hooked` 或立即进 `resolving`
- 开发提示词：[bugfix-afk-disconnect-dev.prompt.md](../../../../planning/prompts/bugs/bugfix-afk-disconnect-dev.prompt.md)

### 实现

- `reconnectSession` 仅恢�?socket 会话；`join_pond` 重连分支调用 `restoreDisconnectedUser`
- `fishingStateMachine`：`handleDisconnect` / `restoreDisconnectedUser` / timeout 结构化日志与 metrics
- `fishing_metrics` 新增 `disconnect`、`reconnect`、`disconnect_timeout`
- `index.ts` 移除 `markUserDisconnected` 重复写入
- `resumeAfterReconnect`：`hooked` 断线保存 `hookEndsAt`，重连复�?`advanceFromHooked`

### [0.4.1-bite-yield] - 2026-07-07

### 策划

- [咬钩产量调优-单鱼抽样与脱钩](../../../../planning/specs/咬钩产量调优-单鱼抽样与脱�?md) · [v0.4.1-开发交接](../../../../planning/specs/v0.4.1-开发交�?md)�?*已实�?*
- **D10**：咬�?1min/次；本点**单鱼**按品�?weight 抽取后判定（废除 Σeff�?- **D11**：`BITE_BASE_SCALE = 1/20` 全局缩放咬钩�?- **D12**：幼�?0.08�?.35m 脱钩抬高�?.1m�?2%�? 空杆飘字
- 目标�?0 人全服日上岸 **< 100 �?*（v0.4 基线 ~1235�?
### 实现

- `FISH_BITE_CHECK_MS = 60_000`；迁�?`bite_check_interval_60s`
- `pickSpotFishCandidate`、`calcSingleFishBiteProbability`；`rollBiteHook` 先抽后判
- `calcQualitySizeBiteRate` × `BITE_BASE_SCALE`；幼�?`calcSizeEscapeRate` 0.08�?.35m 线性段
- `emitFishingMissFloatText`（`miss` 飘字）；Debug `pickedFishId` / `pBite`
- `simulate-pond-day.ts`、`a0-verify.ts` 接入 D10–D12

### [0.4.0-spot-mobility] - 2026-07-07

### 策划

- [钓点鱼群流动性与分区咬钩](../../../../planning/specs/gameplay/钓点鱼群流动性与分区咬钩.md) · [v0.4.0-开发交接](../../../../planning/specs/v0.4.0-开发交�?md)�?*已实�?*
- **D9 钓点分区咬钩**：每条鱼 `spot_id`；咬钩仅本点�?- **迁徙与补充同 tick**�?5min）；40% 鱼换�?- **玩家 UI 不展示鱼�?*；Debug 每点只显示该点鱼
- **有意降低咬钩�?*；Bot 仅本点鱼�?
### 实现

- 迁移 `pond_fish_spot_id`：`spot_id` �?+ `(pond_id, spot_id)` 索引；`pond_state.last_migration_at`
- `shared/pondEcology.ts`：`FISH_MIGRATION_*`、`pickSpotForNewFish`、`pickMigrationSpot`、`calcSpotDestinationWeights`；`PondFishEntity.spotId`
- `server/src/pondEcology.ts`：`listPondFishAtSpot`、`migrateFishSpots`；`tickPondEcology` 补充→迁徙→生长
- `server/src/fishingSession.ts`：`buildBiteWeights` �?`listPondFishAtSpot`
- `fishingDebug.ts` / Admin：`fishAtSpotCount`、`lastMigrationAt`；塘内鱼列表�?`spotId`
- `scripts/a0-verify.ts`：D9 分区咬钩与迁徙验�?
### [0.3.2-supplement-tuning] - 2026-07-07

### 策划

- [生态调�?咬钩间隔与鱼群恢复](../../../../planning/specs/生态调�?咬钩间隔与鱼群恢�?md) §3.10–�?.11 · [v0.3.2-开发交接](../../../../planning/specs/v0.3.2-开发交�?md)�?*已实�?*
- **D7 动态补充频�?*：活跃钓�?�? �?�?15min 检查；人越少间隔越长（0 �?37.5min，倍率 cap 45min）；鱼数=0 仍立即兜�?- **D8 品质缺口补充**：以 `FISH_QUALITIES` 理想比例为基准；低品缺口优先；塘内仍有紫+ 时补充权�?×0.02

### 实现

- `shared/pondEcology.ts`：`calcSupplementCheckMs`、`calcSupplementQualityWeights`、`rollSupplementQuality`；`isFishingActive` 抽到 `shared/types.ts`
- `server/src/pondEcology.ts`：`countActiveAnglers`；`trySupplement` �?D7 动态间隔；补充品质�?D8（`seedPond` �?`rollFishQuality()`�?- `fishingDebug.ts` / Admin：展�?`activeAnglers`、`effectiveSupplementCheckMs`、各品质 actual/ideal
- `scripts/a0-verify.ts`：D7/D8 验收；`simulate-pond-day.ts` 接入 D7/D8

### [0.3.1-ecology-tuning] - 2026-07-07

### 策划

- [生态调�?咬钩间隔与鱼群恢复](../../../../planning/specs/生态调�?咬钩间隔与鱼群恢�?md) · [v0.3.1-开发交接](../../../../planning/specs/v0.3.1-开发交�?md)�?*已实�?*
- 咬钩判定间隔 30s �?**300s**�? 分钟�?- 鱼群恢复：每 15 分钟补充（缺�?**50%**）；**移除繁殖机制**
- 幼年尺寸�?*绝对 0.08~0.20m**（与品质无关�?- 脱钩成长�?*当前体长 × 1.02**
- 生长�?*共用 L(t) 绝对长度曲线**�? �?= 40m，低品质更早触顶
- **D6**：脱�?收杆**仅体�?*；非线性曲线；40m�?8.5%脱钩�?h收杆

### [0.3.1-ecology-tuning] - 2026-07-07

### 实现

- [生态调�?咬钩间隔与鱼群恢复](../../../../planning/specs/生态调�?咬钩间隔与鱼群恢�?md) · [v0.3.1-开发交接](../../../../planning/specs/v0.3.1-开发交�?md)�?*已实�?*
- 咬钩判定间隔 `FISH_BITE_CHECK_MS=300000`�? 分钟）；生�?tick �?30s
- 废弃阻断式耗尽恢复；每 15 分钟 `trySupplement`（缺�?50%，单次上�?12 条）
- 移除繁殖 `tryBreed`；幼年鱼绝对体长 0.08~0.20m（`rollJuvenileSize`�?- 脱钩成长 `sizeM × 1.02`；全鱼共用绝对长度–时间曲�?L(t)�? �?= 40m

### [0.3.0-bite-escape-rework] - 2026-07-06

### 实现

- [数值重�?品质尺寸咬钩脱钩](../../../../planning/specs/数值重�?品质尺寸咬钩脱钩.md) · [v0.3.0-开发交接](../../../../planning/specs/v0.3.0-开发交�?md)�?*已实�?*
- 咬钩/脱钩与物种解耦，改由品质×尺寸公式决定
- 尺寸越大咬钩率越高（满尺�?最高值，最小尺�?35%）；脱钩率同方向递增
- 废弃 `NEAR_MAX_BITE_RATE`、`QUALITY_ESCAPE_BONUS`、`species.biteWeight` 计算路径
- `pond_fish` 新增 `bite_multiplier`、`escape_multiplier` 个体偏置列（入库�?
记录 `docs/planning/` 目录本身的修订，非游戏版本发布说明�?
### [0.2.6-fishing-probability] - 2026-07-06

### 实现

- [分析与修�?钓鱼概率与饵文案](../../../../planning/specs/分析与修�?钓鱼概率与饵文案.md) · [v0.2.6-开发交接](../../../../planning/specs/v0.2.6-开发交�?md)�?*已实�?*
- 强制 `FISH_BITE_CHECK_MS=30000`（迁�?+ initGameConfig upsert + runtime 下限�?- 启动日志 `bite check interval = 30000ms`；玩�?Bot 循环共用 `getBiteCheckMs()`
- `fishingDebug.ts` 去掉 bite 相关 round2；Debug 动态间�?+ `formatBiteRatePct`
- 商店/图鉴饵文案统一�?% 2 位小数；商店补食性偏好行与说�?
### [0.2.5-fishing-duration] - 2026-07-03

### 实现

- [BUG修复-鱼塘钓鱼时长显示](../../../../planning/specs/bugs/BUG修复-鱼塘钓鱼时长显示.md) · [v0.2.5-开发交接](../../../../planning/specs/v0.2.5-开发交�?md)�?*已实�?*
- `sessionFishingMs` 会话时长；头�?badge 改展示会话而非今日累计
- `flushFishingSessionToToday`：收起鱼�?停止�?`addTodayFishingMs` 落库
- 1s 广播覆盖 baiting~stopping 全阶段；`formatFishingDuration` 统一含秒格式
- `PondCharacter` badge minWidth 148px；Demo 模式会话计时对齐

### [0.2.4-debug-grid] - 2026-07-03

### 实现

- [v0.2.4-开发交接](../../../../planning/specs/v0.2.4-开发交�?md) 任务 1�?*已实�?*
- 新建 `AdminPondFishDebugGrid.tsx`：合并鱼塘明�?+ 钓鱼概率；页签「鱼�?| 钓位 1…N�?- 鱼格 200×200；鱼塘页签仅生态字段；钓位页签展示咬钩/脱钩/�?运气 ×N
- 角标「体型上限�? 图例；删�?`AdminFishingDebugPanel.tsx`
- `fishingDebug.ts`：`fishContributions` 过滤 `effectiveBite > 0`
- UI §8.2 商店显示此前已验收，本次无代码变�?
### [0.2.2-b1] - 2026-07-01

### 实现

- [B1-鱼饵偏好](../../../../planning/specs/gameplay/B1-鱼饵偏好.md) 状态→**已实�?*
- `baitBiteBonus(baitId, speciesId)` �?`globalBonus + affinityByDiet[diet]`
- 鱼种 `diet` 对齐策划表（carp→草食、perch→杂食等�?- 商店 UI 偏好标签；Debug 面板饵切�?+ diet �?
### [0.2.1-b0] - 2026-07-01

### 实现

- [B0-商店基础](../../../../planning/specs/gameplay/B0-商店基础.md) 状态→**已实�?*
- `player_gear` �?+ 迁移（存量玩家赠 5 个玉米试用）
- `shop.ts` REST API（购�?装备/幂等 token）；`gear.ts` 持久�?- 咬钩公式接入饵加成与渔具脱钩减免；咬钩成功扣�?+ `bait_depleted` 事件
- 鱼塘「补给」按�?+ `ShopModal` / `useShop`

### [0.2.0-alpha3-a2] - 2026-07-01

### 实现

- [A2-Debug面板](../../../../planning/specs/gameplay/A2-Debug面板.md) 状态→**已实�?*
- `GET /api/admin/ponds/:pondId/fishing-debug`（admin key�?s 内存缓存，`?refresh=1` 绕过�?- `server/src/runtimeConfig.ts` 只读运行时数值；`fishingDebug.ts` 聚合报告
- `fishingSession.ts` 导出 `getSpotBiteTickModel`、hook 阶段 `phaseEndsAt`
- 客户�?`AdminFishingDebugPanel.tsx` + `adminApi.getFishingDebug`

### [0.2.0-alpha2-a1] - 2026-07-01

### 实现

- [A1-飘字广播](../../../../planning/specs/gameplay/A1-飘字广播.md) 状态→**已实�?*
- Socket `fishing_float_text`；`emitFishingFloatText()` 封装
- 咬钩瞬间 hook 飘字；`hookDurationMs` �?escape 飘字 + `fish_miss` / `fish_bite`
- 客户�?`FishingFloatText.tsx` + i18n（zh-CN / en-US�?
### [0.2.0-alpha1-a0] - 2026-07-01

### 实现

- [A0-数值重构](../../../../planning/specs/A0-数值重�?md) 状态→**已实�?*
- 新建 `shared/fishing.ts`：指数咬�?`P=1-exp(-W×0.02)`、品质尺寸上限、脱钩率、成长公�?- 鱼种扩展 `biteWeight` / `baseEscapeRate` / `diet`；品质出生固定、成长不改品�?- 服务�?`fishingSession.ts` + `migrations/fishing_v2.ts`；`pond_fish.bite_weight` �?- `npm run verify:a0` 自动化验收脚�?
### [0.2.3.1-ui-patch] - 2026-07-03

### 实现

- [UI体验修复-社交商店图鉴](../../../../planning/specs/gameplay/UI体验修复-社交商店图鉴.md) §8.1~8.2 补丁�?*已实�?*
- §8.1：`PendingFishCatch.isCodexNew`；钓获弹窗「新」角标；移除图鉴 Alert
- §8.2：商�?`flex` 布局修复；`useShop` 静态兜�?+ 错误重试；打开�?`await refresh()`
- 修复 `ensurePlayerGear` 对不存在 `players` 行写入时�?FK 崩溃（Bot tick / 商店 API�?
### [0.2.3-ui-fix] - 2026-06-30

### 实现

- [UI体验修复-社交商店图鉴](../../../../planning/specs/gameplay/UI体验修复-社交商店图鉴.md) 状态→**已实�?*
- `PostCard` 纪念�?`contain` + 4:3，`maxHeight` 240/320，加载失败隐�?- `ShopModal` / `CodexModal` 背包式双栏布局；`ShopButton` / `CodexButton` 全局入口
- `codexApi.ts` 封装图鉴读取；Debug 入口扩展至鱼�?社交/资料
- 好友动�?Tab 改名 + `posts.ts` SQL 过滤非好友公开�?
### [0.2.4-a0-v2-fix] - 2026-07-03

### 修复

- `rollInitialSize`：近满尺寸分支用 `ceil(threshold×100)/100` 避免 round2 漏计；非满尺�?cap 防金品质 round 误入 97.5% �?- `getQualityMaxSize` 至尊/神话可达 40m 上限（支�?gold �?0m 初始�?- `scripts/a0-verify.ts`�?4 日成长参数修正；满尺寸占比容�?0.50%~0.70%�? 万次采样�?- `npm run verify:a0` 全通过
- Admin Debug 钓点 Top 鱼贡献改�?4 列方�?- `AdminConfigPanel` 配置项中文标�?+ 说明（�?.2�?
### [0.2.4-a0-v2-debug-ui] - 2026-07-03

### 策划更新

- [数值重构v2-成长咬钩与文�?md](../../../../planning/specs/数值重构v2-成长咬钩与文�?md) §6.3
  - 鱼塘明细 + 钓鱼概率 **合并为页签方格面�?*（鱼�?/ 钓位1…N�?  - 鱼格 **200×200**、字号下限；概率字段 **仅钓位页�?*
  - 角标「满」→ **「体型上限�?* + 图例说明

### [0.2.4-a0-v2] - 2026-07-01

### 实现

- [数值重构v2-成长咬钩与文�?md](../../../../planning/specs/数值重构v2-成长咬钩与文�?md) 状态→**已实�?*
- `shared/fishing.ts`�?4 日成长、满尺寸鱼、乘法咬�?脱钩�?0s 判定
- `server`：`pondEcology` 钓点系数 0~5、`fishingSession` 新模型、`fishing_v2_mult` 迁移
- `mobile`：商�?图鉴人话文案、Admin 鱼塘方格、Debug 面板 v2 字段
- `scripts/a0-verify.ts` 更新�?A0-v2 验收

### 新增（策划）

- [数值重构v2-成长咬钩与文�?md](../../../../planning/specs/数值重构v2-成长咬钩与文�?md)�?*已确�?*�?  - 14 日成长至上限；初始尺寸全区间 + 0.6% 满尺寸（�?30m+ 至尊�?  - 满尺寸：3% 咬钩 / 98.4% 脱钩 / 2h 收杆
  - 钓点乘法系数 0~5；咬钩判�?30s；渔具脱钩乘法最�?30%
  - 全站/Debug 人话文案；鱼塘明细方格；generation→初始鱼/�?n �?- A0 §3.2~3.8 标记为部�?superseded

### [0.2.3.1-ui-patch] - 2026-07-01

### 策划更新

- [UI体验修复-社交商店图鉴.md](../../../../planning/specs/gameplay/UI体验修复-社交商店图鉴.md) 新增 **§8 补丁**
  - 图鉴解锁：钓获弹窗「新」角标，废弃顶部 Alert
  - 商店：网�?详情必显、布局 flex 修复、API 失败静态兜�?
### [0.2.3-ui-fixes] - 2026-07-01

### 新增

- [UI体验修复-社交商店图鉴.md](../../../../planning/specs/gameplay/UI体验修复-社交商店图鉴.md)�?*已实�?*�?  - PostCard 纪念�?contain + 4:3
  - Shop/Codex 背包式双�?+ 全局入口
  - Debug 全页面入�?  - 好友动�?Tab 过滤修正

### [0.2.0-fishing-v2-split] - 2026-07-01

### 新增 / 拆分

- �?spec 拆为分阶段文档（**均已确认**）：
  - [A0-数值重构](../../../../planning/specs/A0-数值重�?md)、[A1-飘字广播](../../../../planning/specs/gameplay/A1-飘字广播.md)、[A2-Debug面板](../../../../planning/specs/gameplay/A2-Debug面板.md)
  - [B0-商店基础](../../../../planning/specs/gameplay/B0-商店基础.md)、[B1-鱼饵偏好](../../../../planning/specs/gameplay/B1-鱼饵偏好.md)
  - [C-调优与状态机](../../retired/planning/specs/C-调优与状态机.md)、[状态机需求描述](../../../../planning/specs/状态机需求描�?md)
- [specs/README.md](specs/README.md) 索引；[钓鱼系统v2-开发交�?md](../../../../planning/specs/钓鱼系统v2-开发交�?md) 批量交接
- 采纳 §4.11 五项状态机决策；指数咬�?`P=1-exp(-W×0.02)`；品质尺寸上限调整（purple 4.5 等）

### [0.2.0-fishing-v2-draft] - 2026-06-30

### 新增

- [钓鱼系统v2-生态与玩法重构.md](../../retired/planning/specs/钓鱼系统v2-生态与玩法重构.md)（已由子文档 supersede�?
### [0.1.1-profile-pond-ui] - 2026-06-30

### 实现

- [BUG修复-资料与鱼塘UI.md](../../../../planning/specs/bugs/BUG修复-资料与鱼塘UI.md) 状态→**已实�?*
- 关弹窗闪屏：`useProfileModal` 延迟清空 target；`UserProfileModal` 支持 `onModalHide`
- 首页头像直进 `/profile`，移�?`useProfileModal`
- 自己资料弹窗「编辑资料」置�?ID 下方
- 新增 `PondSocialPanel` 合并在线 chips + 聊天

### [0.1.1-bugs-impl] - 2026-06-30

### 实现

- [BUG修复-四项体验问题.md](../../../../planning/specs/bugs/BUG修复-四项体验问题.md) 状态→**已实�?*
- **BUG-2**：`inventory.ts` 增加 `pondFishId` �?pending 锁与超时释放；`pickFishForBite` 支持排除已锁鱼；机器人跳过已锁实�?- **BUG-1**：`UserProfileModal` 关闭期不重置好友状�?+ 父组件乐观更�?`friendIds`
- **BUG-3**：`useProfileModal` 统一入口；地�?鱼塘顶栏与场景角色可点开资料弹窗；自己展示公开资料 +「编辑资料�?- **BUG-4**：世界地图顶�?`AdminDebugButton`；社交设置保留弱样式备用入口

### [0.1.1-ui] - 2026-06-30

### 新增

- 简�?spec [BUG修复-资料与鱼塘UI.md](../../../../planning/specs/bugs/BUG修复-资料与鱼塘UI.md)�? 项，已确认）

### [0.1.1-bugs] - 2026-06-30

### 新增

- 专项策划 [BUG修复-四项体验问题.md](../../../../planning/specs/bugs/BUG修复-四项体验问题.md)（已确认，待开�?Agent�?
### [0.1.1-workflow] - 2026-06-30

### 变更

- 明确 **�?Agent 分工**：策划只文档、开发只代码
- 新增 [HANDOFF.md](../../../../planning/workflow/HANDOFF.md)、[templates/开发交接说�?md](../../../../planning/templates/开发交接说�?md)
- 更新 WORKFLOW.md、Cursor 规则 `planning-docs.mdc` / `dev-from-planning.mdc`

### 说明

「他人主页优化」曾由同一 Agent 兼做策划与开发；后续需求请�?HANDOFF 流程执行�?
## [0.1.1] - 2026-06-30

### 新增

- 专项策划 [他人主页优化.md](../../../../planning/specs/gameplay/他人主页优化.md) 并实现：`UserProfileModal` 展示简介、收藏品、动�?- API `GET /api/players/:playerId/public-view?viewer=`
- 共享类型 `PublicPlayerView`、`PublicPlayerProfile`

## [0.1.0] - 2026-06-30

### 新增

- 建立 `docs/planning/` 策划文档目录与工作流（`README.md`、`WORKFLOW.md`�?- 添加文档索引 `INDEX.md`
- 添加功能规格、版本变更记录模�?- 从代码库梳理并落�?**[v0.1.0-功能全景.md](../../versions/planning/product/v0.1.0-功能全景.md)**（完整功能基线）
- 添加 Cursor 规则 `planning-docs.mdc`，支�?AI 协作维护策划文档

### [v0.6.0-social-interaction] - 2026-07-13

### 策划

- 新增 [社交互动-动态点�?md](../../../../planning/specs/社交互动-动态点�?md)�?*已确�?* / **FEAT-SOC-01**�?- 新增 [社交互动-动态评�?md](../../../../planning/specs/社交互动-动态评�?md)�?*已确�?* / **FEAT-SOC-02**�?- 新增 [排行榜系�?md](../../../../planning/specs/排行榜系�?md)�?*已确�?* / **FEAT-SOC-03**�?- 开发提示词：[v0.6.0-backend-dev.prompt.md](../../../../planning/prompts/versions/v0.6.0-backend-dev.prompt.md) · [v0.6.0-frontend-dev.prompt.md](../../../../planning/prompts/versions/v0.6.0-frontend-dev.prompt.md)
- 计划表已更新�?7 行，�?3 项新需求）

- 实现：清空旧状态、连接代际门禁、snapshotReady、显式演示开关，取消 10 秒静默 DEMO 降级
- `usePondSocket`��������վ�״̬�����Ӵ��ʱ�����snapshotReady �Ž���ȡ�� 10 �뾲Ĭ DEMO ����
- `pond/[id].tsx`������ǰ�����ʾ�������С������ÿ�ʼ����
