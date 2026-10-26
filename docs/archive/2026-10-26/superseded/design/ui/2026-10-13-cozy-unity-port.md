# 最终版雕刻 UI → fish-social Unity 移植

日期：2026-10-13。目标分支：`开发用分支`。目标工程：`fish-social-unity`（Unity 2021.3.29f1c1，uGUI）。

## 采用的最终方案

使用已确认的奶油纸张、可可棕单色雕刻图标及栅格手绘场景，不再制作新的按钮凹槽方案。

- 默认：浅奶油纸张，素材内包含柔和外投影。
- 悬停/选中：暖桃色高亮，继续使用同一套单色图标。
- 按下：切换到较深的按下底板，视觉内容下移 3 个参考像素，外投影随底板切换而消失；不添加内投影。
- 位移只作用于按钮的视觉内容，不移动按钮根节点、锚点或点击区域，不改变原来的点击事件。
- 图标 PNG 已含可可棕色，Unity 使用白色 tint，避免二次染色。底板采用九宫格拉伸；不使用 SVG。

## 工程接入位置

| 内容 | 工程路径 |
| --- | --- |
| 最终切图包 | `Assets/StreamingAssets/DesktopTheme/Cozy/` |
| 素材清单及颜色规范 | 切图包内 `manifest.json`、`design-tokens.json` |
| 加载及主题兼容层 | `Assets/Scripts/Desktop/UI/CatTheme.cs` |
| 按钮三态及视觉位移 | `Assets/Scripts/Desktop/UI/CozyButtonVisual.cs` |
| 运行时新增节点主题 | `Assets/Scripts/Desktop/UI/CozyPanelSkin.cs` |
| 布局辅助 | `Assets/Scripts/Desktop/UI/CozyUi.cs` |
| 壳层、首页、导航及路由接入 | `Assets/Scripts/Desktop/UI/DesktopShellUi.cs` |
| 鱼塘目录卡片 | `Assets/Scripts/Desktop/UI/CozyPondSelectionPanel.cs` |
| 编辑器素材检查菜单 | `Assets/Scripts/Desktop/Editor/CozyThemeValidationMenu.cs` |
| EditMode 回归测试 | `Assets/Tests/Editor/CozyDesktopThemeTests.cs` |

切图保留 `icons/`、`buttons/`、`textures/`、`scenes/`、`characters/`、`fish/`、`sources/` 分类。`sources/approved-button-states.png` 是最终确认图；`reference-*.png` 含背包图案，仅供比对，**不能当空白按钮底板**。运行时使用 `plate-default/hover/pressed.png`。

## 页面与功能对照

桌面沿用原工程 1280×720 参考分辨率：顶部身份/状态/金币、中央内容、底部十一项导航。登录窗口仍使用原来的小窗与 Steam 流程。页面根部统一标题和留白，功能页继续挂载原有 Prefab；加载出的 Prefab 与运行时生成的节点都接入主题。

| 设计入口 | Unity 页面/组件 | 继续使用的真实功能 |
| --- | --- | --- |
| 开始游戏 / 主页 | `Home` / `DesktopShellUi` | Steam 登录、当前会话、进入当前鱼塘 |
| 世界地图 | `WorldMap` / `DesktopWorldMapPanel` | 拖拽缩放、真实目录、等级进度、门票档位、确认进塘 |
| 鱼塘选择 | `PondSelection` / `CozyPondSelectionPanel` | 展示 `WorldMapPondCatalog`；选中后定位世界地图，再走原进塘校验 |
| 开始钓鱼 | `Pond` / `DesktopShellUi` | 原有连接、选钓位、开始钓鱼、收杆、进入原生桌宠 |
| 背包 | `CatchBag` / `DesktopCatchBagModalView` | 真实鱼获、金币、出售、回鱼、分享 |
| 装备栏 | `Equipment` / `DesktopShopPanel` 装备模式 | 真实已拥有钓竿、已解锁鱼饵、`EquipTackle` / `EquipBait` |
| 商店 | `Shop` / `DesktopShopPanel` | 原商品目录、价格、等级条件、购买与装备 |
| 成就 | `Achievements` / `DesktopProfileHubPanel` | 原个人中心的成就页和解锁数据 |
| 排行榜 | `Leaderboard` / `DesktopLeaderboardPanel` | 原日/周/鱼塘/稀有榜、自己的排名、玩家资料入口 |
| 个人信息 | `Profile` / `DesktopProfileHubPanel` | 资料、展示柜、图鉴、相册、成就、编辑资料 |
| 社交动态 | `SocialFeed` / `DesktopSocialFeedPanel` | 公共/好友动态、点赞、评论、删除等现有交互 |

好友/私聊、设置、资料编辑、原图鉴入口仍可使用，不因新的十一项导航而删除。装备模式不展示购买按钮；没有船只装备 API，因此不伪造船只装备。鱼饵仍遵循原工程的解锁/按次计费规则，不改造成前端虚拟库存。

