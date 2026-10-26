# 原版猫咪钓鱼手账 UI：Unity 完整替换清单

更新日期：2026-10-21。实施基线为 Git HEAD `d57a7dd0` 与本工作区的原版预览、共享资源和现有业务。本轮已将正式 Unity 页面、组件及业务接入手账主题。

**当前状态：正式页面替换已实施，Unity 自动化与渲染证据见[实施记录](2026-10-21-cozy-journal-unity-implementation.md)。Windows 发布包、Steam 真账号联机、DPI 与最终美术效果仍需平台验收。** 下文保留原始全量验收要求；`[x]` 表示本轮已有实现和检查依据，`[ ]` 表示整项尚未完成验收，不能把已有资源或截图自动等同业务通过。

### 本轮正式接入

| 范围 | 已实施 |
|---|---|
| 主壳和全部现有页面 | 正式 Resources/Desktop/Prefabs 及 Library 迁移共享 journal 资源；原生控件，保留 Variant/嵌套关系；新增常驻世界入口 |
| 地图和选塘 | 共享 PondSelectionState、PondDetailView；真实场景照片、分类/搜索、等级/费用、双档、固定入塘按钮、失败重试；同塘换档走真实结算重入 |
| 背包/装备/商店 | 真实道具页、装备/购买互斥、鱼获出售/回塘/分享；账号隔离 NEW 已读、贴入动画、成功印章/失败反馈 |
| 个人中心/成就/排行榜 | 保留五个个人子页、权限、编辑保存；真实成就分类；排行点击玩家卡及资料跳转 |
| 动态 | 公开/好友/我的分页、右栏评论、Socket 保留输入草稿；正式发布弹窗选择鱼获、160字心情、公开或好友可见，服务端验证和保存 |
| 动效/设置 | 可取消切页、同屏唯一持续循环、隐藏暂停、减动效立即归位、皮肤姿势、普通/新鱼庆祝；窗口/通知/服务器/托盘四类设置 |
| 持久化与构建 | canonical prefab 别名保存、防旧修复器覆盖新状态、PPU/insets/Multiply 校验；发布弹窗基础布局保存到 prefab |

正式 Unity 截图浏览：[production/index.html](../../../../../art/cozy-journal/production/index.html)。截图由正式预制体配合明确的编辑器样例数据渲染，不是真实账号联机录像，也不使用 JournalSamples 的参考贴图。

## 1. 当前交付状态与替换范围

### 1.1 资源准备基线与验收边界

| 交付层 | 当前事实 | 后续工作 |
|---|---|---|
| 原版交互预览 | 10 个页面；包含常驻世界入口、鱼塘详情与档位分区修正 | 作为视觉/交互基准，演示数据与模拟业务不移植 |
| 共用 UI 美术 | 90 张 PNG，其中 50 项按原版 CSS/SVG 校正，40 项为补充设计或状态 | 逐项接入正式组件；补充项结合实际页面确认 |
| 猫咪姿势 | 10 款皮肤 × 6 姿势，共 60 张透明图 | 新姿势待视觉确认；默认猫继续以原版造型为准 |
| 鱼塘图片 | 21 个实际场景 prefab 的渲染图，1024×640 | 按 pondId 映射并确认裁切；资源数量不等于实际开放鱼塘数 |
| 主题目录 | 原有 137 张 PNG 保留，新增 journal 171 张，共 308 张 | 复用原始鱼、物品、地图、头像和字体；依实际引用决定旧图退役 |
| Unity 页面样板 | 11 个独立 prefab，覆盖 10 页及设置第二个状态；原生 Image/Text 和按钮命中区 | 证明参考画面可在 Unity 渲染；按钮未接正式业务，不覆盖全部子页/弹窗 |
| 组件样板 | 12 个独立组合 prefab；纸纹 Multiply 材质及 shader | 迁入正式 Library、补状态和生命周期 |
| 正式客户端 | 正式 uGUI 页面、共享主题、控制器与发布接口已替换 | 平台/联机/视觉验收见实施记录；不以参考样板代替 |

- [x] 原版预览补齐页面，修正主界面地图入口和鱼塘操作分区。
- [x] A01–A30 资源组有交付来源；171 张资源有参数表、源文件或场景来源。
- [x] 已有资源尺寸、透明通道、GUID、PPU、九宫格和源文件检查记录。
- [x] 已有 Unity 导入、材质、字体、样板保存重载和 11 状态渲染记录。
- [x] 已有浏览器资源页、对照页和交互检查记录。
- [ ] 40 项补充 UI、60 张角色姿势和 21 张场景图完成实际使用效果的视觉确认。
- [ ] 正式页面、子页、弹窗、业务、动效及 Windows 发布验证完成。

