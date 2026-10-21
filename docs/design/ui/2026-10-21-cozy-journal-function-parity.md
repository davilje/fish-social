# 原版猫咪钓鱼手账 UI：功能对照审计

审计日期：2026-10-21。范围：个人中心五子页、编辑资料、成就、排行、动态、发布、设置。本文依据原版实际事件处理和 Unity 运行时绑定，不把截图样例数据、隐藏控件或仅有方法但没有入口视为已实现。商店、背包、装备、地图、鱼塘及主壳由相应页面审计覆盖。

**当前不能宣称所有功能与原版完全一致。** 展示柜原位更换/清空、未解锁图鉴探索入口、资料简介实时预览已补齐。仍有相册手动钉选/取消、关注、成就领奖和连续进度等差异。部分属于已有服务端规则与网页演示不同，详见逐项记录。

状态定义：

- **可比**：原型的交互意图有真实绑定；数据来自真实接口或桌面设置，不要求数值等于演示数据。
- **规则差异**：具备功能，但范围、统计口径、流程或限制不同，必须显式保留说明。
- **缺失**：原型入口/行为在当前正式页面不存在，或者仅有未接线的方法；不计入完成。
- **新增**：正式产品已有能力，原版没有对应有效行为。

证据索引（下文控制器名均指这些文件，方法名便于搜索）：

| 简称 | 文件 |
|---|---|
| 原版扩展 | `docs/design/ui/prototype/cozy-journal/pages.js` |
| 原版基础 | `docs/design/ui/prototype/cozy-journal/app.js` |
| Hub | `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopProfileHubPanel.cs` |
| Edit | `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopProfileEditPanel.cs` |
| Rank | `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopLeaderboardPanel.cs` |
| Feed | `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopSocialFeedPanel.cs` |
| Post | `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopSocialPostCard.cs` |
| Composer | `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopSocialComposeModal.cs` |
| Settings | `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopSettingsModalView.cs` |
| 页面作者 | `fish-social-unity/Assets/Scripts/Desktop/Editor/CozyRedesignAuthoring.DetailParity.cs` |
| 发布作者 | `fish-social-unity/Assets/Scripts/Desktop/Editor/CozyJournalComposerAuthoring.cs` |

## 个人中心与展示柜

| 原版源触发器/展示 | 真实绑定/数据 | 状态与实际规则 |
|---|---|---|
| `profileBody` 五个页签：资料、展示柜、图鉴、相册、成就 | Hub `WireFromPrefab → SelectTab → RenderCurrentTab` | 可比。五页仍然保留，切换会显示对应内容和状态。 |
| `j-edit-profile` 编辑资料 | Hub `_editButton → _openEdit`；Edit `OnOpened` | 可比。加载服务端资料后编辑。查看别人时按 `canEdit` 隐藏编辑，属于权限控制。 |
| `j-profile-tab` 我的展示柜/翻翻相册 | Hub `RenderJournalProfile` 中绑定 `JournalShowcaseLink/JournalAlbumLink` | 可比。真实切换子页。 |
| 右侧“整理我的展示柜” | Hub `RenderProfile` 绑定 `DesignShowcase/Open` | 可比。进入展示柜页。 |
| 身份头像、昵称、简介 | Hub `RenderProfile/LoadAvatarRoutine`，`GetProfileHub` | 可比。正式数据不会使用小橘等样例覆盖。 |
| 统计“累计鱼获 / 探索水域 / 图鉴” | Hub `RenderProfile` | 规则差异。前两项目前显示真实等级和 XP，第三项为真实图鉴数量；不把预览中的 64 条/5 个水域当服务端数据。 |
| 下一级经验进度 | Hub `RenderJournalProfile`，`player-levels` 阈值 | 可比。以真实等级、XP 计算；不是固定 64%。 |
| 展示柜点击鱼获卡片 `jSelect(profile, slot)` | Hub `RenderShowcase → ShowFishDetail` | 可比。点击显示该鱼真实品质、尺寸、时间、来源、食性等。 |
| 展示柜空位卡片 | Hub `RenderShowcase → ShowShowcaseNote` | 可比。选择空格后右栏显示“选择鱼获”，打开本页原生选择器。 |
| 3 个展示位 | Hub `RenderShowcase`；Edit `_slots` | 规则差异。正式规则为 8 个格子；布局为可滚动 3 列，不能删成 3 格而损失已有装备。 |
| 展示柜右栏 `j-pick-showcase` 选择/更换 | Hub `OpenShowcasePicker → GetInventoryItems → SaveShowcaseSelection → SetShowcase` | 可比。本次已增加本页原生选择器，选鱼直接真实提交；busy 防重复、失败可再次选择重试，保留另外 7 格。 |
| 展示柜右栏 `j-clear-showcase` 清空 | Hub `JournalClear → SaveShowcaseSelection(null) → SetShowcase` | 可比。本次已增加右栏直接清空及选择器内清空；真实提交、不出售鱼获。空格清空按钮禁用，别人资料隐藏两项修改入口。 |
| 展示鱼选择器关闭/取消 | Edit `HidePicker`；资料页 `GoBack` | 可比。关闭选择器保留编辑草稿；取消资料不提交。 |
| 展示鱼保存失败/重试 | Edit `SaveRoutine/OnRetry` | 新增真实状态。资料和展示柜分两次请求；资料已成功而展示柜失败时会说明部分保存，不伪造整体成功。 |

