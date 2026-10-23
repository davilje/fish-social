# 首页室内小屋接入 Unity

2026-10-23。将 cozy-journal 当前 v4 分层房间接入正式桌面工程，保持现有顶部资料卡、钓鱼世界入口与七项底部导航。正式启动链仍为 `DesktopAppBootstrap → DesktopShellUi → DesktopShell.prefab`，未切换到另一套 JournalV2 界面。

## 资源与预制体

- 资源：`fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/home-room/`。15 张独立透明摆件、空房间背景、两个原有橘猫姿势、10 猫种已有睡姿，以及与网页同源布局的 `manifest.json`。三支鱼竿复用一张切图，共 17 个独立摆件节点。运行切图已按展示尺寸优化，原始大图保留在网页 `assets/room-v4/`。
- 正式 `DesktopShell.prefab/MainRoot/HomeRoom` 已保存分层节点与组件引用；原室外 Background 关闭，原右下角 CozyCornerCat 移除。`PanelHome.prefab` 原展示内容关闭，保留旧节点供业务绑定与预制体兼容。
- 地毯按清单指定宽高展开，其余摆件按底部中心对齐、保持比例。没有窗光、地面光斑、摆件投影与角色脚底投影；原 UI 的纸张与按钮阴影保留。
- PNG 导入为 Sprite / FullRect / 无 mipmap / Uncompressed / Clamp，最大尺寸 2048，保持透明边缘。每个摆件可以在 Unity 层级内单独替换或调整。

## 本轮交互补充

点击三支鱼竿中的任一支，直接进入正式装备页。点击猫窝后，猫咪移向当前猫窝位置，抵达后使用当前皮肤的已有静态睡姿；窝前沿复用同一张窝图的下部区域遮挡，让猫咪位于窝里。点地面或触碰猫咪即可离窝。橘猫现使用新增步行动画；其他角色仍使用已有姿势平移，入窝过渡与睡眠呼吸帧留待补充。

猫窝、食水碗、猫爬架可以直接拖动，拖动阈值生效后取消点击动作；其他摆件固定。窗口失焦、打开页面或中断拖动时撤回本次未完成的摆放。移动已睡着猫咪的猫窝时，猫咪跟随；去窝途中搬动猫窝会更新目标。下表使用 1672 × 941 画布上的底部中心坐标，保证物件避开顶部 UI 与底部导航。

| 物件 | 横向范围 | 底部范围 |
| --- | --- | --- |
| 猫窝 | 170–370 | 675–785 |
| 猫爬架 | 180–350 | 600–665 |
| 食水碗 | 1430–1580 | 660–785 |

Unity 使用 `PlayerPrefs` 按当前登录账号保存本机布置，不上传服务器；网页评审使用独立的 localStorage。旧布置、损坏数据和越界坐标均有回退或限制。

2026-10-24 按用户“优先实现在工程项目里”的要求，橘猫首套序列帧已接入正式小屋。其余八种猫的新动作待制作，大肥鱼不在本轮范围。动作拆分、帧数、画布尺寸、方向版本及喂食换帧约定见 [小屋动画资源需求](2026-10-23-home-room-animation-requests.md)。接入状态不代表美术验收通过。

## 素材尺寸

可重复导出：`python3 scripts/art/export-home-room-runtime.py`，依赖 Pillow。保留母版，根据摆放矩形按最大展示约 2x 输出，导入时不强制缩放为 2 的幂。透明边缘使用预乘 Alpha 重采样；地毯按原画面地面投影尺寸重采样，角色图只裁透明留白，画风与布局不变。运行尺寸清单及逐文件预算见 [texture-budget.json](../../../fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/home-room/texture-budget.json)。

例如猫窝由 1586 × 657 降为 628 × 260，食水碗由 1393 × 454 降为 270 × 88，老鼠玩具由 1480 × 689 降为 116 × 55。15 张摆件的 RGBA 像素预算从 54,059,876 bytes（约 51.6 MiB）降为 8,207,200 bytes（约 7.8 MiB），减少 84.8%；这是纹理解码像素预算，不是实测进程内存。背景不计入此比较。

网页 `assets/room-runtime/` 与工程运行资源由同一脚本输出，网页的原尺寸切图浏览页继续保留用于母版检查。

## 角色与交互

`CozyHomeRoom` 统一管理一个首页角色。点击中央地面移动、拖动猫咪、轻触回应；方向键移动，Home 回中央，R 休息。脚底坐标限制在 x=580–1070、y=690–775，不进入导航区。拖动保留抓取偏移，打开页面或失去窗口焦点即取消拖动。

角色读取当前账号已选皮肤。橘猫待机仍使用原站立与坐姿，行动使用新序列帧；其他已登记猫种复用项目现有 rest / sleep 素材，资源缺失时不替换成橘猫。

无操作 12 秒后低频短距离漫游，30 秒后休息。打开功能页时隐藏角色与房间操作入口、暂停漫游，合页保留位置；失焦、后台暂停及减少动效均复用现有控制。减少动效时立即移动且不自动漫游。

“我的钓具”进入正式装备页；“鱼缸 · 展示柜”进入正式个人中心 Showcase 页签。原钓鱼世界、选塘、入塘及实际钓鱼状态与原生 Overlay 保持原业务路径，小屋漫游不会写入服务端钓鱼阶段。