| 用途 | 当前权威来源 |
|---|---|
| 原版交互基准 | [原版预览](../../../../../design/ui/prototype/cozy-journal/index.html)及同目录 CSS/JS |
| 原版 / Unity / 差异对照 | [compare.html](../../../../../art/cozy-journal/compare.html) |
| 171 张资源浏览 | [资源画廊](../../../../../art/cozy-journal/index.html) |
| 交付边界、源文件及重建顺序 | [资源 README](../../../../../art/cozy-journal/README.md) |
| A01–A30 数量与接入状态 | [coverage.json](../../../../../art/cozy-journal/coverage.json)，productionBinding 已按实际引用更新；不等同视觉验收 |
| 共享切图参数 | [ui-manifest.json](../../../../../../fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/journal/ui-manifest.json) |
| 角色/鱼塘映射 | [character-manifest.json](../../../../../../fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/journal/character-manifest.json)、[pond-manifest.json](../../../../../../fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/journal/pond-manifest.json) |
| 实际 Unity 导入信息 | [asset-manifest.json](../../../../../../fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/asset-manifest.json)：尺寸、GUID、fileID、Sprite rect |
| 参考层、布局和原版源文件 SHA256 | [layouts.json](../../../../../../fish-social-unity/Assets/Desktop/JournalSamples/ReferenceAssets/layouts.json) |

当前 11 个对照状态为：商店/钓竿、地图/选中鱼塘、设置/窗口显示、设置/通知偏好、背包/鱼获、装备/钓竿、成就/全部、排行榜/今日大鱼、个人信息/资料、钓友动态/公开动态、选塘/全部鱼塘。**其他分类、首页收起态、登录、长列表、编辑与发布弹窗、加载/错误状态不能用这 11 张图代替验收。**

### 1.2 替换目标与范围

视觉和交互基准是 [已补齐的原版原型](/Users/davilje/fish-social/docs/design/ui/prototype/cozy-journal/index.html)，包含 `style.css / app.js / pages.css / pages.js`，以及最新两项修正：主界面常驻「钓鱼世界」入口、鱼塘详情与收费档位分区。不是另做的 v2 方案，也不是原型工具栏中的“现状”截图。

目标为 Unity 桌面主窗口的完整 UI 替换：外壳、10 个原型页面、子页、弹窗、提示、动效、状态和资源；同时把原型未覆盖的现有功能纳入同一视觉体系，避免旧页面残留或功能丢失。沿用 Unity 2021.3.29f1c1、uGUI、现有业务控制器和真实数据接口。

浏览器原型的演示工具栏、速度控制、空格对比、示例数据、本地保存和模拟入塘成功页不进入正式客户端。皮肤、数量、等级、金币、库存、鱼塘费用均使用实际数据。

WPF 原生 Overlay 是另一套渲染系统。按原需求文档，本次主窗口方案不重做其 960×560 钓鱼画布，但必须验收入塘、托盘、回主窗、结算和社交跳转的衔接。若“完全替换”也包含 WPF 场景本身，需要另补对应场景与 HUD 的设计稿，不能用主窗口原型直接代替。

## 2. 实施前关键差异（整改依据）

| 优先级 | 当前代码事实 | 完整替换必须处理 |
|---|---|---|
| P0 | `TryLoadShellCanvas()` 初始化使用窗口默认尺寸和 MatchWidthOrHeight；`EnterMainShell()` 已设置 1672×941，但使用 Expand；参考样板为 MatchWidthOrHeight、match=0.5 | 统一登录→主壳缩放策略，设计坐标与物理窗口解耦，核查四档窗口和 DPI；不能笼统认为主壳仍按 1920×1080 布局 |
| P0 | 新样板位于 `Assets/Desktop/JournalSamples/`；319 张 ReferenceAssets PNG 为逐元素参考层 | 将共享资源与原生控件迁入正式 Library/页面，不能把参考层直接作为完整运行时方案 |
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

以下是完整验收口径；代码实现见上方接入表和实施记录。涉及多种异常、Windows 或视觉确认的复合条目保留待验收，不将部分检查当作整项完成。

### A. 主界面、顶栏、导航与页面容器（P0）

涉及：`DesktopShellUi`、`PanelRouter`、`DesktopAppBootstrap`、`DesktopShell.prefab`、`PanelHome.prefab`、`PanelPond.prefab`、`CozyRedesignAuthoring.Shell/Common`。

- [ ] 保留樱花背景、头像/昵称/等级/经验、金币/鱼获、铃铛和设置，按原型重新排列；数字支持千分位和大数。
- [x] 顶栏新增常驻「钓鱼世界 / 探索地图·选择鱼塘」，首页和手账页均可点击；关闭手账后依旧存在。
- [ ] 底部固定七入口：背包、装备、商店、成就、排行榜、个人信息、钓友动态；地图/设置不是额外挤入底栏。
- [ ] 统一手账纸页、三枚活页环、标题、点缀色、左栏索引、卡片区、右详情、右上关闭按钮。
- [ ] 关闭手账回首页，不退出当前鱼塘会话；切普通页不触发 leave_pond。地图与列表互跳、资料编辑返回均保留合理选择与滚动位置。
- [x] 修正 `AlignHeaderActions` 等运行时代码，避免覆盖新布局；去重旧 Header/Home/Leaderboard 等重复入口。
- [x] 切页采用可取消的过渡，只保留一个活动页面和一个导航浮漂；过渡时禁重复业务提交，完成/取消都恢复 raycast 和 alpha。
- [ ] 玩家名字/猫皮肤/金币/经验/库存变化同步顶栏；首次加载、断线和失败显示真实状态，不瞬间展示演示数值。

