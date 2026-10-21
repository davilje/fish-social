# 原版猫咪钓鱼手账 UI：Unity 完整替换清单

> 制作进展更新：资源与原版对照已生成，最新状态见 [资源交付与视觉纠偏](../../art/cozy-journal/README.md)。下文保留原审计口径，不代表全部正式页面已替换。

日期：2026-10-21。依据：当前工作区，Git HEAD `806a765d`，含本次已补齐的原版 HTML 原型。本文是代码与资源审计后的迁移清单；尚未实施 Unity 替换，也未进行本次 Unity Play Mode / Player 构建验收。

## 1. 替换目标与范围

视觉和交互基准是 [已补齐的原版原型](/Users/davilje/fish-social/docs/design/ui/prototype/cozy-journal/index.html)，包含 `style.css / app.js / pages.css / pages.js`，以及最新两项修正：主界面常驻「钓鱼世界」入口、鱼塘详情与收费档位分区。不是另做的 v2 方案，也不是原型工具栏中的“现状”截图。

目标为 Unity 桌面主窗口的完整 UI 替换：外壳、10 个原型页面、子页、弹窗、提示、动效、状态和资源；同时把原型未覆盖的现有功能纳入同一视觉体系，避免旧页面残留或功能丢失。沿用 Unity 2021.3.29f1c1、uGUI、现有业务控制器和真实数据接口。

浏览器原型的演示工具栏、速度控制、空格对比、示例数据、本地保存和模拟入塘成功页不进入正式客户端。皮肤、数量、等级、金币、库存、鱼塘费用均使用实际数据。

WPF 原生 Overlay 是另一套渲染系统。按原需求文档，本次主窗口方案不重做其 960×560 钓鱼画布，但必须验收入塘、托盘、回主窗、结算和社交跳转的衔接。若“完全替换”也包含 WPF 场景本身，需要另补对应场景与 HUD 的设计稿，不能用主窗口原型直接代替。

## 2. 已确认的关键差异

| 优先级 | 当前代码事实 | 完整替换必须处理 |
|---|---|---|
| P0 | `DesktopShell.prefab` 保存的参考分辨率为 1672×941；`DesktopShellUi.TryLoadShellCanvas()` 运行时改为 `WindowSettings.DefaultWidth/Height`，即 1920×1080 | 分离“设计分辨率”和“实际窗口尺寸”。保留 1672×941 设计坐标，统一缩放；实际窗口仍支持四档预设 |
| P0 | `WireShellFromPrefab()` 未绑定顶栏世界地图按钮；已有 WorldMap 路由 | 新增并持久化顶栏 `WorldMapEntry`，绑定已有路由，纳入新手引导锁定与键盘焦点；首页和功能页都可达 |
| P0 | 地图右栏通过 `DetailsText` 一段文本拼接位置、等级、费用、次数和鱼种；选塘列表右栏也用一个 Body | 拆成照片、地点资料、准入状态、计费说明、档位、主按钮，复用同一个详情视图，避免长文挤压操作区 |
| P0 | 列表 `EnterSelected()` 仅传 pondId，调用地图 `EnterPond()`；地图重选鱼塘会重置档位为 `sell_only` | 共用选择状态与入塘流程，显式携带 pondId 和 returnFeeMode；不能让列表选好的档位被隐式重置 |
| P0 | `CozyPondSelectionPanel` 仅按是否开放/巨物塘显示可进入；地图另检查等级、新手引导 | 两入口共用准入判定，覆盖未加载进度、等级不足、未开放、连接中和实际服务端拒绝 |
| P0 | 页面、组件预制体已存在，运行时大量通过 `Transform.Find` 查固定路径；构建守卫要求预制体完整且多数为 Variant | 改布局时同步绑定、嵌套实例、校验与生成工具；不能只改单页 PNG 或运行时临时节点 |
| P1 | `CozyMotion` 已实现按压、基础翻页、数字滚动、水波、导航与静态猫；地图对所有未锁定标记启动 Float，猫和导航也会 Float | 补齐完整表现并新增同屏唯一循环调度、隐藏暂停、取消与归位；现状不满足单循环要求 |
| P1 | Reduced 模式下 `PressDown` 仍缩放，`Flip` 直接切换；默认只读取 PlayerPrefs | 统一为短淡入淡出，禁位移/缩放/旋转，处理系统偏好、切换中打开减少动效等状态 |
| P1 | 设计文档曾建议 DOTween，但 STEAM-DESKTOP-21 规格明确本期不引入；代码已有自有动效门面 | 推荐扩展现有 CozyMotion；DOTween 不是完整替换的必要依赖。统一令牌和验收效果优先 |
| P1 | Unity 动态接口支持鱼获分享、点赞、评论；原型增加自由文字发布、我的动态和本地关注等 | 分清可复用接口与产品新增项，详见第 6 节；不能把本地演示成功当正式成功 |

P0 为会影响整体可用性或数据正确性的前置工作；P1 为达到完整视觉和交互目标的必要工作；P2 为补充覆盖与发布收尾，并非可以永久遗漏。

## 3. 页面与交互逐项清单

以下勾选框均表示待实施/待验收，不代表已经完成 Unity 迁移。

### A. 主界面、顶栏、导航与页面容器（P0）

涉及：`DesktopShellUi`、`PanelRouter`、`DesktopAppBootstrap`、`DesktopShell.prefab`、`PanelHome.prefab`、`PanelPond.prefab`、`CozyRedesignAuthoring.Shell/Common`。

- [ ] 保留樱花背景、头像/昵称/等级/经验、金币/鱼获、铃铛和设置，按原型重新排列；数字支持千分位和大数。
- [ ] 顶栏新增常驻「钓鱼世界 / 探索地图·选择鱼塘」，首页和手账页均可点击；关闭手账后依旧存在。
- [ ] 底部固定七入口：背包、装备、商店、成就、排行榜、个人信息、钓友动态；地图/设置不是额外挤入底栏。
- [ ] 统一手账纸页、三枚活页环、标题、点缀色、左栏索引、卡片区、右详情、右上关闭按钮。
- [ ] 关闭手账回首页，不退出当前鱼塘会话；切普通页不触发 leave_pond。地图与列表互跳、资料编辑返回均保留合理选择与滚动位置。
- [ ] 修正 `AlignHeaderActions` 等运行时代码，避免覆盖新布局；去重旧 Header/Home/Leaderboard 等重复入口。
- [ ] 切页采用可取消的过渡，只保留一个活动页面和一个导航浮漂；过渡时禁重复业务提交，完成/取消都恢复 raycast 和 alpha。
- [ ] 玩家名字/猫皮肤/金币/经验/库存变化同步顶栏；首次加载、断线和失败显示真实状态，不瞬间展示演示数值。