## 图鉴与相册

| 原版源触发器/展示 | 真实绑定/数据 | 状态与实际规则 |
|---|---|---|
| 图鉴点击已解锁鱼种 `jSelect` | Hub `RenderCodex → ShowCodexDetail` | 可比。读取真实图鉴记录，显示鱼种资料、累计捕获和纪录。 |
| 未解锁剪影/尚未发现 | Hub `RenderCodex/ShowCodexDetail` | 可比。实际鱼种目录、解锁状态来自游戏；不固定为原型 15 种。 |
| 未解锁右栏 `j-open-ponds` “去探索新的水域” | Hub `ShowCodexDetail → JournalExplore → _openWorldMap` | 可比。本次新增原位探索按钮，未解锁时显示，调用主壳已有世界地图路由；已解锁时隐藏。 |
| 相册 `j-album-page` 上一页/下一页 | Hub `RebindAlbumPagers → RenderAlbum` | 可比。本次审计补齐首尾页按钮禁用，空相册两按钮禁用；每页两张。 |
| 点击左右照片选择，右栏换回忆 | Hub `BindAlbumSelection → RenderAlbum` | 可比。本次审计补齐照片点击，右栏随左右选择更新；翻页回到左照片。 |
| 相册照片与回忆说明 | Hub `LoadAlbumPhotosRoutine/FormatAlbumPage`，`GetProfileHub.albumPins` | 规则差异。使用真实自动精选与服务端照片；原型是固定背景加鱼种贴图。 |
| `j-album-add` 钉选回忆，鱼获选择器 | Hub `PinRoutine` 存在但无 UI 接线；服务端 `profileHub.ts` 明确返回 `albumCandidates: []` | **缺失入口且规则不同**。当前自动记录稀有/大鱼/回鱼/新图鉴，不提供原型“任意未钉选鱼获”候选。存在 API 不能算已完成。 |
| `j-album-remove` 取消钉选 | Hub `UnpinRoutine` 存在但无 UI 接线；服务端 `ChangeAlbumPin` 支持 unpin | **缺失**。正式相册目前没有取消按钮。需连同自动补录规则确认，避免用户删除后又被重新填回。 |
| 相册空态 | Hub `RenderAlbum` | 可比。有空相册文案及禁用翻页；当前页标为 0/0，原版为 1/1，属于显示差异。 |

## 编辑资料

