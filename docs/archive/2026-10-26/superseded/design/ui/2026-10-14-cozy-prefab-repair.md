# 桌面 UI 参考稿对齐与持久化修复

## 交付边界

- 基准：用户确认的「喵与鱼」横版首页（1672×941），樱花鱼塘、左侧猫咪、右侧菜单卡和底部七项导航。
- 修复 Unity 工程，不增加一套网页游戏，不改 Steam 登录、服务端经济、Socket 钓鱼或 Windows Overlay 的业务协议。
- 已提供实际 PNG/TextureImporter/Prefab 文件，不需要先运行旧 Bake 才能看到界面。
- 本机没有 Unity Editor；下文静态检查不是 Unity 编译、Play Mode、真实联机或用户视觉验收。不要将布局预览当作 Unity 截图。

## 根因与处理

### Unity 导入错误修正（2026-10-14）

- `CozyPrefabRepair.cs.meta` 原 GUID 有 33 位，Unity 因此忽略对应脚本，并导致 Catalog 和测试中的 `CozyPrefabRepair` 报 CS0103。已修正为 32 位十六进制 GUID；原无效值未被任何预制体或其他资源引用。
- 移除旧 `DesktopPrefabValidator` 的 `InitializeOnLoadMethod` 自动 Bake 回调，避免脚本重载/导入期间自动加载、检查和重建预制体；保留明确调用的编辑工具。此项消除一处自动加载入口，不代表已经确认递归序列化报错的完整调用链。
- 增加 Edit Mode 回归测试：所有 Assets `.meta` 的 GUID 格式/唯一性、修复脚本实际导入的 GUID、旧 Baker 不在域初始化时自动运行。
- 切回 Unity 等待重新导入；如仍使用修正前的脚本导入结果，对 `CozyPrefabRepair.cs` 手动执行 Reimport。无需删除整个 Library，也无需重新生成 UI 预制体。
- `Recursive Serialization is not supported` 尚需完整 Console 堆栈定位；目前没有本机 Unity 编译或运行验证，不能仅凭这条通用报错认定具体故障点。

1. 旧 shell 会在加载后删除并重建顶栏/十一项导航，覆盖美术调整。现在只绑定 Header、Nav 与十五个 Content 页面槽，不再重建布局。
2. 旧切图通过 StreamingAssets 的 Sprite.Create 临时产生，不能可靠存到预制体。新主题位于 Resources/Desktop/Theme/Cozy，优先加载真实导入的 Sprite；兼容回退默认关闭。
3. 旧页面/弹窗缺少完整预制体时会创建粗糙布局。现在缺失时明确报错，实际页面、模态容器、右键菜单、结算窗口和列表项均随提交提供。
4. 移除 Bind/Ensure 期间的布局重排。运行时保留已保存的锚点、位置、大小与组件引用，只更新内容、状态、数据列表和按钮回调。

## 文件与层级

| 用途 | 工程位置 |
| --- | --- |
| 主窗口 | Assets/Resources/Desktop/Prefabs/DesktopShell.prefab |
| 首页 | 同目录 PanelHome.prefab |
| 钓鱼、选塘、装备、成就 | PanelPond、PanelPondSelection、PanelEquipment、PanelAchievements.prefab |
| 世界地图/背包/商店/资料/编辑/排行/动态/社交/图鉴/设置 | 同目录对应 Panel*.prefab |
| 功能模态窗口 | FeatureModalRoot.prefab（包含四个完整页面），ProductContextMenu.prefab，PanelPondSettlement.prefab |
| 数据卡片 | PondSelectionCard、WorldMapPondMarker 和已有列表项 Prefab |
| 主题切图 | Assets/Resources/Desktop/Theme/Cozy/{buttons,panels,icons,icons-light,brand,characters,fish,scenes,textures} |
| 可核对的原稿 | docs/design/ui/references/approved-desktop-ui.png |
| 布局核对图（非 Unity 运行截图） | docs/design/ui/previews/authored-home-layout.png |
| 静态检查结果 | docs/design/ui/reports/cozy-prefab-static-validation.json |

主窗口：MainRoot/Header；MainRoot/Content/{Home,Pond,WorldMap,Shop,Friends,CatchBag,Gallery,Profile,ProfileEdit,SocialFeed,Leaderboard,Settings,Equipment,PondSelection,Achievements}；MainRoot/Nav/{CatchBag,Equipment,Shop,Achievements,Leaderboard,Profile,SocialFeed}。其他入口保留在顶栏、首页、资料页和右键菜单。

## 九宫格与按钮