`ShellPanelId` 的既有 0–11 值不变，只在末尾增加 `PondSelection=12`、`Equipment=13`、`Achievements=14`，避免破坏旧场景和菜单的序列化引用。

## 没有改动的业务边界

- 不替换服务端、认证、数据模型、Socket 协议、埋点、价格或经济规则。
- 不使用网页效果稿中的演示余额、假商品、假排行榜或 localStorage 作为真实数据。
- 新鱼塘卡片只负责选取；进入鱼塘仍由旧地图的锁定、费用及确认逻辑执行。
- 主页/钓鱼控制页中的手绘湖泊和橘猫是 UI 场景插画，不是重写后的钓鱼模拟器。
- **真实桌宠钓鱼目前由原工程 Windows WPF Overlay 承载。此补丁修改 Unity 主窗口 UI，不把 WPF 窗口伪称为已更换样式，也不替换其鱼竿/猫咪动画。** Windows 端继续走原有 Overlay 和 Named Pipe；macOS 不具备该 Windows 窗口的实际运行能力。
- 成就只展示原工程已有的解锁信息；不增加原 API 没有的领奖、任务或支付功能。

## 实现流程

1. 保留现有功能组件、绑定路径与请求接口，把已确认的 PNG 素材导入独立 `Cozy/` 目录。
2. `CatTheme.LoadCozy` 从 StreamingAssets 读取 PNG，缓存 Texture/Sprite；拒绝越界路径，分别处理图片与九宫格边界。
3. 主壳层在加载原 DesktopShell Prefab 后重排页头和底部导航；各页面挂载原有业务组件，不另造游戏数据。
4. `DesktopFeaturePanelFactory`、行卡片工厂和通用控件工厂接入主题，兼容 Prefab 与回退布局；动态添加的行卡片通过 `CozyPanelSkin` 接续主题。
5. `CozyButtonVisual` 监听鼠标、键盘和禁用状态，仅切换底板、图标/文字的视觉偏移，原 `Button.onClick` 继续处理业务。
6. 地图、商店/装备、背包、资料/成就、排行榜、社交页分别调整为暖纸卡片、可读字号和响应式布局，保留真实字段及交互。
7. 在 Unity 编辑器检查素材、编译、运行 EditMode 测试，再在 Windows 带 Steam/服务端的真实环境验收全部页面。

## 字体

优先使用 `Resources/DesktopTheme/Cozy/Fonts` 等目录中的中文 Font（或主题字体 AssetBundle），否则尝试系统中文字体：Windows 微软雅黑、macOS 苹方/黑体及 Noto Sans CJK 等。未复制受许可限制的系统字体。正式发行建议把具有再分发许可的中文字体导入 Resources，并在目标系统确认字形；不要把本机有中文字体等同于所有玩家机器都有。

## 验收步骤与验证范围

本次开发机器没有安装 Unity 编辑器或 C# 编译器，因此**不能把静态检查当成 Unity 编译、Play Mode 或真实联机验收**。

1. 用 Unity 2021.3.29f1c1 打开 `fish-social-unity`，等待导入，检查 Console；不要先重烘焙全部 Prefab，以免覆盖现有人工布局。
2. 执行菜单 `Fish Social → Cozy UI → 检查最终版素材`。
3. 打开 Test Runner，在 EditMode 执行 `CozyDesktopThemeTests` 和现有 `DesktopProtocolResponseTests`。测试覆盖路线稳定、素材加载、按钮三态、点击区域/事件不变、重复套皮、进度图层保护及路径边界。
4. 运行 `Assets/Scenes/DesktopMain.unity`，Steam 登录后逐一打开底部十一项导航。检查顶部金币真实同步、标题/列表不溢出，默认/悬停/按下视觉不跳动。
5. 世界地图拖拽缩放，鱼塘目录 → 地图定位 → 检查条件 → 选择计费档 → 确认进塘；不得绕过锁定或门票。
6. 购买真实商品、在装备页切换已拥有钓竿/已解锁鱼饵，验证装备显示与后端一致，装备页没有购买操作。
7. 背包出售/回鱼/分享、个人资料/相册、成就、排行榜、好友/私聊、动态点赞/评论等按原工程账号和服务端逐一验收。
8. Windows 检查桌宠 Overlay 恢复、隐藏主窗口、钓位/钓鱼/收杆/鱼获领取及新手引导限制；检查原有流程未受 UI 改动影响。
9. 以 1280×720 和用户实际桌面窗口大小进行截图复核；视觉验收尚未通过前，不把移植描述为最终已验收版本。

## 后续维护

只换外观优先替换 `Cozy/` 内同名 PNG 并保留尺寸/边界；按钮状态改 `CozyButtonVisual`，页面布局改对应 UI 组件。不要为了实现 UI 演示直接更改服务器经济规则，也不要把装饰图标染色与玩家真实猫咪形象/鱼种系统混为一谈。
