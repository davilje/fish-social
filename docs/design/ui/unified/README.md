# 全桌面 UI 统一交付（2026-10-14）

## 范围与状态

本次同时覆盖 Unity 主窗口/功能页面/弹窗/数据卡片，以及 Windows WPF Overlay 的 HUD、按钮、聊天、气泡、悬停卡、玩家与产品菜单、调试窗口。复用同源奶油纸张、单色雕刻 PNG；不改 Steam 登录、认证、经济接口、钓鱼 IPC、钓位协议和 v18 分层帧动画。移动客户端、运营后台和操作系统原生文件选择器不属于桌面游戏 UI。

这是代码、资源和布局交付，不是用户验收通过。没有本机 Unity Editor 或 Windows WPF 运行环境；引擎编译、实际导入、窗口缩放、IME 和业务联机最终验收由用户进行。静态布局稿不是引擎截图。

## 修复日志中已确认的问题

- 新写入的 ScrollRect GUID 错误：`1aa08ab7e0800fa44ae55d278d1423e3`。已替换为旧版可工作的 `1aa08ab6e0800fa44ae55d278d1423e3`，不是修改 Steam 或认证。
- PostCommentRow 的 HorizontalLayoutGroup 错误 GUID 改为现有正确组件 GUID。
- 补齐 `PanelWorldMap/Details/ModeSell` 和 `ModeAuto`，以及 `PanelShop/Header/Title`、`PanelEquipment/Header/Title`，保持现有 Tabs/Tackle 业务名称。
- 去除 8 处重复 ScrollRect 和 1 处重复 HorizontalLayoutGroup，并重映射引用到保留组件；补全所有 ScrollRect 的 viewport/content 引用。旧社交页面以自身作为 viewport，保留原 `Scroll/Content` 业务路径。
- 首页左侧排行榜快捷入口只在首页显示，避免覆盖重新排版的功能页面。
- 新增 `CozyUiBuildGuard`：Player 构建前检查导入组件、完整节点、滚动引用、重复 ScrollRect 和 Variant 类型，阻止“登录成功但缺页”的资源继续进入构建包。

## Unity 组件库

资源目录：`fish-social-unity/Assets/Resources/Desktop/Prefabs/Library/`。

| 家族 | 基础 Prefab | Variant |
| --- | --- | --- |
| 按钮 | CozyButtonBase | Primary、Secondary、Quiet、Tab、Icon、Nav、Danger |
| 面板 | CozyPanelBase | Card、Dialog、Sidebar、Toolbar、Tooltip、Hud |
| 输入框 | CozyInputBase | Search、Multiline |
| 滚动视图 | CozyScrollBase | List、Grid |

共 21 个库资源，包含 4 个基础 Prefab 和 17 个 Variant。原 `Desktop/Prefabs` 下的 41 个界面、弹窗、列表项及 Overlay HUD/Actor 资源均转换为真正的 Prefab Variant，原文件名、GUID、业务组件和节点路径保留；页面里的按钮、输入框、滚动组件使用嵌套 Prefab 实例，不再只是脱离基础模板的复制品。

- 基础 PNG 状态、纸张九宫格和视觉脚本可沿继承链共享。
- 九宫格有三层合同：Resizable PNG 导入为 Single/FullRect Sprite 且 border 非零；背景 Image 保存为 Sliced；运行时 SkinTree 会将已有 border 的普通 Image 规范为 Sliced、关闭 preserveAspect。CozySpriteImporter 在导入时补全缺失的 border；保留既有非零自定义 border。经验进度图继续为 Filled，普通雕刻图标继续为 Simple + preserveAspect。构建预检会阻止无 border 的背景图片进入 Player，测试同时检查横长/小尺寸背景的实际网格顶点数，不只检查枚举值。
- Sprite Editor 的 border 与 Image 组件的 Type 是两项独立配置。若实际 `CozyVisual` 的 Image 未使用 Sliced，先保存并关闭 Prefab Stage，再打开 `Fish Social → UI Prefab 管理 → 仅修复 Image 的 Sliced 模式`。此操作直接修改实际导入的 Image `m_Type` 并记录 Variant/嵌套 Prefab 的属性覆盖，保存后卸载并重新加载检查；Console 显示资源路径、节点路径、原类型及修改数量。操作不重建布局、不导入素材、不改 border/PPU/Sprite/按钮事件。`CozyVisual` 不允许使用 Filled；仅真正的进度图保留 Filled。新增保存重载、错误类型及 Variant 覆盖回归测试，本机无 Unity，尚未执行这些 Edit Mode 测试。
- 各实例保留功能文字、图标、锚点、尺寸、显隐及序列化数据引用作为覆盖。
- 修改整个按钮体系：先改 CozyButtonBase；修改主操作类别：改 CozyButtonPrimary；只改一个页面的位置或文案：改该页面中的实例覆盖。
- 不要执行 Unpack 后再保存来调整颜色。业务列表运行时仍实例化卡片 Prefab，但不重新创建整页布局。
- `Fish Social → UI Prefab 管理` 已登记基础库和所有页面，支持 Library 子目录；不在脚本导入阶段自动 Bake。
- 已为美术组件库 `Assets/Resources/Desktop/Prefabs/Library` 和 Cozy `data` 配置添加精确 Git 忽略豁免；Unity 生成的根 Library 缓存和服务端 data 仍然忽略，避免工程同步时漏掉组件库和导出合同。