| 原版源触发器/展示 | 真实绑定/数据 | 状态与实际规则 |
|---|---|---|
| `j-nickname` 输入昵称 | Edit `_nickname`，`Save` | 规则差异。正式上限 12 字，原型 16 字；空白会显示错误并阻止提交。 |
| `j-bio` 输入简介 | Edit `_bio`，`SaveRoutine` | 规则差异。正式上限 120 字，原型 80 字；多行输入。 |
| `data-avatar` 六猫头像 | Edit `RenderAvatars/SelectAvatar` | 可比。使用真实可选头像资源，选中状态及保存有效。 |
| 输入昵称/选头像实时更新右侧拍立得 | Edit `UpdateJournalPreview/RenderAvatars` | 可比。立即显示当前草稿。 |
| 输入简介实时更新右栏 `j-bio-preview` | Edit `_bio.onValueChanged → UpdateJournalBio`，作者 `JournalPreview/Bio` | 可比。本次补齐原生简介副本，实时更新草稿；展示柜编辑滚动区下移保留。 |
| `j-profile-form submit` 保存 | Edit `Save → SaveRoutine → UpdatePlayerProfile + SetShowcase` | 可比但提交真实网络请求。保存忙、会话失效、错误、重试由正式状态驱动；顶栏及 Overlay 身份同步。 |
| `j-cancel-edit` 取消修改/关闭 | Edit `GoBack`；`Header/Back` 与 `DesignHeader/Close` | 可比。返回个人资料；保存进行时受 busy 保护。 |
| 展示柜 8 格编辑、选择器清空 | Edit `OpenPicker/PickFish/ClearPickedSlot` | 新增原型编辑页没有的内容，与展示柜原位编辑同时保留。 |

## 成就

| 原版源触发器/展示 | 真实绑定/数据 | 状态与实际规则 |
|---|---|---|
| “全部/已完成”页签 | Hub `WireAchievementRefs → RenderAchievements` | 可比。过滤真实 unlocked 状态，个人中心与独立成就页均可用。 |
| 未完成筛选、钓鱼/收藏/成长分类 | Hub `MatchesAchievementCategory` | 新增正式分类。分类不是本地写死的完成记录。 |
| 完成数量/总量 | Hub `RenderAchievements` | 可比。按真实成就列表计算，含隐藏描述权限处理。 |
| `data-act=claim` 领取金币 | 无 claim 接口/控制器；`AchievementViewDto` 使用 unlocked | **缺失且超出现有规则**。正式页只标“已完成”，不能把它称为“已领取”或播放假金币奖励。 |
| 未完成连续进度条与开页增长 | 正式成就 DTO 未提供当前值/目标值，仅解锁状态 | **缺失连续进度**。不能用原型固定百分比代替服务端进度。 |
| 成就实时解锁 | Hub `OnAchievementUnlocked` / pond socket | 可比。真实解锁触发刷新；不等同于奖励领取。 |

## 排行与钓友资料

| 原版源触发器/展示 | 真实绑定/数据 | 状态与实际规则 |
|---|---|---|
| 四榜页签 | Rank `SelectBoard → LoadRoutine` | 可比。有缓存/请求过期保护/错误状态；数据来自真实排行榜。 |
| 今日大鱼 | Rank `FormatValue(BoardDaily)` | 可比。实际最大尺寸，额外带鱼种名。 |
| 每周钓王：原型按累计 kg | Rank `FormatValue(BoardWeekly)` | **规则差异**。现后端按金币收益统计，显示金币和捕获条数；不是 kg 榜。 |
| 当前鱼塘 | Rank `BoardPond`，当前 pondId 请求 | 可比。真实鱼塘捕获条数，附最大体长；不固定“千岛湖”。 |
| 稀有鱼获：原型按珍稀鱼种数 | Rank `FormatValue(BoardRare)` | **规则差异**。正式为史诗及以上鱼获条数，非去重鱼种数。 |
| 点击前三领奖台/榜单行 | Rank `BindPodium/BindRow → SelectPlayer` | 可比。右栏显示所选玩家真实名片、榜单值。 |
| 右栏“看看钓友的收藏” | Rank `SelectPlayer → _openPlayer`，Hub `ShowOtherPlayer` | 可比但承载不同：正式打开可浏览五子页的只读个人中心，原版是紧凑玩家弹窗。 |
| 右栏等级/简介/代表鱼获及三个统计 | Rank `LoadPlayer` 当前接昵称、头像、简介和本榜值 | **部分缺失**。没有完整复刻原型名片里的等级、代表鱼获、累计鱼获/稀有收藏/今日大鱼三项组合。 |
| 名片 `j-follow` 关注/取消关注 | 无对应控制器/关系 API | **缺失且超出现有规则**。原版明确仅本地预览；正式好友动态按真实好友关系，不能用本地假关注改变权限。 |
| 我的排名 | Rank `FetchMyRankOnly/SetMyRank` | 可比。来自真实服务端排名，不固定第 5。 |
| 重试/刷新 | Rank `RefreshNow` | 新增网络错误恢复。 |