- plate-default / plate-hover / plate-pressed 使用同一透明边距裁切范围，保证状态切换不跳尺寸；均为 Single Sprite、Full Rect、关闭 mipmap、Clamp。
- 按钮三态与 panels/paper 的 Sprite Border 为 `(64,64,64,64)`；对应 Image 为 `Sliced`。原有纸纹 texture border 为 32；角色、图标、背景图不做九宫格。
- Border 是源图像素，不是屏幕固定圆角；按钮已保存 pixelsPerUnitMultiplier，按实际尺寸配置，不通过运行时重排补偿。
- 每个普通 Button 的子节点包含 CozyVisual/Image、Icon/Image 与文字，CozyButtonVisual 序列化绑定 plate/icon/normalSprite/hoverSprite/pressedSprite。
- Button 的 Transition 为 None，SpriteState 的 hover/selected 对应 hover，pressed 对应 pressed，disabled 对应 normal。三态由 CozyButtonVisual 统一处理，避免 ColorTint 与状态贴图争抢。
- 按下使用确认方案：底色更深、视觉与内容轻微下移 3 个参考单位、默认外投影换为 pressed 切图；释放恢复原始位置，不累加位移。
- 七项导航默认是平面；悬停/选中/按下显示相应底板。原图标为单色雕刻 PNG，不新增彩色 SVG 图标。
- Dim、ModalPanel、MenuPanel 等用于阻止点击穿透的 Button 不属于装饰按钮，不加 CozyVisual，也不改成纸张背景。
- 中文字体在运行时复用现有系统字体加载与 glyph 检查（Windows 微软雅黑/macOS 中文字体），没有把系统字体或临时 Font 对象塞进 Prefab。需要跨平台一致的编辑器中文预览时，可将有分发授权的中文字体导入 Resources/Desktop/Theme/Cozy/fonts；本次官方字体下载连接失败，未声称已随包提供字体。

## 业务接入

- 开始钓鱼继续调用已有 OpenPond/Overlay 流程；世界地图、费用模式、钓位、商店购买/装备、鱼获出售/展示、资料、成就、排行、动态、好友与私聊继续使用现有 API 和控制器。
- 顶栏金币与昵称/等级/经验读取已有 GetCoins/GetProfileHub；经验上限来自现有 shared/generated/game-data/player_levels.json 的只读副本，不伪造服务端进度。
- 参考图右上第二数字在实现中表示当前背包鱼获数，不虚构新珍珠货币。
- 「每日礼物」目前服务器没有对应领取 API，因此入口指向商店并提示尚未开放，不添加本地伪奖励。
- 首页猫咪复用最新 v18 分层帧与已有皮肤选择；钓鱼页仍绑定 PetStateController。此修复不覆盖已更新的动画资源、服务端状态或动画测试修正。

## 编辑器工作流

1. 用 Unity 2021.3.29f1c1 打开 fish-social-unity，等待所有资源导入和脚本编译。
2. 通过现有唯一入口 `Fish Social → UI Prefab 管理` 检查/修复引用与导入设置；新预制体全部登记到 DesktopPrefabCatalog。
3. 直接双击对应 Prefab 修改布局。运行时不会把这些修改重新排回旧版；主窗口参考坐标为 1672×941，CanvasScaler Expand 保持布局比例，登录窗口仍为 480×320。
4. 切图需要与节点用途匹配：纸底/按钮用 Sliced，地图/头像/猫咪/鱼用 Simple 或 Filled；不要把图片内容或进度条当成纸底。
5. 通过 Test Runner 跑 EditMode 的 CozyDesktopThemeTests、CozyPrefabPersistenceTests，再跑原有桌面与钓鱼动画测试。

## 引擎验收清单（待执行）

- 确认 Console 无编译/资源缺失错误；加载 DesktopShell 和所有十五项页面，切换后没有多生成第二套按钮/布局。
- 在 1280×720、1920×1080、2560×1440 与宽屏窗口检查菜单和导航比例、背景裁切及弹窗位置。
- 测试默认/悬停/按下/释放、键盘焦点、禁用按钮；重复操作没有位移漂移，保存关闭再打开 Prefab 后贴图与引用仍在。
- 打开好友/私聊、鱼获详情/展示、资料编辑头像与鱼获选择、图鉴、排行榜、社交评论、设置、右键菜单与离塘结算；没有旧深色面板，遮罩无点击穿透。
- 正常 Steam 登录与既有服务端联机；校验金币/经验/购买/装备/鱼获/排行/好友仍是真实数据。
- 验证 v18 待机、坐下、挂饵、抛钩、咬钩、循环发力、成功/空军、复位与皮肤替换，确保 UI 修改没有破坏动画更新。

## 已执行的检查

- C# 语法树解析：45 个文件无语法错误（不等同类型检查/Unity 编译）。
- 持久化资源：73 张导入 Sprite；41 个预制体；212 个 Sliced Image；143 个序列化按钮状态。
- 检查本地 fileID、Sprite GUID、按钮状态一致性、所有主路由/组件/页面节点、切图尺寸和非零九宫格 border。
- 检查通用预制体迁移幂等性，保留原业务组件、事件与锚点；模态窗口内嵌页面与最新页面文件一致。
- 本机未执行 NUnit/Unity 编译/引擎渲染/Steam 联机验收，仍须按前述清单执行。