## 独立页面与效果稿

`page-designs.json` 记录 23 个页面/嵌入视图设计、19 种列表原型的画布、区域、颜色和完整路径合同。

- 功能页面基于实际 Shell 内容区 `(34,148,1604,636)`；模态内容区单独按 `1456×620` 排版。
- 包含选塘、世界地图、背包、装备、商店、成就、排行、个人资料、个人中心五区、资料编辑、动态、社交、图鉴、设置、钓鱼、结算、四类功能模态窗口、右键菜单、登录。
- 已确认的 PanelHome 首页不重新设计；首页角色仍由既有分层钓鱼动画驱动。
- 登录单独按运行时 `480×320` 小窗口排版，LoginRoot 使用中心锚点/中心 pivot、零偏移和 `480×320` 固定尺寸；登录成功后主界面继续使用 `1672×941`，两种参考尺寸不混用。登录效果稿同样输出为 `480×320`，构建预检与测试约束此合同。
- `previews/` 提供独立完整壳层布局稿及总览；部分列表稿展示模板示意，示例仅在预览脚本内存中使用，未写入运行时预制体，不伪造玩家、商品或奖励数据。
- 页面静态文案与加载态可编辑；真实玩家资料、地图开放条件、物品、排行榜和社交内容仍由现有业务绑定。

## Overlay 同源方案

- 同源 73 张 PNG 位于 `desktop-overlay/OverlayResources/hud/cozy/`，和 Unity Cozy 目录中对应 PNG 字节一致。
- `CozyTheme.xaml` 定义 WPF 按钮、输入框、滚动、菜单、Tooltip 模板；`CozyTheme.cs` 负责 PNG、纸张九宫格和图标染色。
- `overlay-hud.json` 保留 960×560 和全部 32 个 widget id。共享 `components` 与按钮 normal/hover/pressed、独立 icon 字段均为实际数据，不是说明示例。
- 三态按钮 PNG 为 407×400，border 为 64；按下只移动视觉内容 3px，点击区域不漂移；透明窗口、点击穿透和裁切预设保留。
- Unity `OverlayHud.prefab` 的同名 widget 矩形、图层和资源按这份 JSON 对齐；导出器保留 cozy 子目录、三态和 icon，不再导出回旧版单贴图。
- ARGB：Overlay `#FF75472E` 对应 Unity RGBA `#75472EFF`，不要直接按 RGBA 解析 Overlay 字段。
- 详见 `desktop-overlay/COZY_UI_HANDOFF.md` 的资源合同与 Windows 风险清单。

## 使用与用户验收

1. Unity 导入最新工作区资源，确认 Console 没有 Missing Script；原 GUID 修复类也必须已正常导入。
2. 在 `Fish Social → UI Prefab 管理` 查看所有库与页面资源。Inspector 的 Prefab 类型应为 Variant；基础四项是普通 Prefab。
3. Test Runner 可运行 CozyUiLibraryTests、CozyPrefabPersistenceTests 与既有桌面/动画测试。新的构建检查在 Player 构建之前执行，不自动改 UI。
4. 按项目原有方式编译 Windows `desktop-overlay/FishSocialOverlay.csproj`，包含新主题 XAML/C# 和同源 PNG；不要只复制一个旧 Overlay exe 或只复制 JSON。
5. 验收登录后的地图、选塘、商店、装备、背包、成就、资料、头像/鱼获选择、排行、动态、社交、图鉴、设置、结算、右键及 Overlay 菜单/聊天。
6. 实测 1280×720、1920×1080、宽屏与三种 Overlay 裁切预设、长文本、长列表、键盘焦点、按下/释放、禁用态、IME 和点击穿透。

每日礼物仍无服务端领取 API，本次不伪造领取成功；独立中文字体授权文件仍未新增，Windows 使用现有系统中文字体。`Recursive Serialization` 如果复现，必须保留新的完整堆栈，不能仅凭旧日志摘要认定已消失。