### B. 世界地图（P0）

涉及：`DesktopWorldMapPanel`、`WorldMapPondCatalog`、`DesktopGameData`、`PanelWorldMap.prefab`、`WorldMapPondMarker.prefab`。

- [ ] 地图采用贴在纸上的照片与四角胶带，保留拖动、滚轮缩放、边界约束；重置视图放地图工具区或左栏，避免占右栏入塘操作空间。
- [ ] 补齐原型分类图例：野外、水库、进阶、老手、巨物、禁区、未解锁；分类色、锁图标、开放状态与数据一致。
- [ ] 将矩形标记改为浮标：固定命中区、悬停名称/放大、选中爪印与两圈水波、350ms 平滑定位；未解锁标记仍可看详情但不漂。
- [x] 右栏用共享鱼塘详情组件；补真实鱼塘图片映射，按 pondId 寻图，不能用列表序号轮换同一张照片来冒充真实场景。
- [x] 左栏增加“列表选塘”，列表“地图查看”返回时聚焦同一鱼塘，保持档位、缩放和可预期的地图位置。
- [ ] 入塘时禁重复点击、显示连接状态；服务端确认成功后完成浮标下沉演出并衔接原生 Overlay。失败恢复按钮、档位和选择，不先显示成功。

### C. 选塘列表（P0）

涉及：`CozyPondSelectionPanel`、`DesktopShellUi.BuildPondSelection`、`PanelPondSelection.prefab`、`PondSelectionCard.prefab`。

- [x] 增加全部、可进入、免费鱼塘筛选；增加名称/分类/水域搜索，空结果与清除条件入口。
- [ ] 卡片按原型重排为图片、名称、地点/水域、鱼种、门槛和费用；整卡选择与按钮操作分清。
- [x] 不硬编码“20 个”：数量、显示资格、名称与 `WorldMapPondCatalog.LoadVisible()` 一致；免费筛选按真实费用语义定义。
- [x] 添加原型右栏档位选择，地图/列表共用状态与计费展示。无双档的鱼塘展示实际单档说明，不保留无意义的切换按钮。
- [x] 列表刷新保留有效选中项；搜索结果排除当前选中项时，明确清空或选中首项，不显示与列表无关的旧详情。
- [ ] 卡片首次进入可见区时贴入，回滚不重播；分页/过滤新项重新判断，复用与销毁时清理动画。
- [ ] “到底了”提示不遮卡片；鼠标滚轮、拖动、卡片选择不互相抢事件。

### D. 背包（P1）

涉及：`DesktopCatchBagModalView`、`PanelCatch.prefab`、`CatchSlot.prefab`、`CozyItemArt`。

- [ ] 左栏鱼获/道具分类、贴纸鱼卡、稳定倾角、NEW 角标、选中胶带、右侧拍立得鱼图及尺寸/稀有度/来源信息。
- [x] 现有背包主要绑定鱼获，补道具分页的数据来源和真实数量；从现有装备/鱼饵库存接口核对，不造本地库存。
- [ ] 保留出售、分享、符合条件时回塘，以及当前档位下的禁用原因；删除/出售选中鱼后同步展示柜等引用状态。
- [x] 新获得/已查看标记确定账号级存储策略；UI 浏览状态可本地存，真实库存和售卖结果由服务端决定。
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
- [x] 原型“领取奖励”不直接本地加金币。若保留，须补服务端奖励配置、领取与幂等记录；否则改为实际支持的“已解锁”呈现。

### G. 排行榜（P1）

涉及：`DesktopLeaderboardPanel`、`PanelLeaderboard.prefab`、`LeaderboardRow.prefab`。

- [ ] 保留已有四榜 API、缓存、分页/分帧生成与我的名次，按原型改前三名领奖台、行选中及右侧钓友卡。
- [ ] 榜单值按对应指标与单位显示，不把原型本地排序逻辑移到客户端重新计算正式名次。
- [ ] 玩家卡进入他人资料，接好友申请/私聊等实际能力；“关注”另见第 6 节。
- [ ] 验证不足三人、空榜、自己未上榜、未入塘时当前塘榜不可用、缓存刷新失败和超长昵称。

### H. 个人信息与五个子页（P1）

涉及：`DesktopProfileHubPanel`、`DesktopProfileEditPanel`、`PanelProfileHub.prefab`、`PanelProfileEdit.prefab`、`ShowcaseSlot.prefab`、`AvatarChoice.prefab`、`GallerySpeciesSlot.prefab`。