## 动态与留言

| 原版源触发器/展示 | 真实绑定/数据 | 状态与实际规则 |
|---|---|---|
| 公开/好友/我的页签 | Feed `SelectFeed → GetSocialFeed(friendsOnly)/GetMySocialFeed` | 可比。“我的”使用本人专用接口，包含本人不同可见范围，未用公开资料接口冒充。 |
| 好友动态包含本地关注人 | 真实好友接口 | 规则差异。原型把本地 followed 混进好友；正式按真实访问权限。 |
| 页面“分享鱼获” | Feed `OpenComposer` | 可比。打开持久化原生发布弹窗。 |
| 空态“分享第一条鱼获” | 仍可用顶部“分享鱼获”；列表空态未增加原型那一个独立按钮 | **缺失重复原位入口**。主要发布功能已接，但不能算空态按钮也存在。 |
| 点击动态整卡选择右栏留言 | Post `IPointerClickHandler.OnPointerClick → SelectComments` | 可比。本次审计补接整卡点击；原生子按钮自行处理，拖动滚动不触发卡片点击。 |
| 默认选中首条动态 | Feed `CreatePostCard → SelectComments` | 可比。本次审计补接第一条加载完成后显示其留言。 |
| `j-like` 点赞/取消赞 | Post `ToggleLike → TogglePostLike` | 可比。真实 API 成功后更新，socket `ApplyLiked` 同步；保留评论草稿。 |
| `j-comments` 评论按钮 | Post `ToggleComments/ConfigureCommentsHost` | 可比。将同一张卡的真实 CommentsPanel 移入右栏，切换还原所有权；已展开再次点击聚焦输入，不关闭右栏。 |
| 输入评论/留下鼓励 | Post `SendComment → AddPostComment` | 规则差异。正式上限 200 字、支持多行，原型为 80 字单行；空白不会提交。 |
| 评论加载、列表滚动 | Post `GetPostComments/AddCommentRow`，原生 ScrollRect | 可比。服务端失败可重试，切换时抛弃旧回调，评论不会串到其他帖子。 |
| 评论发送后刷新数量/列表 | Post `SendComment/ApplyCommented` | 可比。真实结果去重更新；服务端拒绝保留草稿。右栏摘要评论数可能直到重新选择才刷新，需进一步同步。 |
| 删除自己的评论 | Post `DeleteComment → DeletePostComment` | 新增。受本人权限限制，不给所有评论显示删除。 |
| 动态头像/文字/鱼种卡 | Post `RenderPost/RefreshCardLayout` | 可比主体，但鱼种插画取本地资源；原版等级行尚未完全对应真实作者等级，动态卡不显示后端照片 URL。长正文会增加卡片高度。 |
| 滚动加载更多 | Feed `OnScrollChanged/LoadMore` | 新增真实分页，首次 50 条，后续 10 条。 |

## 发布弹窗

| 原版源触发器/展示 | 真实绑定/数据 | 状态与实际规则 |
|---|---|---|
| `j-compose` 打开弹窗 | Feed `OpenComposer → Composer.Create/Open` | 可比。Resources 加载 `JournalComposeModal.prefab`，只动态生成鱼获选项，不在运行时重建基础页面。 |
| 挑鱼下拉 | Composer `LoadFish/BindControls 下拉监听`，`GetInventoryItems` | 可比。选真实库存 fishId；无鱼时禁止提交，加载失败可重试。 |
| 160 字心情输入/计数 | Composer `_story.onValueChanged` | 可比长度。正式保留原生多行输入与计数。 |
| 空心情阻止提交 | Composer `Submit/Publish` 支持空白默认分享文案 | **规则差异**。原型 required 必须写一句话；正式沿用既有分享语义，空白由服务端生成鱼获文案。 |
| 所有钓友/仅好友下拉 | Composer `SelectVisibility` | 可比。真实 public/friends 权限；没有未受支持的“本地可见”假选项。 |
| “贴进动态手账” | Composer `Publish → ShareFish(fishId,nickname,visibility,text)` | 可比。成功后关闭弹窗并切到“我的动态”重新请求；无本地模拟成功。 |
| 提交忙/失败/成功 | Composer `RefreshControls/Publish` | 新增网络边界。忙时防重复提交并禁止取消；失败保留输入和选鱼。 |
| 关闭 × / Escape / 点击遮罩 | Composer `Close/OnPointerClick` | 可比。忙时阻止意外丢失；退出页面可 force 关闭。 |
| Tab 焦点循环/恢复原焦点 | Composer `Update/Close` | 可比。循环可交互控件，关闭恢复此前焦点。 |