### B. 世界地图（P0）

涉及：`DesktopWorldMapPanel`、`WorldMapPondCatalog`、`DesktopGameData`、`PanelWorldMap.prefab`、`WorldMapPondMarker.prefab`。

- [ ] 地图采用贴在纸上的照片与四角胶带，保留拖动、滚轮缩放、边界约束；重置视图放地图工具区或左栏，避免占右栏入塘操作空间。
- [ ] 补齐原型分类图例：野外、水库、进阶、老手、巨物、禁区、未解锁；分类色、锁图标、开放状态与数据一致。
- [ ] 将矩形标记改为浮标：固定命中区、悬停名称/放大、选中爪印与两圈水波、350ms 平滑定位；未解锁标记仍可看详情但不漂。
- [ ] 右栏用共享鱼塘详情组件；补真实鱼塘图片映射，按 pondId 寻图，不能用列表序号轮换同一张照片来冒充真实场景。
- [ ] 左栏增加“列表选塘”，列表“地图查看”返回时聚焦同一鱼塘，保持档位、缩放和可预期的地图位置。
- [ ] 入塘时禁重复点击、显示连接状态；服务端确认成功后完成浮标下沉演出并衔接原生 Overlay。失败恢复按钮、档位和选择，不先显示成功。

### C. 选塘列表（P0）

涉及：`CozyPondSelectionPanel`、`DesktopShellUi.BuildPondSelection`、`PanelPondSelection.prefab`、`PondSelectionCard.prefab`。

- [ ] 增加全部、可进入、免费鱼塘筛选；增加名称/分类/水域搜索，空结果与清除条件入口。
- [ ] 卡片按原型重排为图片、名称、地点/水域、鱼种、门槛和费用；整卡选择与按钮操作分清。
- [ ] 不硬编码“20 个”：数量、显示资格、名称与 `WorldMapPondCatalog.LoadVisible()` 一致；免费筛选按真实费用语义定义。
- [ ] 添加原型右栏档位选择，地图/列表共用状态与计费展示。无双档的鱼塘展示实际单档说明，不保留无意义的切换按钮。
- [ ] 列表刷新保留有效选中项；搜索结果排除当前选中项时，明确清空或选中首项，不显示与列表无关的旧详情。
- [ ] 卡片首次进入可见区时贴入，回滚不重播；分页/过滤新项重新判断，复用与销毁时清理动画。
- [ ] “到底了”提示不遮卡片；鼠标滚轮、拖动、卡片选择不互相抢事件。

### D. 背包（P1）

涉及：`DesktopCatchBagModalView`、`PanelCatch.prefab`、`CatchSlot.prefab`、`CozyItemArt`。

- [ ] 左栏鱼获/道具分类、贴纸鱼卡、稳定倾角、NEW 角标、选中胶带、右侧拍立得鱼图及尺寸/稀有度/来源信息。
- [ ] 现有背包主要绑定鱼获，补道具分页的数据来源和真实数量；从现有装备/鱼饵库存接口核对，不造本地库存。
- [ ] 保留出售、分享、符合条件时回塘，以及当前档位下的禁用原因；删除/出售选中鱼后同步展示柜等引用状态。
- [ ] 新获得/已查看标记确定账号级存储策略；UI 浏览状态可本地存，真实库存和售卖结果由服务端决定。
- [ ] 出售成功才播纸屑与金币飞入；失败保持鱼卡，显示可读原因；重复点击不重复出售。

### E. 装备与商店（P1）

涉及：`DesktopShopPanel`、`PanelEquipment.prefab`、`PanelShop.prefab`、`ShopItemCard.prefab`。

- [ ] 保留商店鱼饵/钓竿/船具三类和装备钓竿/鱼饵两类，复用已有双模式逻辑，分别适配原型网格。
- [ ] 卡片白边、投影、稳定倾角、胶带；右栏拆图片/说明/数量与价格/按钮，长描述可滚动且按钮固定。
- [ ] 已拥有、未解锁、金币不足、购买中、已装备和库存耗尽状态完整显示；真实数量/价格/门槛来自目录与装备接口。
- [ ] 购买成功商品沿弧线飞向背包，金币滚动；装备成功盖章；失败按钮轻摇并标明原因。
- [ ] 购买/装备后同步相关页与顶栏，动画不改变业务提交次数。

### F. 成就（P1，领奖功能见第 6 节）

涉及：`DesktopProfileHubPanel` 成就分区、`PanelAchievements.prefab`、`AchievementRow.prefab`。

- [ ] 独立成就页和个人中心成就页使用同一数据与状态；全部/已完成/未完成筛选按最终界面统一。
- [ ] 奖章卡、解锁盖章/翻转、进度、完成时间、隐藏成就展示；无数据、未解锁和全部完成状态。
- [ ] 原型“领取奖励”不直接本地加金币。若保留，须补服务端奖励配置、领取与幂等记录；否则改为实际支持的“已解锁”呈现。

### G. 排行榜（P1）

涉及：`DesktopLeaderboardPanel`、`PanelLeaderboard.prefab`、`LeaderboardRow.prefab`。

- [ ] 保留已有四榜 API、缓存、分页/分帧生成与我的名次，按原型改前三名领奖台、行选中及右侧钓友卡。
- [ ] 榜单值按对应指标与单位显示，不把原型本地排序逻辑移到客户端重新计算正式名次。
- [ ] 玩家卡进入他人资料，接好友申请/私聊等实际能力；“关注”另见第 6 节。
- [ ] 验证不足三人、空榜、自己未上榜、未入塘时当前塘榜不可用、缓存刷新失败和超长昵称。

### H. 个人信息与五个子页（P1）