- [x] 资料：头像/名字/签名/等级经验/统计卡，编辑入口，自己和他人的权限区分。
- [x] 编辑：昵称/签名、头像选择、保存/取消、错误提示、长度限制；取消不写入，保存成功才更新顶栏和其他页面。
- [ ] 展示柜：三个槽位、已选鱼详情、选鱼弹窗、清空/替换；对接现有 SetShowcase，清空展示位不等于出售鱼。
- [x] 图鉴：使用实际全量鱼种，已收录图片与未收录剪影、记录明细；原型的 12/15 张示例不能作为正式目录上限。
- [ ] 相册：双拍立得、翻页、候选照片、钉入/取下、数量上限、网络图片加载与失败占位；沿用 album pins/candidates 真实语义。
- [ ] 成就：复用成就数据/组件；展示柜仅当前鱼摆尾，暂停导航和猫等其他持续循环。
- [x] 他人资料为只读，保留好友/私聊等现有入口，禁止出现可编辑自己库存的按钮。
- [ ] 检查旧 `PanelProfile` 与独立 `PanelGallery` 的调用，完成兼容路由后再决定是否删除，避免旧入口露出旧皮肤。

### I. 钓友动态（P1）

涉及：`DesktopSocialFeedPanel`、`DesktopSocialPostCard`、`PanelSocialFeed.prefab`、`SocialPostCard.prefab`、`PostCommentRow.prefab`。

- [x] 公开/好友/我的三个分类、可滚动动态卡、点赞、作者跳转、右栏详情和评论输入，按原型布局改造。
- [x] 当前评论在卡片内展开，需迁移到右栏并保持 postId 绑定；切帖子取消/丢弃旧请求，避免旧评论串页。
- [x] 复用点赞、评论、删自己评论、Socket 更新、分页和加载更多；保留原有权限与失败回滚。
- [x] 添加发布弹窗：真实鱼获选择、可见范围、文字、提交状态；“文字”和“我的动态”存在接口差异，按第 6 节补齐。
- [ ] 切分类/返回帖子保持合理滚动与选择；图片失败、无好友动态、无自己的动态均有明确状态。

### J. 设置（P1）

涉及：`DesktopSettingsModalView`、`PanelSettings.prefab`、`WindowManager/WindowSettings`、通知设置。

- [ ] 沿用原型索引、猫爪拨钮、选中盖章，保留真实窗口模式与 1280×720、1600×900、1920×1080、2560×1440 四档。
- [ ] 原型只有三个分类，Unity 另有“服务器连接”；保留该能力，建议作为第四个同风格分类，不因照搬原型删掉。
- [ ] 覆盖通知总开关、免打扰、咬钩/好友/连接错误通知、托盘行为、退出游戏、服务器保存/测试、新手引导重置。
- [x] “减少动效”存储并立即生效；改窗口模式时不叠加翻页动画；重启后读取实际偏好。
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
- [x] 装饰 Image 关闭 raycastTarget；选中标识、禁用原因不能只靠颜色表达。
- [ ] 弹窗统一 ModalCanvas/遮罩/焦点恢复；MotionCanvas 与模态层排序明确，动效不能挡弹窗或穿透点击。
- [ ] 预制体序列化必需节点与组件；运行时允许实例化池化项目，不再临时生成基础页面布局。
- [ ] 通用 Library Variant、单页 prefab、DesktopShell 内嵌实例、FeatureModalRoot 内嵌实例都检查继承覆盖。

### 4.4 从参考样板迁移到正式组件

当前实现链为：原版 DOM/CSS 导出视觉层与布局 → 编辑器生成 Unity Image/Text/命中区域 → Unity 渲染截图 → 同尺寸对照。它不在运行时解析网页，也未接入 WebView。正式页面现使用共享资源与业务控件，此段保留参考样板的制作链说明。

- [ ] 使用 `ReferenceAssets/layouts.json` 校对位置、颜色、文字、阴影和裁切；正式界面使用 `journal/ui-manifest.json` 的共享资源、原有高分辨率 Sprite 和正式组件。
- [x] 319 张参考 PNG 保留对照用途；部分旋转、裁切和效果已随导出状态固定，不能假定拉伸后仍正确。正式页面不依赖整页截图或逐层参考贴图替代控件。
- [ ] 将命中区换成有真实状态和导航关系的 Button/Toggle/InputField/ScrollRect；输入、中文 IME、滚轮、焦点和键盘另做交互验证。
- [ ] 使用项目字体动态绑定昵称、数量、价格和状态；定义行高、换行、溢出、最小字号和禁用颜色，不沿用参考导出时的固定文本高度处理所有数据。
- [ ] 将示例列表改为真实内容布局、滚动/遮罩和按需复用；大量数据、网络图片延迟、分类切换不遮盖固定操作区。
- [ ] 先以正式资源实现完整纸页、商品卡和鱼塘详情，逐区确认圆角、阴影、纸纹、字体及长内容，再扩展全部页面。
- [x] 新组件进入正式 prefab 目录及生成、修复、构建检查，保存重载后仍成立；不只修改 `JournalSample-*`。
- [x] 正式接入后另产出运行时截图及交互记录，标明“正式页面”，与现有参考样板证据区分。

## 5. 动效与美术资源替换清单

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

### 5.2 美术资源现状与逐组工作

