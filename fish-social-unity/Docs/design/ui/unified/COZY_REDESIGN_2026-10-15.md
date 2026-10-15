# 桌面八页 UI 重排与资源接入 · 2026-10-15

## 范围与实现

本轮以确认的八张桌面效果稿为排版方向，修改 Unity 内实际可交互的 uGUI 页面，不把整张效果稿贴成界面，也不改成 UI Toolkit。页面仍使用 `Canvas / RectTransform / Image / Button / Text / ScrollRect / InputField`，已有 REST、好友、商品、装备、鱼获和动态逻辑继续复用。

全部尺寸以 1672 × 941 的桌面画布为基准。内容面板起点为 (160, 150)，大小 1370 × 650；顶部 96 像素为独立标题区。关闭按钮固定标题区右上角，内距 (24, 22)，大小 52 × 52。原悬浮 `Reflow_PageSeal` 和重复标题禁用，页面图标并入标题栏。整体仍由现有 CanvasScaler 缩放；这不是移动端布局。

## 八页布局

| 页面资源 | 本轮布局 |
| --- | --- |
| PanelLeaderboard | 上方 2 / 1 / 3 排列的纵向领奖台；下方滚动榜单及本人排名；右栏分类和榜单说明。分类不再被旧 HorizontalLayoutGroup 推出面板。 |
| PanelProfileHub | 左侧 210 宽导航，中间头像、昵称、签名、ID 与真实统计，右侧代表鱼获；保留资料编辑、展示柜、图鉴和相册接线。 |
| PanelAchievements | 取消个人中心式侧栏占位；左侧条件与完成状态列表，右侧完成数量概览。 |
| PanelSocial | 顶部鱼塘 / 私信切换；左侧好友或在线钓友，右侧聊天记录；输入栏、发送按钮独立 56 高，文字留足上下内边距。 |
| PanelCatch | 左侧鱼获分类，中间三列卡片，右侧选中鱼获详情与既有操作；资源路径仍兼容 Grid/Viewport/Slots。 |
| PanelEquipment | 左侧分类，中间两列较宽装备卡，右侧插画、描述和装备操作；沿用商品拥有 / 装备状态。 |
| PanelShop | 左侧分类，中间三列商品，右侧详情与现有购买 / 选择规则；隐藏旧详情装饰和重试入口。 |
| PanelSocialFeed | 左侧公开 / 好友筛选，中间动态流，右侧分享说明和真实背包分享入口；配图左对齐，卡片随正文、图片、评论展开调整高度。 |

## Prefab 组件关系

- 八个页面继续保存为现有基础面板的 **Prefab Variant**，不是运行时临时拼接出来的替代页面。
- 新增 `Library/CozyPageHeader.prefab`，继承 `CozyPanelToolbar`，嵌套已有 `CozyButtonBase` 关闭按钮。页面均嵌套这套统一标题组件。
- 修改 `CatchSlot`、`ShopItemCard`、`LeaderboardRow`、`AchievementRow`、`SocialPostCard` 的实际列表模板；实例化和复用逻辑沿用原工程。
- `DesktopShell` 保存八页内容槽的统一尺寸。`DesktopShellUi` 负责为标题组件的关闭按钮绑定返回主页，为动态页背包入口绑定现有路由。
- `DesktopLeaderboardPanel`、`DesktopProfileHubPanel` 检测到 `DesignHeader` 时跳过旧版挤压布局代码。运行时只更新内容、交互与选中态，不再覆盖新页面的几何排版。
- 有边框的纸质底板继续使用 Sprite border + `Image.Type.Sliced`；插画、头像和功能图标使用 Simple + preserveAspect。进度条允许 Filled；不能为了“所有都 Sliced”而破坏填充逻辑。

## 新增资源

目录：`Assets/Resources/Desktop/Theme/Cozy/items/`。

- `items-atlas.png`：透明背景手绘物品插画，4 × 3；12 个独立 Sprite：四种鱼竿、六种鱼饵、两种船只。
- `fish-atlas.png`：透明背景鱼获插画，3 × 2；6 个独立 Sprite：青鱼、锦鲤、大口黑鲈、草鱼、鲤鱼、黑鱼。
- `CozyItemArt` 从 Resources 读取已命名的子 Sprite，按已有商品分类 / 名称或鱼种 ID 绑定；不依赖编辑器文件路径。
- 这两张是 **Multiple Sprite** 图集，单 Sprite 修复工具跳过 `items/*-atlas.png`，避免再次维修九宫格时毁掉子图映射。
- 其余纸质底板、按钮三态、雕刻图标及湖景继续复用工程既有 `Theme/Cozy` 资源；没有额外引入黑色字库图标。

## 与效果稿示例的边界

效果稿中的数值和部分入口并不等于现有服务端功能。本轮不虚构新后端：

1. 鱼饵不是可批量购买、持有库存的背包物品；现有机制为等级解锁、咬钩按规则扣金。没有加入示例数量加减、库存与虚假的总价逻辑。
2. 背包当前只展示真实鱼获，未伪造鱼饵、道具数据页签。
3. 成就接口只给条件和 unlocked，不提供数值进度；显示“已完成 / 未完成”及真实完成数量，不拿示例百分比代替真实进度。
4. 动态沿用公开 / 好友筛选及背包分享，未增加接口不支持的“我的动态”筛选或伪发布器。
5. 仅六种鱼有新增专属插画。其他鱼种保留真实文字与数据，不用错误的鱼种插画冒充。需要后续补齐美术资源，不能宣称已补齐全部鱼种。
6. 资料、头像、金币、排名、商品价格和装备状态在实际游戏中全部由现有数据更新。预览图的示例数据只在临时预览场景内创建，绝不写入交付 Prefab 或服务端。

## 编辑与复核入口

Unity 菜单 `Fish Social / Cozy Redesign`：

- `Inspect Saved UI`：导出实际保存的层级、尺寸、文字、布局组件到 `Library/CozyRedesign.inspect.txt`。
- `Apply Approved Eight Page Layouts`：显式重新应用本轮布局、图集切片及模板。会覆盖这些对象的设计尺寸，手动改版后不要无意重复执行。不会在资源导入时自动执行。
- `Validate Saved Layouts`：复用 Player 构建前检查并验证统一关闭按钮与有 border 的 Image 类型；报告 `Library/CozyRedesign.validation.txt`。
- `Render Saved Prefab Previews`：从已保存的 Prefab 创建可丢弃 PreviewScene，填入示例数据并用 Unity Camera 渲染八页，输出 `Library/CozyRedesignPreviews`；不修改当前场景，不登录、不购买、不发送社交请求。

回归测试：`Assets/Tests/Editor/CozyRedesignLayoutTests.cs`，检查八页 Variant、右上关闭锚点、Sliced、占位文案、网格列数、18 个图集子 Sprite、排行榜绑定不挤压领奖台、个人页不挤压侧栏，以及现有 Player Prefab 契约。

这些检查不替代联网验收、Windows Player 和原生 Overlay 联调。本轮范围是上述 Unity 八页及共用壳层，不代表原生 WPF Overlay 已按新八页稿重新设计。最终视觉和业务验收由用户进行。