涉及：`DesktopProfileHubPanel`、`DesktopProfileEditPanel`、`PanelProfileHub.prefab`、`PanelProfileEdit.prefab`、`ShowcaseSlot.prefab`、`AvatarChoice.prefab`、`GallerySpeciesSlot.prefab`。

- [ ] 资料：头像/名字/签名/等级经验/统计卡，编辑入口，自己和他人的权限区分。
- [ ] 编辑：昵称/签名、头像选择、保存/取消、错误提示、长度限制；取消不写入，保存成功才更新顶栏和其他页面。
- [ ] 展示柜：三个槽位、已选鱼详情、选鱼弹窗、清空/替换；对接现有 SetShowcase，清空展示位不等于出售鱼。
- [ ] 图鉴：使用实际全量鱼种，已收录图片与未收录剪影、记录明细；原型的 12/15 张示例不能作为正式目录上限。
- [ ] 相册：双拍立得、翻页、候选照片、钉入/取下、数量上限、网络图片加载与失败占位；沿用 album pins/candidates 真实语义。
- [ ] 成就：复用成就数据/组件；展示柜仅当前鱼摆尾，暂停导航和猫等其他持续循环。
- [ ] 他人资料为只读，保留好友/私聊等现有入口，禁止出现可编辑自己库存的按钮。
- [ ] 检查旧 `PanelProfile` 与独立 `PanelGallery` 的调用，完成兼容路由后再决定是否删除，避免旧入口露出旧皮肤。

### I. 钓友动态（P1）

涉及：`DesktopSocialFeedPanel`、`DesktopSocialPostCard`、`PanelSocialFeed.prefab`、`SocialPostCard.prefab`、`PostCommentRow.prefab`。

- [ ] 公开/好友/我的三个分类、可滚动动态卡、点赞、作者跳转、右栏详情和评论输入，按原型布局改造。
- [ ] 当前评论在卡片内展开，需迁移到右栏并保持 postId 绑定；切帖子取消/丢弃旧请求，避免旧评论串页。
- [ ] 复用点赞、评论、删自己评论、Socket 更新、分页和加载更多；保留原有权限与失败回滚。
- [ ] 添加发布弹窗：真实鱼获选择、可见范围、文字、提交状态；“文字”和“我的动态”存在接口差异，按第 6 节补齐。
- [ ] 切分类/返回帖子保持合理滚动与选择；图片失败、无好友动态、无自己的动态均有明确状态。

### J. 设置（P1）

涉及：`DesktopSettingsModalView`、`PanelSettings.prefab`、`WindowManager/WindowSettings`、通知设置。

- [ ] 沿用原型索引、猫爪拨钮、选中盖章，保留真实窗口模式与 1280×720、1600×900、1920×1080、2560×1440 四档。
- [ ] 原型只有三个分类，Unity 另有“服务器连接”；保留该能力，建议作为第四个同风格分类，不因照搬原型删掉。
- [ ] 覆盖通知总开关、免打扰、咬钩/好友/连接错误通知、托盘行为、退出游戏、服务器保存/测试、新手引导重置。
- [ ] “减少动效”存储并立即生效；改窗口模式时不叠加翻页动画；重启后读取实际偏好。
- [ ] 原型自动保存文案仅用于真正即时保存的设置；服务器地址等仍需保存/测试的项目保留相应流程。

### K. 原型未覆盖但完整客户端不能遗漏的界面（P1/P2）

| 界面 | 已有落点 | 迁移要求 |
|---|---|---|
| Steam 登录、认证错误、服务器不可达 | DesktopShell/LoginRoot、SteamAuthController、DesktopServerConfig | 同风格登录卡/加载/错误重试；保留认证过程 |
| 新手引导与首次入塘 | DesktopOnboardingController、LocalOnboardingSession | 新入口纳入锁定，文案和高亮锚点跟随新布局，不可跳过必要流程 |
| 好友、好友申请、Steam 邀请、在线玩家、塘聊、私聊 | PanelSocial、DesktopSocialModalView 及各行预制体 | 完整换皮；保留可发现入口，铃铛不能只保留摆动而没有消息功能 |
| 玩家资料与社交操作 | PanelProfile/PanelProfileHub、OverlayPlayerSocialBridge | 统一进入新个人中心，保留只读权限与私聊回跳 |
| 离塘结算 | PanelPondSettlement、DesktopPondSettlementModalView | 纸页/鱼获卡/费用结算/确认关闭；真实结果与会话一致 |
| 右键菜单与功能弹窗 | ProductContextMenu、FeatureModalRoot、DesktopModalHost | 换皮及层级/遮罩/Escape/键盘焦点；内嵌页面不能残留旧版 |
| 主窗的钓鱼状态与返回入口 | PanelPond、DesktopShellUi、DesktopAppBootstrap | 适配主壳视觉，不破坏正在钓鱼和切换鱼塘的状态 |
| 游戏调试菜单 | DesktopGameplayDebugMenu | 仅开发构建检查路由/层级，不复制原型演示按钮到发行版 |

## 4. 共享布局与组件改造

### 4.1 坐标与缩放

- 设计坐标使用 1672×941，手账页 1370×650；页面内左栏 `(24,112,210,496)`，右栏 `(984,112,362,496)`。
- 中栏根据页型使用现有原型的标题/搜索区与内容区；原文档 `(254,164,710,432)` 与当前 CSS 个别 top/height 不同，落地以当前可运行原型逐页取值，整理一个统一 layout token 源，不再多处硬编码。
- `CanvasScaler.referenceResolution` 不随窗口预设改写；窗口预设只调整物理窗口。地图坐标/鼠标坐标/浮漂落点均转换到各自 RectTransform 空间。
- 先完成 1672×941 比例复刻，再验收四种真实窗口分辨率和 Windows DPI 缩放。浏览器验证过的 1280×800 不是 Unity 的 1280×720 验收替代。

### 4.2 共享鱼塘详情（建议新增 PondDetailView 与 PondEntryController，名称为建议）

视图结构统一为：`Photo / Name / Facts / AccessState / FeeSummary / EntryControls{ModeSell,ModeAuto,Enter}`。

