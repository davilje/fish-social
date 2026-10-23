# 首页室内小屋接入 Unity

2026-10-23。将 cozy-journal 当前 v4 分层房间接入正式桌面工程，保持现有顶部资料卡、钓鱼世界入口与七项底部导航。正式启动链仍为 `DesktopAppBootstrap → DesktopShellUi → DesktopShell.prefab`，未切换到另一套 JournalV2 界面。

## 资源与预制体

- 资源：`fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/home-room/`。15 张独立透明摆件、空房间背景、两个原有橘猫姿势、10 猫种已有睡姿，以及与网页同源布局的 `manifest.json`。三支鱼竿复用一张切图，共 17 个独立摆件节点。运行切图已按展示尺寸优化，原始大图保留在网页 `assets/room-v4/`。
- 正式 `DesktopShell.prefab/MainRoot/HomeRoom` 已保存分层节点与组件引用；原室外 Background 关闭，原右下角 CozyCornerCat 移除。`PanelHome.prefab` 原展示内容关闭，保留旧节点供业务绑定与预制体兼容。
- 地毯按清单指定宽高展开，其余摆件按底部中心对齐、保持比例。没有窗光、地面光斑、摆件投影与角色脚底投影；原 UI 的纸张与按钮阴影保留。
- PNG 导入为 Sprite / FullRect / 无 mipmap / Uncompressed / Clamp，最大尺寸 2048，保持透明边缘。每个摆件可以在 Unity 层级内单独替换或调整。

## 本轮交互补充

点击三支鱼竿中的任一支，直接进入正式装备页。点击猫窝后，猫咪移向当前猫窝位置，抵达后使用当前皮肤的已有静态睡姿；窝前沿复用同一张窝图的下部区域遮挡，让猫咪位于窝里。点地面或触碰猫咪即可离窝。当前猫咪移动使用已有姿势平移；新增步行、入窝与睡眠呼吸帧留待补充。

猫窝、食水碗、猫爬架可以直接拖动，拖动阈值生效后取消点击动作；其他摆件固定。窗口失焦、打开页面或中断拖动时撤回本次未完成的摆放。移动已睡着猫咪的猫窝时，猫咪跟随；去窝途中搬动猫窝会更新目标。下表使用 1672 × 941 画布上的底部中心坐标，保证物件避开顶部 UI 与底部导航。

| 物件 | 横向范围 | 底部范围 |
| --- | --- | --- |
| 猫窝 | 170–370 | 675–785 |
| 猫爬架 | 180–350 | 600–665 |
| 食水碗 | 1430–1580 | 660–785 |

Unity 使用 `PlayerPrefs` 按当前登录账号保存本机布置，不上传服务器；网页评审使用独立的 localStorage。旧布置、损坏数据和越界坐标均有回退或限制。

打滚、倒粮及完整进食仍缺少序列帧，本轮按用户要求先列资源需求。猫咪保留原轻触回应；食水碗本轮只支持摆放。动作拆分、帧数、画布尺寸、10 猫种扩展及喂食换帧约定见 [小屋动画资源需求](2026-10-23-home-room-animation-requests.md)。

## 素材尺寸

可重复导出：`python3 scripts/art/export-home-room-runtime.py`，依赖 Pillow。保留母版，根据摆放矩形按最大展示约 2x 输出，导入时不强制缩放为 2 的幂。透明边缘使用预乘 Alpha 重采样；地毯按原画面地面投影尺寸重采样，角色图只裁透明留白，画风与布局不变。运行尺寸清单及逐文件预算见 [texture-budget.json](../../../fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/home-room/texture-budget.json)。

例如猫窝由 1586 × 657 降为 628 × 260，食水碗由 1393 × 454 降为 270 × 88，老鼠玩具由 1480 × 689 降为 116 × 55。15 张摆件的 RGBA 像素预算从 54,059,876 bytes（约 51.6 MiB）降为 8,207,200 bytes（约 7.8 MiB），减少 84.8%；这是纹理解码像素预算，不是实测进程内存。背景不计入此比较。

网页 `assets/room-runtime/` 与工程运行资源由同一脚本输出，网页的原尺寸切图浏览页继续保留用于母版检查。

## 角色与交互

`CozyHomeRoom` 统一管理一个首页角色。点击中央地面移动、拖动猫咪、轻触回应；方向键移动，Home 回中央，R 休息。脚底坐标限制在 x=580–1070、y=690–775，不进入导航区。拖动保留抓取偏移，打开页面或失去窗口焦点即取消拖动。

角色读取当前账号已选皮肤。橘猫复用网页的原始站立与坐姿；其他已登记猫种复用项目现有 rest / sleep 素材，不新增角色画风。移动暂用现有姿势平移，没有新增逐帧步行动画。

无操作 12 秒后低频短距离漫游，30 秒后休息。打开功能页时隐藏角色与房间操作入口、暂停漫游，合页保留位置；失焦、后台暂停及减少动效均复用现有控制。减少动效时立即移动且不自动漫游。

“我的钓具”进入正式装备页；“鱼缸 · 展示柜”进入正式个人中心 Showcase 页签。原钓鱼世界、选塘、入塘及实际钓鱼状态与原生 Overlay 保持原业务路径，小屋漫游不会写入服务端钓鱼阶段。

## 编辑与检查入口

Unity 菜单：`Fish Social → Home Room → Apply approved layered room` 重建并保存房间；`Verify and render saved room` 校验保存结果并输出 `Library/HomeRoom/unity-home-room.png` 与 `verification.txt`。常规 Cozy Journal 重新制作入口也会建立 HomeRoom，避免恢复旧的右下角角色。

使用现有 `SaveJournalPrefab` 保存，保留嵌套预制体的规范别名与引用。

原生截图见 [首页](previews/home-room-unity/unity-home-room.png)、[窝内休息](previews/home-room-unity/unity-home-room-sleep.png)。Unity 编译、运行切图预算校验、保存后原生渲染通过。EditMode 共 143 项通过：25 项小屋专项及 118 项现有预制体、持久化、页面绑定、布局与动效回归。新增覆盖三支鱼竿直接点击、进窝/离窝、随窝移动、三件用品拖动限制、取消拖动、账号布置恢复与 10 猫种睡姿。逐项结果见 [verification.json](previews/home-room-unity/verification.json)。网页检查同时覆盖直接鱼竿入口、进窝、带猫移动、三件用品摆放/恢复、七项导航、图层与背景对比及 1280 窗口；见 [web-verification-v5.json](previews/home-room-unity/web-verification-v5.json)。

## 当前边界

摆件与缸内鱼仍为当前方案的示意陈列，尚未按账号的拥有列表、装备槽或展示柜鱼种动态替换。缸内鱼、水草、水体仍为一张独立鱼缸图；未制作缸内动画。网页工具栏的原背景对比与隐藏摆件属于评审工具，不加入正式玩家界面。

本次验证使用 Unity 编辑器及离线数据，不包含 Windows 打包、Steam 登录和真实服务器会话联调。