## 设置

| 原版源触发器/展示 | 真实绑定/数据 | 状态与实际规则 |
|---|---|---|
| 窗口/通知/托盘三个索引 | Settings `BindTabs/ShowTab` | 可比。开发构建另外显示服务器连接页；发布构建隐藏服务器页。 |
| 四种分辨率 `.preset` | Settings `EnsureResolutionButtons → WindowManager.ApplyWindowedPreset` | 可比。真实应用并自动持久化，选中章跟随当前模式/尺寸。 |
| 窗口/无边框/全屏 `data-mode` | Settings `Bind → WindowManager.SetMode` | 可比。`JournalWindowed` 已补齐第三段；不是只做视觉选中。 |
| 通知总开关 | Settings `Bind → SetNotify(EnableNotifications)` | 可比。写实际 NotificationSettings。 |
| 免打扰 | Settings `SetNotify(DoNotDisturb)` | 可比。正式布尔免打扰，不等于自动夜间时段计划。 |
| 咬钩/好友邀请/连接异常提醒 | Settings `SetNotify` 对应三个标志 | 可比。实际通知服务读取这些设置。 |
| 关闭窗口隐藏到托盘 `data-sw=tray` | Settings `HideToTrayOnClose` + Save | 可比。是否能显示系统托盘还依平台桥接支持。 |
| 立即隐藏到托盘 | Settings `WindowManager.HideToTray` | 新增真实动作。原型 `settingsBody` 按钮本身没有 data-act，仅是静态按钮。 |
| 重置新手引导 | Settings `OnResetOnboardingClicked` | 新增真实动作。原型同名按钮未绑定业务。 |
| 退出游戏 | Settings `DesktopAppBootstrap.QuitForReal` | 新增真实动作。原型同名按钮未绑定业务。 |
| 减少动效（原版全局工具 `#reduce`） | Settings `EnsureReduceMotionToggle → CozyMotion.SetReduced` | 可比，正式放在设置页并持久化。 |
| 设置右栏自动更新当前状态 | Settings `RefreshWindowSelection/RefreshTabInfo` | 可比。来自真实窗口、通知设置；不是原型固定值。 |
| 开发服务器地址保存/测试 | Settings `EnsureServerUrlControls` | 新增开发功能，发布隐藏属于正式环境边界，不用来代替原型功能。 |

## 本次审计补丁与验证范围

本次补齐不需要新增后端产品规则的漏接：动态整卡选留言与默认首条选中、评论按钮在右栏已打开时聚焦输入、相册左右照片选中及翻页边界禁用、发布遮罩点击关闭，以及展示柜原位真实更换/清空、图鉴探索路由、编辑简介实时预览。修改限定在上述页面控制器、专属作者 partial 和测试；未修改预览样例或后台规则。

新增编辑器回归用例 `CozyJournalPageBindingTests.AlbumBindsBothPhotosAndDisablesPagingForEmptyAlbum`、`AuthoredShowcaseActionsRespectReadOnlyAndCodexExploreUsesRoute`、`ProfileBiographyDraftUpdatesAuthoredPreviewImmediately`。最新 runtime 与 editor/tests 均已通过 Unity 2021.3 对应 C# 编译器检查（0 错误）；实际 Unity Test Runner 与画面比对由主流程执行，本文不把编译通过等同于所有交互实机验收。

位置、角度、尺寸以 `CozyRedesignAuthoring.DetailParity.cs` 和发布作者文件的可编辑原生节点为准：相册卡 311×300、CSS 角度 −1.3°/+1.3°，资料/排行/动态右栏宽 362；成就卡 215×258；动态正文允许真实长度增高；发布逻辑纸面 690×720，绘制纸层按透明边缘四向各扩 80。图形倾角通过顶点变换实现，点击根保持 0°。这些是作者参数，不是“所有像素已经一致”的证明；最终图像和运行时点击边界须由逐页预览及交互检查确认。