当前原型的 362×496 右栏可作为起点：照片区在上，信息区约 top278 / height72，操作区 top358，档位约 top366 / height48，进入按钮约 top428 / height50。以上是界面局部坐标，不是屏幕坐标。

正式数据比原型更长：除鱼种/地点，还需要显示每 2h 价格、今日扣费次数、等级/禁入原因。必须固定操作区，信息区按需滚动、展开或收起次要说明；不能简单把全部文字塞进 72px，也不能沿用原型 `overflow:hidden` 截断关键收费信息。地图的重置视图和列表的地图查看放到左栏/地图工具区。

共同状态至少包含 `pondId / returnFeeMode / progressLoaded / accessReason / busy / error`。UI 免费/单档/双档判断共用；最终准入和扣费以服务端为准。入塘流程调用现有 `SetPendingReturnFeeMode → SwitchPond 或 ConnectAndJoin → 成功确认 → StartNativeOverlay`，不要复制两套逻辑。

### 4.3 组件与预制体

- [ ] 纸页/索引标签/贴纸卡/拍立得/胶带/印章/浮标/猫爪开关/空状态/加载/小鱼提示共用组件。
- [ ] GridLayoutGroup 控制外层占位，倾角、抬升、缩放作用于内层 Visual，避免布局组每帧抵消动画或触发重排。
- [ ] 卡片按业务 id 哈希固定倾角，池化时重绑 id；正文不随装饰倾斜超过 2°。
- [ ] 装饰 Image 关闭 raycastTarget；选中标识、禁用原因不能只靠颜色表达。
- [ ] 弹窗统一 ModalCanvas/遮罩/焦点恢复；MotionCanvas 与模态层排序明确，动效不能挡弹窗或穿透点击。
- [ ] 预制体序列化必需节点与组件；运行时允许实例化池化项目，不再临时生成基础页面布局。
- [ ] 通用 Library Variant、单页 prefab、DesktopShell 内嵌实例、FeatureModalRoot 内嵌实例都检查继承覆盖。

## 5. 动效与资源完整清单

### 5.1 动效缺口

| 动效 | 当前落点/状态 | 必做增量 |
|---|---|---|
| 按钮按压/回弹/禁用反馈 | CozyButtonVisual + CozyMotion 已有基础 | 对齐令牌、禁用原因、悬停和键盘；减少动效下不缩放 |
| 页面打开/关闭/翻页 | PanelRouter + CozyMotion.Flip 基础实现 | 纸页/活页环、中栏 stagger、右栏延迟淡入；快速切换取消与归位 |
| 导航浮漂 | 运行时 EnsureBobber，分两段位移 | 持久化 prefab、下蹲/抛物线/落地/水波，与唯一循环调度统一 |
| 卡片 | 未见完整 Sticker/Stagger 实现 | 稳定倾角、首次入场、悬停抬升、拉胶带、视口触发 |
| 地图 | 多个 Float、点击单水波、FocusPond 瞬移 | 一个选中浮标循环、双水波/爪印/平滑定位/入塘下沉 |
| 顶栏与通知 | 金币 Roll、基本 Toast | 金额增减反馈、经验/升级、铃铛短促提示、小鱼提示队列与错误猫 |
| 收获/购买/出售/装备/成就 | 未见完整演出组件 | 真实事件触发、可跳过、不重复播；普通收获与新鱼分流 |
| 角落猫 | 固定橘猫 Sprite + Float | 使用玩家皮肤；趴/抬头/跳/睡与偶发动作，预加载 |
| 加载/空状态 | 各页状态文字 | 统一浮漂加载与睡猫；超过 3 秒提示，取消/失败/重试 |
| 动效生命周期 | OnApplicationPause + Jobs 列表 | 页面隐藏/销毁/托盘暂停、恢复、job 上限、重复注册和资源池清理 |

共享令牌：Tap60ms、Snap220ms、Slide200ms、Hop420ms、Float2400ms、Celebrate900ms、Stagger40ms/最多8项；翻页旧页退场120ms、减动效淡变120ms等辅助值也集中管理。收获序列按原设计 1600ms/普通约1000ms，可点击跳过。

建议以现有 `CozyMotion` 为门面，增加序列取消句柄和唯一循环 owner；新增/拆出 Sticker、Stagger、PageFlip、BobberNav、Counter、Toast、Stamp、Celebration、CornerCat、Loading、Toggle 等预制体组件。不要另挂第二套按钮动画造成双重缩放。

当前宿主只监听 `OnApplicationPause`，未见 WindowManager 托盘隐藏直接调用暂停；Windows 隐藏窗口不应假设必定触发 Unity pause 回调。需要实接窗口/托盘生命周期，并只暂停主窗表现，不暂停后台钓鱼业务。

### 5.2 资源

逐项制作规格、现有137张PNG的复用/替换划分、30组资源与组件的尺寸/切图/状态要求，见[独立美术资源替换清单](/Users/davilje/fish-social/docs/design/ui/2026-10-21-cozy-journal-art-replacement.md)。实测原图尺寸、GUID及已有border见[资源盘点JSON](/Users/davilje/fish-social/docs/design/ui/2026-10-21-cozy-journal-art-inventory.json)。当前README和asset-manifest部分参数已过时，实施时须按实际导入资源重建。

原型 `prepare_assets.py` 明确说明图片是从 Unity 原图裁切/缩小的网页副本。正式客户端应复用 Unity 原始 Sprite 和目录，不把低分辨率网页 JPG 再导回去。

| 资源组 | 现状与工作 |
|---|---|
| 背景/地图/鱼/鱼饵/钓竿/船/头像/字体 | 已有 Theme/Cozy 资源；复用高分辨率原图、atlas 子图、FishCozyRounded-Medium 与字体许可证，核对裁切和透明边缘 |
| 鱼塘照片 | 建立 pondId→图片配置及 fallback；覆盖全部实际开放鱼塘，禁止按数组索引错配 |
| 纸页、活页环、索引、贴纸白边、拍立得、胶带三款 | 现有 paper 可复用，补原型 CSS 表现对应的 Sprite/9-slice；本次文件扫描未见完整 journal 目录交付 |
| 新/已装备/已收录印章、图钉、爪印 | 补透明 2×图或等效 uGUI 结构，保留可读文字与本地化能力 |
| 浮漂、水波、水花、金币粒子、小鱼提示 | 替代首次点击现场生成 Texture2D 的路径；预载、SpriteAtlas/对象池 |
| 睡猫与角落猫姿势 | 复用现有分层猫的合适帧；缺姿势补资源，同时覆盖实际玩家皮肤 |
| 声音 | 按压/落水/贴纸/翻页/盖章/金币保留统一接口与开关；可用素材和音量另验收，不以音频阻塞基础布局 |