[独立美术规格](2026-10-21-cozy-journal-art-replacement.md)保留 A01–A30 的制作要求；[早期盘点](../../../../../design/ui/2026-10-21-cozy-journal-art-inventory.json)只记录原有 137 张资源。当前资源是否存在、导入参数与校正状态，以第 1 节的 manifest 和 coverage 为准。根目录 asset-manifest 已按实际 AssetDatabase 重建。

下表“已校正”只统计按原版几何/样式生成或校正的 UI 条目，不表示整个组件已获视觉认可或正式接入。本轮已接入正式主题；具体引用与仍作为备用的补充资源见 coverage.json，下面保留各组原定检查要求。

| 组 | 交付内容 | PNG 数 | 已校正 | 仍需处理 |
|---|---|---:|---:|---|
| A01 | 纸页、纸边、纸影 | 3 | 3 | 正式纸页九宫格与阴影外延 |
| A02 | 纸点、纸纹、缝线 | 3 | 2 | 缝线待确认；纸纹用 Multiply |
| A03 | 活页环 | 1 | 1 | 三环定位及页面过渡层级 |
| A04 | 左索引底、右详情底 | 2 | 2 | 窄屏与长内容适配 |
| A05 | 索引标签四状态 | 4 | 3 | disabled 待确认；选中与焦点接入 |
| A06 | 普通/主/危险按钮、焦点框 | 13 | 8 | 危险按钮四状态、焦点框待确认 |
| A07 | 顶栏胶囊、世界入口底板 | 2 | 2 | 常驻入口与动态数值布局 |
| A08 | 底栏板与边缘 | 2 | 2 | 七入口与浮漂遮挡关系 |
| A09 | 贴纸卡、阴影、选中框 | 4 | 4 | 倾角/抬升不影响布局与命中 |
| A10 | 三款胶带 | 3 | 3 | 对齐原版尺寸与透明度 |
| A11 | 拍立得框与阴影 | 2 | 2 | 照片独立裁切、文字可编辑 |
| A12 | 图钉 | 1 | 0 | 造型与相册使用效果待确认 |
| A13 | 印章环、印记、NEW 角标 | 3 | 2 | stamp-mark 待确认；文案用原生文本 |
| A14 | 浮漂整体及分层 | 4 | 4 | 定位、颜色、加载与导航动画 |
| A15 | 水波、爪印 | 2 | 0 | 形状与动效效果待确认 |
| A16 | 开关轨道、旋钮、推爪 | 3 | 2 | paw-push 待确认；真实 Toggle 语义 |
| A17 | 进度轨道、填充、经验泡泡 | 3 | 2 | xp-bubble 待确认；真实数值绑定 |
| A18 | 输入框五状态 | 5 | 2 | error/disabled/readonly 待确认；IME 与焦点 |
| A19 | 模态纸、遮罩、关闭图形 | 3 | 1 | 遮罩/关闭图形待确认；焦点与层级 |
| A20 | 空篮前后层 | 2 | 0 | 造型待确认；与猫的遮挡关系 |
| A21 | 十皮肤六姿势 | 60 | — | 补充姿势视觉确认、皮肤与姿势映射 |
| A22 | 便笺提示、错误提示、小鱼分层 | 4 | 2 | 鱼身/鱼尾待确认；队列与错误语义 |
| A23 | 水滴、水花、金币闪点、纸屑 | 8 | 0 | 外形与演出确认、粒子数量、对象池 |
| A24 | 稀有闪光、奖励放射光 | 2 | 0 | 演出确认、减少动效 |
| A25 | 排名徽章、领奖台 | 2 | 1 | 领奖台待确认；不足三人状态 |
| A26 | 分类签、锁遮罩、状态图形 | 3 | 1 | 锁/状态图形待确认；不能只靠颜色 |
| A27 | 地图照片框 | 1 | 1 | 拖动缩放内容与固定框分离 |
| A28 | 相册折角、槽位 | 2 | 0 | 外形确认、翻页/空位/容量上限 |
| A29 | 实际鱼塘场景图 | 21 | — | 逐 pondId 确认图片与裁切 |
| A30 | 缺图、头像、鱼占位 | 3 | 0 | 外形确认、失败与重试交互 |

合计：90 张共用 UI（50 已校正、40 补充）＋60 张角色＋21 张场景＝171 张。ReferenceAssets 的 319 张参考层不计入这个资源包数量。

### 5.3 正式使用资源的技术要求