## 排行榜动效逐项审计与修复（追加）

源逻辑需同时阅读 `app.js` 的 `press/release/stagger/switchTab` 与 `pages.js` 覆盖后的 `jSelect/refreshAside`。后者把基础版右栏“横移 24px + 260ms”覆盖为**无横移、220ms**；不能仅照基础函数或静态截图实现。

| 行为 | 原版实际时序 | Unity 当前接线与修复 |
|---|---|---|
| 领奖台/整榜入场 | DOM 顺序为第二/第一/第三领奖台，再完整榜单；仅前 8 张从 1.12 倍、CSS 倾角 +6°、透明到最终，260ms OutBack，每张延迟 40ms | `BindPodium/BindRow → CozyStickerMotion.Bind`。原先领奖台只有静态角度、榜单从第四名起；已改完整榜单含前三名，按原 DOM 顺序赋序号，领奖台倾角由真实 playerId 的源 hash 算法计算，列表角度为 0。 |
| 悬浮 | 抬起 5px，180ms OutCubic；阴影加深 | 通用 `CozyStickerMotion`，领奖台和列表现在都绑定。固定命中根不移动。 |
| 按下/释放 | 60ms OutQuad 到 1.02×0.96；220ms OutCubic 经 0.99×1.02 回归 1 | `CozyButtonVisual` 识别 sticker 的 light press；原先新建按钮漏了视觉组件，现作者补齐。 |
| 选中 | 同 playerId 的领奖台与列表同时 selected，抬起 6px/180ms；胶带 160ms OutCubic 展开 | `SelectPlayer → SyncPlayerSelection` 同步所有卡；补齐 `JournalCard` outline/tape状态绑定。胶带默认 CSS −28°，Unity +28°，非选中隐藏。 |
| 点击同一玩家 | `jSelect` 没有 early-return，仍重播右栏/胶带 | 控制器保留同玩家点击重播右栏；通用胶带选中边沿播放与原型重复点击重播仍有差异，不能声称完全一致。 |
| 右栏切换 | 整个拍立得（边框、阴影、照片、caption、胶带）.9→1 倍、.3→1 opacity；220ms OutBack，无横移 | `SelectPlayer → JournalPhoto.CozyJournalStaticPose.PlayReveal()`（类型名 `CozyJournalStaticPose`）；共用中心，绑定多个兄弟图形，接口响应只更新文字/头像，不重新播放。 |
| 切榜 | 索引先选中；中栏/中标题/右栏/底部状态100ms线性淡出；新卡贴入，标题160ms淡入，右栏延迟 `80+min(8,卡数)×20ms` 后160ms淡入 | `SelectBoard → SwitchBoardRoutine → CozyJournalPageMotion.Exit → RefreshNow`；数据到达后 `PlayEntrance`。网络期间显示真实加载/错误，不用演示数据填补。纸页和索引不参与淡出。 |
| 滚动 | 领奖台和完整榜单共用中栏纵向滚动 | 已将 podium 持久化放在 `Scroll/Viewport/Content/Podium`，ignoreLayout；完整榜单接其后。原先领奖台固定、列表单独滚动已修。 |
| 打开玩家详情 | 原版紧凑对话框200ms OutCubic，从 y+16px/透明进入；有本地假关注 | 正式仍进入真实只读个人中心。保留产品承载差异，不添加本地假关注或把个人中心说成原型弹窗。 |

Editor 可复现路径：`DesktopLeaderboardPanel.PresentRows(snapshot)` 建立与网络加载相同的按钮和回调；调用 `SelectPlayer(snapshot[1])` 再 `SampleMotion(t)`，分别用新实例采样 0/0.06/0.11/0.18/0.22 秒。先结束统一页面入场，再触发选择采样；采样后不要再调用全局 ApplyFinalPose。页面切换可直接用 `CozyJournalPageMotion.SampleExit(t)`、`SampleEntrance(t,卡数)`。共享页面组件支持 `ConfigureAside(parts)` 处理多个同级右栏图形。

新增 `CozyJournalLeaderboardMotionTests` 检查页面100ms淡出/右栏延迟淡入、完整前三名行、双份选中联动、右栏0与220ms尺度/透明度和不横移。仅编译检查不替代网页/Unity逐帧对照。