建议新资源放 `Assets/Resources/Desktop/Theme/Cozy/journal/`；落实 Sprite/FullRect、透明、无 mipmap、Clamp、九宫格 border、Image.Type.Sliced。同步 CatTheme.SurfaceBorder、CozySpriteImporter 和 CozyPrefabRepair，避免“修复全部”后把新素材边框覆盖回旧值。

## 6. 真实业务接入与原型新增功能

| 项目 | 已核查现状 | 实施结论 |
|---|---|---|
| 买/装备/出售/回塘 | IAuthenticatedApiClient 已有相关方法 | 复用接口，成功响应驱动表现；任何经济数值不写 PlayerPrefs |
| 资料/展示柜/图鉴/相册 | GetProfileHub、UpdatePlayerProfile、SetShowcase、GetCodex、ChangeAlbumPin 已有 | 主要是换布局/绑定/刷新；保留权限、候选照片和相册容量 |
| 四榜及我的排名 | GetLeaderboard / GetMyLeaderboardRank 已有 | 保留服务端排序、周期与单位 |
| 点赞/评论/删评 | 已有 REST + Socket 事件 | 移动到新组件并确保订阅解除、请求世代与权限 |
| 鱼获分享与可见范围 | 后端 POST /api/posts 支持 visibility；Unity ShareFish 只暴露 fishId/nickname | 扩展客户端参数即可接“公开/好友”，服务端继续校验 |
| 自由文字动态/可不附鱼 | 当前 POST /api/posts 要求 fishId，createPost 从库存取鱼；未接收自由文字 | 若保留原型功能，扩展客户端 DTO/API、server/socialRoutes.ts 与 posts.ts 的输入校验和保存；表已有 text 不代表发布 API 已支持 |
| 我的动态 | Unity GetSocialFeed 仅 bool friendsOnly；已核查 wall/friends 路由无作者筛选参数 | 增加账号自己的分页查询；不能只过滤已加载一页公开流，否则会漏数据和好友可见动态 |
| 关注钓友 | 当前客户端是好友请求/接受/拒绝/删除关系，没有关注接口 | 推荐改为“加好友”接现有关系；如必须单向关注，则作为新增业务，设计存储/权限/接口/状态，不可用本地布尔值假冒 |
| 成就领取奖励 | 当前成就 DTO/服务主要记录解锁；客户端无领取 API | 推荐解锁式展现；如保留领奖，补奖励定义、领取账本/幂等、金币事务、重复提交保护及测试 |
| NEW 与首次收录演出 | 已有 CodexUnlocked、FishCatchSettled、InventoryUpdated 等事件 | 明确事件关联与去重键，隐藏主窗时排队、回到主窗仅播一次；不根据页面刷新或库存计数猜测新鱼 |
| 入塘与收费 | 已有进度、双收费档位、会话及 Overlay 入口 | 只改共享展示/选择与流程接线，保持权威判定、失败处理、换塘结算 |
| 设置偏好 | Window/Notification 等本地偏好已存在 | 本地只保存表现/窗口偏好，正式进度与社交状态走业务接口 |

“关注”“领奖”“自由文字/我的动态”是迁移中需要明确的产品差异，不能只做样式便宣称完全等价。建议默认保留现有业务语义并统一新外观；如果要求原型每个按钮逐一同功能上线，则相应后端增量也属于交付范围。

## 7. 文件级工作清单

下表列出已确认的主落点。标记含义：**改**=直接修改；**核**=调用/引用与回归检查，只有发现依赖时才改；**新**=建议新增，尚不存在。不以清单机械修改所有文件。

### 7.1 主壳与共用运行时代码

| 标记 | 文件 | 修改/核查内容 |
|---|---|---|
| 改 | [UI/DesktopShellUi.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShellUi.cs) | 新入口、分辨率、导航、页生命周期、顶栏与业务反馈；BuildPondSelection 接口调整 |
| 改 | [UI/PanelRouter.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/PanelRouter.cs) | 翻页、取消、当前页和点击锁一致性 |
| 核 | [UI/ShellPanelId.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/ShellPanelId.cs) | 已有 WorldMap/PondSelection 足够；新增弹窗不必新增顶级页 |
| 改 | [UI/CatTheme.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/CatTheme.cs) | 纸张/胶带/索引/九宫格与材质映射 |
| 核 | [UI/CozyUi.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/CozyUi.cs) | 公共组件创建/样式入口与新主题一致 |
| 改 | [UI/CozyPanelSkin.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/CozyPanelSkin.cs) | 纸页与面板皮肤，避免后处理覆盖 |
| 改 | [UI/CozyButtonVisual.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/CozyButtonVisual.cs) | 索引/按钮/选中态与手感、减少动效 |
| 核 | [UI/CozyGraphicOffset.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/CozyGraphicOffset.cs) | 图文位置与缩放反馈不重叠 |
| 改 | [UI/CozyItemArt.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/CozyItemArt.cs) | 鱼图/商品图/详情装配与新组件绑定 |
| 改 | [UI/CozyMotion.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/CozyMotion.cs) | 动效令牌、序列、资源预热、循环管理、取消与暂停 |
| 改 | [UI/DesktopModalUi.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopModalUi.cs) | 公用绑定、皮肤、关闭按钮与弹窗行为 |
| 改 | [UI/DesktopModalHost.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopModalHost.cs) | 新弹窗布局、层级、输入焦点与关闭 |
| 核 | [UI/DesktopFeaturePanelFactory.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopFeaturePanelFactory.cs) | 新视图映射、预制体路径及绑定校验 |
| 核 | [UI/DesktopUiPrefabFactory.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopUiPrefabFactory.cs) | 新可复用组件实例化与缺失处理 |
| 核 | [DesktopAppBootstrap.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs) | 主壳启动、入塘/回主窗、真实收获事件衔接 |