- [ ] 背景、地图、鱼、鱼饵、钓竿、船、头像复用 Unity 原始资源及 atlas 子图；网页缩小 JPG 只作预览依据。保留 FishCozyRounded-Medium 字体、中文覆盖与许可证。
- [ ] 按 manifest 分别设置 Sprite/FullRect、透明、无 mipmap、wrapMode、PPU、border 和 Image 类型；适合伸缩的底板用 Sliced，不能把全部图片统一设 PPU 200 或 Clamp。
- [ ] border 按源像素，contentInsets/contentSize 按设计单位处理。含透明阴影外延的 Image 矩形与按钮本体、文字区、命中区不能直接共用尺寸；阴影不能挤占内容宽度。
- [ ] 纸纹为原版 256×128 PNG，PPU 50，平铺周期 512×256 设计单位、alpha 0.32；使用 PaperMultiply.mat 与 CozyJournalMultiply.shader。验证遮罩、Canvas alpha、打包及目标平台，普通 alpha 混合不能等价替代。
- [ ] hover/pressed 表面与位移分离，由组件驱动 RectTransform；文字与底板一致，不叠加纹理位移和 CozyButtonVisual 动画。
- [ ] 角色按 manifest 使用 384×328 画布、PPU 200、pivot (0.5,0.1)；核对同皮肤姿势基线、边缘和玩家身份。样板默认的原版 characters/poses/orange-ready.png 不被补充图自动替换。
- [ ] 21 张鱼塘图保留 prefab、来源 hash 和相机裁切信息，按 pondId 配置及 fallback 加载；真实运行目录决定可见数量。原版示意照片与真实场景图分别确认，不能把换照片造成的差异当布局误差。
- [ ] 按目标平台验证图集、压缩、最大尺寸，避免细线、半透明胶带和软阴影出现脏边；平铺纸纹与特殊材质单独核查。记录纹理内存、批次和加载开销。
- [ ] 预加载浮漂、猫、粒子、提示组件并建立对象池；替换首次交互现场生成纹理的路径后检查卡顿。
- [ ] 音效另核查按压、落水、贴纸、翻页、盖章、金币的素材、授权、音量及开关；171 张图片交付不代表音频已齐备。

### 5.4 已有 12 个组件样板的正式化工作

已有目录：[JournalSamples/Components](../../../../../../fish-social-unity/Assets/Desktop/JournalSamples/Components)。这些是组合样板，不再列为“尚未制作”；以下升级工作均待完成。

| 已有 prefab | 正式组件需要补齐 |
|---|---|
| JournalPage | Library 页面基类/Variant、索引与内容槽、固定右栏、关闭与路由 |
| JournalStickerCard | 数据绑定、稳定倾角、选择/禁用/NEW、池化与首次可见动画 |
| JournalPolaroid | 图片异步加载/裁切/失败、文字区、不同照片比例 |
| JournalPondDetail | 共享详情状态；事实/收费/档位/进入分区、长文和失败恢复 |
| JournalMapMarker | pondId、准入/锁定/选中、命中区、坐标转换、唯一漂浮循环 |
| JournalBobber | 导航/加载用途配置、定位、取消与归位 |
| JournalPawToggle | Toggle 值与设置存储、焦点、禁用、减少动效 |
| JournalStamp | 状态文案、业务成功后触发、重播去重与取消 |
| JournalToast | 成功/错误/连接信息、排队/合并/超时、不挡点击 |
| JournalLoading | 请求生命周期、超时、重试/取消、与无数据区分 |
| JournalEmptyState | 区分空库存/空搜索/无动态，与加载失败分开 |
| JournalCornerCharacter | 玩家皮肤、姿势切换、偶发动作、循环优先级、托盘暂停 |

仍需新增或从正式 Library 扩展：索引、输入/滚动/模态组件、选鱼/发布/玩家卡弹窗、收获层，以及统一入塘控制器和动效调度。实现前复用现有职责，避免重复按钮和重复路由系统。

### 5.5 源文件、再生成与版本一致性

- [ ] UI 源文件以 manifest.source 为准：指向 sources/css/ 的 HTML 时，同名 sources/ui/ 旧 SVG 是历史底稿，不得覆盖当前校正图。
- [ ] 按资源 README 重建：初始 build → refine 校正 → 角色导出 → reference 导出 → Unity Prepare/Validate → 对比图 → 画廊/对照页 → verify。只运行初始 build 会把部分资源退回近似稿。
- [ ] 原版变化时更新源 hash、参考层、样板和对照图；正式组件变化另更新正式页面证据，不以样板通过代替正式页面通过。
- [ ] 保留 .meta/GUID 和映射，更新资源验证/覆盖状态；移除旧图前检查页面、嵌套 prefab、图集及 Overlay 导出引用。
- [ ] 资源 ZIP 为某次交付快照；发布时重新打包并附版本、数量和校验记录，当前仓库文档及 manifest 为最新依据。

## 6. 真实业务接入与原型新增功能