## 编辑与检查入口

Unity 菜单：`Fish Social → Home Room → Apply approved layered room` 重建并保存房间；`Verify and render saved room` 校验保存结果并输出 `Library/HomeRoom/unity-home-room.png` 与 `verification.txt`。常规 Cozy Journal 重新制作入口也会建立 HomeRoom，避免恢复旧的右下角角色。

使用现有 `SaveJournalPrefab` 保存，保留嵌套预制体的规范别名与引用。

原生截图见 [首页](previews/home-room-unity/unity-home-room.png)、[窝内休息](previews/home-room-unity/unity-home-room-sleep.png)。Unity 编译、运行切图预算校验、保存后原生渲染通过。EditMode 共 143 项通过：25 项小屋专项及 118 项现有预制体、持久化、页面绑定、布局与动效回归。新增覆盖三支鱼竿直接点击、进窝/离窝、随窝移动、三件用品拖动限制、取消拖动、账号布置恢复与 10 猫种睡姿。逐项结果见 [verification.json](previews/home-room-unity/verification.json)。网页检查同时覆盖直接鱼竿入口、进窝、带猫移动、三件用品摆放/恢复、七项导航、图层与背景对比及 1280 窗口；见 [web-verification-v5.json](previews/home-room-unity/web-verification-v5.json)。

## 当前边界

摆件与缸内鱼仍为当前方案的示意陈列，尚未按账号的拥有列表、装备槽或展示柜鱼种动态替换。缸内鱼、水草、水体仍为一张独立鱼缸图；未制作缸内动画。网页工具栏的原背景对比与隐藏摆件属于评审工具，不加入正式玩家界面。

本次验证使用 Unity 编辑器及离线数据，不包含 Windows 打包、Steam 登录和真实服务器会话联调。

## 2026-10-24：正式小屋动画

选择橘猫后，点击猫咪播放 16 帧打滚并保留末帧；点击地面或使用方向键，播放 4 帧起步、12 帧循环步行、4 帧停步。左右版本分别加载。点击食盆执行倒粮 1 秒 → 行走到当前食盆位置 → 低头、咀嚼三轮、抬头，共 2.5 秒 → 清空粮层并走回中央可活动范围。进食使用 14 张图片，循环区间 `[3,11)`，按时间表展开为 30 次帧显示。

重复点击食盆不重启喂食。移动或拖动猫咪、拖动家具、切角色、切账号、打开页面、失焦、后台暂停、停用组件会取消未完成的喂食并清空临时粮层。减少动效时跳过倒粮运动和路径位移，进食显示静态关键姿态，仍执行粮量变化与结束逻辑。缺少整套资源的角色只提示动作准备中，不播放半套喂食。

### 实现与预制体

- `CozyHomeAnimation.cs` 加载清单与 PNG、校验画布和循环段、按毫秒取帧。角色采用固定画布、注册点和统一 `0.82` 显示倍率；不按每帧包围盒自适应缩放。
- `CozyHomeRoom.Animation.cs` 管理本地动作状态与角色位移，使用 uGUI `RawImage` 换纹理。这里不是 Animator AnimationClip，也不改变真实钓鱼会话。
- `DesktopShell.prefab/MainRoot/HomeRoom` 已保存 `FoodLayer`、`FoodPour`、食盆前景节点及组件引用。角色仍在家具前方，只有食盆前景位于角色上方；原 `Props/food-bowls` 保留拖放坐标与逻辑，其 Image 停用以免重复绘制。
- `CozyRoomBowlView` 将前景食盆的点击和拖动转发给原食盆组件，移动食盆时同步所有喂食图层。
- `Resources/Desktop/Theme/Cozy/home-room/animations/` 共 112 张独立 PNG、12 条动作清单、113 个帧引用；公共空粮层被重复引用。母版、审稿图和生成脚本不进入 Resources。

从同源 Web 素材更新运行资源：在仓库根执行 `node scripts/art/export-home-room-animation.mjs`。脚本校验清单、画布、PNG 数量与哈希，并生成稳定 GUID 的 `.meta`。Unity 菜单 `Fish Social → Home Room → Save and verify home animations` 保存正式预制体、验证完整交互链并生成 `Library/HomeRoomAnimation/` 原生截图。

### 验证与待验收项

小屋与动画专项 EditMode 55 项通过，覆盖真实资源、左右步行、三轮进食、固定注册点、重复点击、中断、减少动效、食盆原生射线命中与拖动同步；预制体、持久化、页面绑定、布局、组件库与动效回归 157 项通过，共 212 项。保存后的正式预制体已完成原生渲染；截图与逐项 XML 结果归档在 [home-room-animation-unity](previews/home-room-animation-unity/)。

源素材使用进食尺度修正版 `2026-10-24-eat-scale-2`。嘴部按当前画稿的左右接触参考坐标对齐，食盆前景已合成；逐帧嘴部/脚掌标定、跨批次花纹与比例、高清母版仍待美术验收。当前仅橘猫启用新增动作，其余八种猫和大肥鱼保留原行为。本轮没有执行 Windows Player 构建。