### 7.2 页面控制器

| 标记 | 文件 | 修改/核查内容 |
|---|---|---|
| 改 | [DesktopWorldMapPanel.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopWorldMapPanel.cs) | 浮标、平滑定位、共享详情与入塘 |
| 改 | [CozyPondSelectionPanel.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/CozyPondSelectionPanel.cs) | 搜索/筛选/照片卡、档位与准入一致性 |
| 核 | [WorldMapPondCatalog.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/WorldMapPondCatalog.cs) | 权威目录与图片映射扩展落点 |
| 改 | [DesktopCatchBagModalView.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopCatchBagModalView.cs) | 鱼获/道具、贴纸卡、右栏、成功反馈 |
| 改 | [DesktopShopPanel.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShopPanel.cs) | 商店与装备双模式、数量、主动作与表现 |
| 改 | [DesktopLeaderboardPanel.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopLeaderboardPanel.cs) | 领奖台/排名卡/玩家详情，保留缓存 |
| 改 | [DesktopProfileHubPanel.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopProfileHubPanel.cs) | 五分区、图鉴/相册/展示柜与只读权限 |
| 改 | [DesktopProfileEditPanel.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopProfileEditPanel.cs) | 编辑表单、头像/选鱼与保存回跳 |
| 核 | [DesktopProfilePanel.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopProfilePanel.cs) | 旧资料页调用迁移后再退役 |
| 核 | [DesktopGalleryModalView.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopGalleryModalView.cs) | 独立图鉴与个人中心的兼容路由 |
| 改 | [DesktopSocialFeedPanel.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopSocialFeedPanel.cs) | 三分类、发布入口、右侧详情/评论与分页 |
| 改 | [DesktopSocialPostCard.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopSocialPostCard.cs) | 卡片重排，评论从卡内迁移到右栏 |
| 改 | [DesktopSettingsModalView.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopSettingsModalView.cs) | 猫爪开关、四类设置、减少动效与保存状态 |
| 改 | [DesktopSocialModalView.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopSocialModalView.cs) | 好友/聊天/私聊等补全换皮与入口 |
| 改 | [DesktopPondSettlementModalView.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopPondSettlementModalView.cs) | 结算视觉与操作状态 |
| 改 | [DesktopProductMenuView.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopProductMenuView.cs) | 右键菜单视觉/焦点/路由 |
| 核 | [DesktopProductMenuAction.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopProductMenuAction.cs) | 保留已有快捷入口语义 |
| 核 | [DesktopGameplayDebugMenu.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopGameplayDebugMenu.cs) | 开发菜单层级与发行开关 |
| 核 | [DesktopDefaultAvatars.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/UI/DesktopDefaultAvatars.cs) | 玩家皮肤与头像映射 |

### 7.3 数据与生命周期

| 标记 | 文件 | 修改/核查内容 |
|---|---|---|
| 改 | [Auth/AuthenticatedApiClient.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Auth/AuthenticatedApiClient.cs) | 新 UI 数据绑定所需方法/参数，按第6节确定扩展 |
| 核 | [Auth/UnityNetworkDtos.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Auth/UnityNetworkDtos.cs) | 发布/领奖等扩展才改DTO，兼容既有响应 |
| 核 | [Auth/SocialPondSessionController.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Auth/SocialPondSessionController.cs) | 入塘档位、进度、结算/图鉴/库存事件去重 |
| 核 | [Auth/SocialSocketClient.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Auth/SocialSocketClient.cs) | 已有事件订阅，新增协议时再扩展 |
| 核 | [DesktopGameData.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/DesktopGameData.cs) | 道具/鱼塘/分类等静态目录权威来源 |
| 核 | [DesktopFishCatalog.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/DesktopFishCatalog.cs) | 实际鱼种全集与资源映射 |
| 改 | [Window/WindowManager.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Window/WindowManager.cs) | 托盘暂停/恢复主窗口动效，窗口模式联动 |
| 核 | [Window/WindowSettings.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Window/WindowSettings.cs) | 物理窗口预设与设计参考分辨率解耦 |
| 核 | [Window/BackgroundRenderGate.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Window/BackgroundRenderGate.cs) | 恢复后刷新与后台性能 |
| 核 | [Notifications/DesktopNotificationService.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Notifications/DesktopNotificationService.cs) | 系统通知与UI提示去重 |
| 核 | [Notifications/NotificationSettings.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Notifications/NotificationSettings.cs) | 偏好持久化与界面覆盖 |
| 改 | [Onboarding/DesktopOnboardingController.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Onboarding/DesktopOnboardingController.cs) | 新按钮锁定/引导定位与新布局接线 |
| 核 | [Onboarding/LocalOnboardingSession.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Onboarding/LocalOnboardingSession.cs) | 首次进入状态回归 |
| 核 | [OverlayPlayerSocialBridge.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/OverlayPlayerSocialBridge.cs) | 原生Overlay跳到新玩家页/私聊 |
| 核 | [NativeOverlayProcessController.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/NativeOverlayProcessController.cs) | 入塘成功启动、回主窗与退出 |

### 7.4 编辑器生成、修复、构建与场景