| 项目 | 已核查现状 | 实施结论 |
|---|---|---|
| 买/装备/出售/回塘 | IAuthenticatedApiClient 已有相关方法 | 复用接口，成功响应驱动表现；任何经济数值不写 PlayerPrefs |
| 资料/展示柜/图鉴/相册 | GetProfileHub、UpdatePlayerProfile、SetShowcase、GetCodex、ChangeAlbumPin 已有 | 主要是换布局/绑定/刷新；保留权限、候选照片和相册容量 |
| 四榜及我的排名 | GetLeaderboard / GetMyLeaderboardRank 已有 | 保留服务端排序、周期与单位 |
| 点赞/评论/删评 | 已有 REST + Socket 事件 | 移动到新组件并确保订阅解除、请求世代与权限 |
| 鱼获分享与可见范围 | 后端 POST /api/posts 支持 visibility；Unity ShareFish 只暴露 fishId/nickname | 扩展客户端参数即可接“公开/好友”，服务端继续校验 |
| 鱼获附言 / 无鱼纯文字 | 已支持真实鱼获附加最多160字心情、公开/好友可见；仍必须选一条库存鱼 | 无鱼纯文字动态不在本次既有鱼获分享语义内；接口拒绝伪造或他人鱼获 |
| 我的动态 | 已增加 GetMySocialFeed 与本人专属分页路由，返回本人全部可见范围动态 | 已验证认证、跨账号拒绝、55条分页、可见范围与空数据 |
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
| 改 | [Scripts/Desktop/Editor/CozyPrefabRepair.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Editor/CozyPrefabRepair.cs) | 已接入 journal 导入配置；继续补正式节点契约及修复后布局/材质持久性 |
| 核/改 | [Scripts/Desktop/Editor/CozySpriteImporter.cs](/Users/davilje/fish-social/fish-social-unity/Assets/Scripts/Desktop/Editor/CozySpriteImporter.cs) | 与已有 CozyJournalSpriteImporter 协调，避免旧规则覆盖 manifest；核查图集设置 |
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

第 5.4 节列出的 12 个组件样板已存在，但尚未成为正式运行时 Library。建议新增的 PondDetailView、PondEntryController 和动效调度/事件队列属于运行时代码；其余按已有样板或 Library 扩展，具体类名在实现时确定，避免与 CozyMotion/CozyButtonVisual 重复职责。

### 7.6 有条件才改的服务端

- [socialRoutes.ts](/Users/davilje/fish-social/server/src/socialRoutes.ts)：自由文字/我的动态/领奖等实际新增功能的 API、认证、校验与返回。
- [posts.ts](/Users/davilje/fish-social/server/src/posts.ts)：发布内容、作者筛选、分页与可见性；不靠客户端过滤全量动态。
- [achievements.ts](/Users/davilje/fish-social/server/src/achievements.ts)：若保留领奖，补权威领取流程；已有解锁流程继续复用。
- [profileHub.ts](/Users/davilje/fish-social/server/src/profileHub.ts)：若响应需新增领奖状态/社交字段等，扩展兼容返回。
- `server/src/migrations/`、`shared/` 对应类型、相关服务端测试：仅确需新增存储/协议时改。新增关注/领奖数据结构需要迁移，不能通过 UI 本地保存替代。

WPF 的 `desktop-overlay/`、Unity 的 OverlayHud/OverlayPondLayoutBaker/OverlayHudExporter 本次以联动回归为主，不做本方案的主窗口换皮。共享资源修改前检查导出链，避免间接改变原生 Overlay。

### 7.7 已交付资源与参考工具：复用及维护

| 标记 | 文件/目录 | 后续责任 |
|---|---|---|
| 核/改 | [CozyJournalResourcePreparation.cs](../../../../../../fish-social-unity/Assets/Scripts/Desktop/Editor/CozyJournalResourcePreparation.cs) | 已有资源配置、组合样板、原版参考导入和验证；保持编辑器职责，正式页面生成进入 Authoring/Library 链 |
| 核/改 | [journal](../../../../../../fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/journal) 及三个 manifest | 正式引用、补充资源确认、导入一致性、皮肤/pondId 映射 |
| 核 | [CozyJournalMultiply.shader](../../../../../../fish-social-unity/Assets/Shaders/UI/CozyJournalMultiply.shader)、[PaperMultiply.mat](../../../../../../fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/journal/materials/PaperMultiply.mat) | 纸纹混合、Mask/RectMask2D、平台与构建包含验证 |
| 核 | [JournalSamples](../../../../../../fish-social-unity/Assets/Desktop/JournalSamples) | 保留 11 个页面、12 个组件的参考用途；检查发布依赖，避免无意带入全部参考层 |
| 核/改 | [scripts/art](../../../../../../scripts/art) 中 build/refine/export/compare/verify/qa/package 脚本 | 原版变化时更新导出；增加正式页面截图/验证，不冒用 reference 结果 |
| 核/改 | [资源说明](../../../../../art/cozy-journal/README.md)、[coverage.json](../../../../../art/cozy-journal/coverage.json)、对照页与验证记录 | 按实际证据更新接入状态，标明样板/正式页面及未解决差异 |

## 8. 实施顺序与交付物