| 标记 | 文件 | 修改/核查内容 |
|---|---|---|
| 改 | [Scripts/Desktop/Editor/CozyRedesignAuthoring.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Editor/CozyRedesignAuthoring.cs) | 统一写入全部新布局、持久化组件、嵌套覆盖及检查 |
| 改 | [Scripts/Desktop/Editor/DesktopPrefabCatalog.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Editor/DesktopPrefabCatalog.cs) | 新增共享组件/动效/弹窗登记，核对Cozy前缀Library路径规则 |
| 改 | [Scripts/Desktop/Editor/DesktopPrefabBaker.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Editor/DesktopPrefabBaker.cs) | 旧结构断言与补齐入口同步，防止生成回旧版 |
| 改 | [Scripts/Desktop/Editor/CozyPrefabRepair.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Editor/CozyPrefabRepair.cs) | 新节点契约/材质导入/修复后持久性 |
| 改 | [Scripts/Desktop/Editor/CozySpriteImporter.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Editor/CozySpriteImporter.cs) | 新资源导入、九宫格与图集设置 |
| 改 | [Scripts/Desktop/Editor/CozyUiBuildGuard.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Editor/CozyUiBuildGuard.cs) | 新组件与节点完整性、Variant和滚动区检查 |
| 核 | [Scripts/Desktop/Editor/CozyThemeValidationMenu.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Editor/CozyThemeValidationMenu.cs) | 验证入口覆盖新资源 |
| 核 | [Scripts/Desktop/Editor/DesktopPrefabManagerWindow.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Editor/DesktopPrefabManagerWindow.cs) | 管理目录、预览、修复动作覆盖新组件 |
| 改 | [Scripts/Desktop/Editor/CozyRedesignPreview.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Editor/CozyRedesignPreview.cs) | 新页面/弹窗/状态截图样本 |
| 核 | [Scripts/Desktop/Editor/CozyFishCatalogPreview.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Editor/CozyFishCatalogPreview.cs) | 全鱼种素材兼容 |
| 核 | [Scripts/Desktop/Editor/DesktopBuildMenu.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Editor/DesktopBuildMenu.cs) | 构建前执行完整UI校验 |
| 核 | [Scenes/DesktopMain.unity](/Users/davilje/fish-social/fish-social-unity/Assets/Scenes/DesktopMain.unity) | 入口场景和引导/壳引用，无旧UI重复根 |
| 改 | [Tests/Editor/CozyRedesignLayoutTests.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Tests/Editor/CozyRedesignLayoutTests.cs) | 新布局/新入口/共享详情断言 |
| 改 | [Tests/Editor/CozyPrefabPersistenceTests.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Tests/Editor/CozyPrefabPersistenceTests.cs) | 保存重载、Variant、生成/修复后保持 |
| 改 | [Tests/Editor/CozyUiLibraryTests.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Tests/Editor/CozyUiLibraryTests.cs) | 组件、九宫格、命中与导入 |
| 核 | [Tests/Editor/CozyDesktopThemeTests.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Tests/Editor/CozyDesktopThemeTests.cs) | 主题和资源回归 |
| 核 | [Tests/Editor/CozyFishCatalogTests.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Tests/Editor/CozyFishCatalogTests.cs) | 图鉴/鱼图资源回归 |
| 核 | [Tests/Editor/DesktopProtocolResponseTests.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Tests/Editor/DesktopProtocolResponseTests.cs) | 接口扩展时补响应与失败测试 |

### 7.5 预制体资产清单

下面所有资产都需逐个检查保存后的最终结果，不能只验证运行时画面。

- **主壳/页面**：[DesktopShell](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/DesktopShell.prefab)、[PanelHome](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelHome.prefab)、[PanelPond](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelPond.prefab)、[PanelWorldMap](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelWorldMap.prefab)、[PanelPondSelection](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelPondSelection.prefab)、[PanelCatch](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelCatch.prefab)、[PanelEquipment](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelEquipment.prefab)、[PanelShop](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelShop.prefab)、[PanelAchievements](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelAchievements.prefab)、[PanelLeaderboard](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelLeaderboard.prefab)、[PanelProfileHub](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelProfileHub.prefab)、[PanelProfileEdit](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelProfileEdit.prefab)、[PanelSocialFeed](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelSocialFeed.prefab)、[PanelSettings](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelSettings.prefab)、[PanelSocial](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelSocial.prefab)、[PanelPondSettlement](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelPondSettlement.prefab)。
- **兼容旧入口与弹窗**：[PanelProfile](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelProfile.prefab)、[PanelGallery](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PanelGallery.prefab)、[FeatureModalRoot](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/FeatureModalRoot.prefab)、[ProductContextMenu](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/ProductContextMenu.prefab)。
- **页面项目**：[PondSelectionCard](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PondSelectionCard.prefab)、[WorldMapPondMarker](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/WorldMapPondMarker.prefab)、[CatchSlot](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/CatchSlot.prefab)、[ShopItemCard](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/ShopItemCard.prefab)、[AchievementRow](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/AchievementRow.prefab)、[LeaderboardRow](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/LeaderboardRow.prefab)、[ShowcaseSlot](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/ShowcaseSlot.prefab)、[AvatarChoice](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/AvatarChoice.prefab)、[GallerySpeciesSlot](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/GallerySpeciesSlot.prefab)、[SocialPostCard](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/SocialPostCard.prefab)、[PostCommentRow](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PostCommentRow.prefab)、[TextStatusRow](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/TextStatusRow.prefab)。
- **现有社交项目**：[FriendRow](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/FriendRow.prefab)、[FriendRequestRow](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/FriendRequestRow.prefab)、[SteamInviteRow](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/SteamInviteRow.prefab)、[OnlinePlayerRow](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/OnlinePlayerRow.prefab)、[PondChatMessageRow](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/PondChatMessageRow.prefab)、[DirectMessageConversationRow](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/DirectMessageConversationRow.prefab)、[DirectMessageRow](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/DirectMessageRow.prefab)。
- **组件库**：[Library](/Users/davilje/fish-social/fish-social-unity/Assets/Resources/Desktop/Prefabs/Library) 下所有按钮、面板、输入框、滚动区 Variant 与 CozyPageHeader；新共享详情/模态/动效资源纳入相同继承和验证约定。

建议新增（尚不存在，不是已完成文件）：共享鱼塘详情、统一入塘控制器、贴纸卡视觉、拍立得、索引标签、猫爪开关、提示/加载/空状态、收获层、角落猫、选鱼/发布/玩家卡弹窗，以及动效调度与事件队列。具体类/资源名在实现时确定，避免与已有 CozyMotion/CozyButtonVisual 重复职责。

### 7.6 有条件才改的服务端

- [socialRoutes.ts](/Users/davilje/fish-social/server/src/socialRoutes.ts)：自由文字/我的动态/领奖等实际新增功能的 API、认证、校验与返回。
- [posts.ts](/Users/davilje/fish-social/server/src/posts.ts)：发布内容、作者筛选、分页与可见性；不靠客户端过滤全量动态。
- [achievements.ts](/Users/davilje/fish-social/server/src/achievements.ts)：若保留领奖，补权威领取流程；已有解锁流程继续复用。
- [profileHub.ts](/Users/davilje/fish-social/server/src/profileHub.ts)：若响应需新增领奖状态/社交字段等，扩展兼容返回。
- `server/src/migrations/`、`shared/` 对应类型、相关服务端测试：仅确需新增存储/协议时改。新增关注/领奖数据结构需要迁移，不能通过 UI 本地保存替代。

WPF 的 `desktop-overlay/`、Unity 的 OverlayHud/OverlayPondLayoutBaker/OverlayHudExporter 本次以联动回归为主，不做本方案的主窗口换皮。共享资源修改前检查导出链，避免间接改变原生 Overlay。

## 8. 实施顺序与交付物

| 阶段 | 工作包 | 进入下一阶段的条件 |
|---|---|---|
| 0 基准冻结 | 逐页截图/控件状态表；统一原型与现有业务差异；保存当前Unity工作区基线 | 确定设计画布、数据语义和兼容入口，完整记录待新增业务 |
| 1 基础体系 | 参考分辨率、Theme/Library、主壳、世界入口、页面与模态层、资源预热 | 首页和所有入口在四档窗口可用；无旧布局覆盖 |
| 2 选塘闭环 | 共享详情、地图浮标、列表筛选、档位与准入、真实入塘/失败/返回 | 两入口选择一致，长信息不挡操作，服务端确认后进入Overlay |
| 3 常用页面 | 背包、装备、商店、资料/展示柜/图鉴/相册 | 真实数据与写操作闭环，状态同步和失败恢复正确 |
| 4 社交与设置 | 排行榜、成就、动态、好友/聊天、登录/引导/结算、设置与兼容弹窗 | 原有功能可达，新业务按第6节决策接入完成 |
| 5 完整表现 | 贴纸/胶带/拍立得、全套动效、小猫、提示/加载/空态、收获队列 | 单循环/减少动效/快速取消/后台恢复与性能达标 |
| 6 发布验收 | 预制体保存重载、修复工具回归、EditMode/PlayMode/Windows Player、截图对照 | 无缺引用/旧页残留/数据错误；完成验收记录 |

每阶段交付：修改过的 C#、已保存 prefab/.meta、资源及映射、对照截图、验证结果与未解决项。业务 API 变更另附协议与迁移测试。完成全部阶段后才能称为“完整替换”；只完成前几步应明确仍是部分接入。

## 9. 验收清单

### 视觉与布局

- [ ] 首页/关闭手账/所有10页/所有分类/编辑与发布等弹窗截图对照当前原型；保留功能另补同风格截图。
- [ ] 四档真实窗口分辨率、Windows DPI、全屏/无边框、重开窗口；字体清晰，无按钮和文本超框。
- [ ] 顶栏世界入口可达、不挤金币；地图和列表照片、地点、费用、档位、进入按钮不重叠。
- [ ] 用最长地点/费用/状态文案、大金币数、长昵称、大量鱼种测试；关键费用与禁入原因不截断。
- [ ] 不出现预览工具栏、样例玩家、硬编码金币、模拟成功、本地演示按钮或旧皮肤入口。

### 交互与业务

- [ ] 鼠标、滚轮、Tab/Enter/Escape、中文输入；输入文字时不触发全局快捷键，弹窗焦点可恢复。
- [ ] 连续快速切页/切分类/关闭重开/退出时取消动画和过期网络请求，无幽灵页或多次监听。
- [ ] 两种收费档位分别从地图和列表入塘；免费/未开放/低等级/新手未完成/断线/切塘失败全覆盖。
- [ ] 购买、装备、出售、回塘、资料保存、展示柜、相册、动态操作：成功/失败/重复点/超时/权限均验证。
- [ ] 好友、私聊、邀请、结算、引导与设置功能不丢失；切页不结束钓鱼，关手账回首页不退出会话。
- [ ] API返回才更新权威经济数据；领奖如启用必须验证幂等；发布范围不能泄露好友动态。

### 动效、性能与持久化

- [ ] 同屏持续循环不超过一个，地图选中/展示鱼/空状态/导航有明确优先级；猫眨眼等偶发动作有节流。
- [ ] 减少动效打开时立即停循环和进行中的位移/缩放/旋转，恢复正确位置，只保留短淡变。
- [ ] 首次收获与普通收获只播一次，可跳过；隐藏主窗排队不抢焦点、不影响钓鱼与余额正确性。
- [ ] 托盘隐藏停止主窗动画，恢复不积累时间导致跳跃；背景业务继续工作。
- [ ] 以原设计预算为目标：活动动画任务≤64，切页额外主线程开销<2ms，无新增 frame_stall；用Profiler/Player.log证实，不能仅凭截图。
- [ ] EditMode：CozyRedesignLayout/PrefabPersistence/UiLibrary/DesktopTheme/FishCatalog；接口变化跑ProtocolResponse及相关后端测试。
- [ ] 补必要PlayMode测试：路由竞态、循环调度、共享选塘状态、减少动效、弹窗焦点与真实事件去重。
- [ ] 执行保存、卸载重载、Repair/Authoring/BuildGuard 后再检查，不能重生成回旧UI；Windows构建和原生Overlay联动通过。

## 10. 审计依据与限制

审计读取了主壳、路由、各页面控制器、主题/动效门面、API/DTO、选塘会话接口、作者工具、构建守卫、已有布局/预制体测试、原型源码及相关服务端路由。代码中已经存在的能力按“复用/改造”列出，建议新增的组件和业务明确标注。

参考设计 [猫咪钓鱼手账方案](/Users/davilje/fish-social/docs/design/ui/2026-10-21-cozy-journal-motion.md) 与 [STEAM-DESKTOP-21 现有规格](/Users/davilje/fish-social/docs/planning/specs/Steam桌面端-21猫咪钓鱼手账动效.md) 是不同覆盖程度：后者只接入基础手感，明确把胶带换皮、完整收获和音效放在后续。本次“完整替换”需要覆盖前者加最新原型扩展，不能用旧规格的部分完成代表全量完成。

本清单基于静态代码与资源检查；没有把浏览器原型测试当作 Unity 验收，也没有断言当前 Player 中每个页面的实际显示完全等同保存资产。执行阶段应首先生成当前 Unity 截图作基准，再按第9节逐项验收。