| 阶段 | 当前状态 | 工作包 | 完成条件 |
|---|---|---|---|
| 0 基准与资源确认 | 已完成部分准备 | 使用当前原版、11 状态、171 资源；补其余子页/异常状态基准，确认补充美术与产品差异 | 来源、画布、全状态范围、图片/角色选择及新增业务边界明确 |
| 1 正式基础组件 | 待实施 | 共享切图接入 Library；先做纸页、商品卡、鱼塘详情，统一字体/纸纹/阴影/缩放、输入与滚动 | 正式资源组件对照、长内容和交互通过，不以 ReferenceAssets 替代 |
| 2 主壳与选塘闭环 | 待实施，P0 | 主壳、常驻地图入口、导航/路由；共享详情、地图/列表、档位/准入、真实入塘/失败/返回 | 四档窗口可用；两入口状态一致、详情不挡操作；确认后进入 Overlay |
| 3 常用页面 | 待实施 | 背包、装备、商店、资料/编辑/展示柜/图鉴/相册 | 真实数据和写操作闭环，刷新/取消/失败/权限正确 |
| 4 社交与保留功能 | 待实施 | 排行榜、成就、动态、好友/聊天、登录/引导/结算、设置及兼容弹窗 | 原有功能全部可达；第 6 节业务按确定范围落地，无模拟按钮 |
| 5 全量表现 | 待实施，可随组件推进 | 全套装饰/动效、皮肤、提示/加载/空态、收获队列、音效 | 视觉确认；单循环/减少动效/快速取消/后台恢复及性能达标 |
| 6 发布与回归 | 待实施 | 全页面状态对照、预制体重载、修复/重生成、EditMode/PlayMode/Windows Player、Overlay 联动 | 第 9 节通过；无缺引用/旧皮肤/经济或会话错误，清单与包同步 |

每阶段交付：修改过的 C#、已保存 prefab/.meta、实际引用资源及映射、正式页面对照截图、验证结果与未解决项。业务 API 变更另附协议与迁移测试。完成全部阶段后才能称为“完整替换”；资源与样板准备不能折算为正式页面完成率。

## 9. 验收清单

### 视觉与布局

- [ ] 截图来自运行中的正式 prefab/业务页面，附版本、分辨率、测试账号状态，与参考样板截图分开存放。
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

### 资源与差异确认

- [ ] 90 张 UI 的实际使用状态、60 张姿势的采用范围、21 张场景图的 pondId 对应均有记录；未采用项不宣称接入完成。
- [ ] 逐区检查布局、文字基线/换行、纸纹、边缘、阴影、半透明胶带、选中/焦点、照片裁切；整屏平均差异不能替代局部验收。
- [ ] 字体栅格化、半透明合成、旋转文字的剩余差异列明；不能因样板 MAE 较低就标注“像素完全一致”。
- [ ] 发布前验证源文件→manifest→导入值→正式 prefab 引用一致；shader/字体/图集在 Player 中可用，旧图无漏引用。
- [ ] 资源清单、覆盖状态、正式页面截图、测试记录与最终资源包同步到同一版本；旧审计及旧 ZIP 标为历史快照。

## 10. 审计证据与完成边界

| 已有记录 | 能证明的范围 |
|---|---|
| [asset-validation.json](../../../../../art/cozy-journal/previews/asset-validation.json) | 尺寸、透明、GUID、源文件、PPU/border、字体引用和对照图等资源检查 |
| [browser-validation.json](../../../../../art/cozy-journal/previews/browser-validation.json) | 11 状态对照、171 张图加载、检索/筛选/对话框、世界入口与鱼塘分区的浏览器验证 |
| [prepared-assets-unity-result.txt](../../../../../art/cozy-journal/previews/prepared-assets-unity-result.txt) | Unity 导入、角色/鱼塘清单、Multiply 材质、样板重载及字体检查 |
| [reference-unity-result.txt](../../../../../art/cozy-journal/previews/reference-unity-result.txt) | 当前参考样板已实际 Unity 渲染 |
| [comparison-metrics.json](../../../../../art/cozy-journal/previews/comparison-metrics.json) | 同尺寸画面残余差异，不是视觉通过百分比或业务验收 |

审计读取了主壳、路由、各页面控制器、主题/动效门面、API/DTO、选塘会话接口、作者工具、构建守卫、已有布局/预制体测试、原型源码及相关服务端路由。代码中已经存在的能力按“复用/改造”列出，建议新增的组件和业务明确标注。

参考设计 [猫咪钓鱼手账方案](/Users/davilje/fish-social/docs/design/ui/journal/2026-10-21-cozy-journal-motion.md) 与 [STEAM-DESKTOP-21 现有规格](/Users/davilje/fish-social/docs/planning/specs/desktop/Steam桌面端-21猫咪钓鱼手账动效.md) 是不同覆盖程度：后者只接入基础手感，明确把胶带换皮、完整收获和音效放在后续。本次“完整替换”需要覆盖前者加最新原型扩展，不能用旧规格的部分完成代表全量完成。

本次重整核对原版、资源/样板结构、已有验证记录及正式主壳/选塘/接口实现，纠正旧清单中 journal 尚未交付、manifest 过时、主壳始终使用 1920×1080、所有组件均需从零制作等过时结论。上表为已有准备阶段证据；本次未重新运行全套 Unity 测试、Play Mode、Windows Player 构建或原生 Overlay 联动。后续以正式页面逐项验收，不用 PNG 数量、样板截图或旧规格部分完成替代全量完成。
