# 首页室内小屋接入 Unity

2026-10-23。将 cozy-journal 当前 v4 分层房间接入正式桌面工程，保持现有顶部资料卡、钓鱼世界入口与七项底部导航。正式启动链仍为 `DesktopAppBootstrap → DesktopShellUi → DesktopShell.prefab`，未切换到另一套 JournalV2 界面。

## 资源与预制体

- 资源：`fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/home-room/`。15 张独立透明摆件、空房间背景、两个原有橘猫姿势，以及与网页同源的 `manifest.json`。三支鱼竿复用一张切图，共 17 个独立摆件节点。
- 正式 `DesktopShell.prefab/MainRoot/HomeRoom` 已保存分层节点与组件引用；原室外 Background 关闭，原右下角 CozyCornerCat 移除。`PanelHome.prefab` 原展示内容关闭，保留旧节点供业务绑定与预制体兼容。
- 地毯按清单指定宽高展开，其余摆件按底部中心对齐、保持比例。没有窗光、地面光斑、摆件投影与角色脚底投影；原 UI 的纸张与按钮阴影保留。
- PNG 导入为 Sprite / FullRect / 无 mipmap / Uncompressed / Clamp，最大尺寸 2048，保持透明边缘。每个摆件可以在 Unity 层级内单独替换或调整。

## 角色与交互

`CozyHomeRoom` 统一管理一个首页角色。点击中央地面移动、拖动猫咪、轻触回应；方向键移动，Home 回中央，R 休息。脚底坐标限制在 x=580–1070、y=690–775，不进入导航区。拖动保留抓取偏移，打开页面或失去窗口焦点即取消拖动。

角色读取当前账号已选皮肤。橘猫复用网页的原始站立与坐姿；其他已登记猫种复用项目现有 rest / sleep 素材，不新增角色画风。移动暂用现有姿势平移，没有新增逐帧步行动画。

无操作 12 秒后低频短距离漫游，30 秒后休息。打开功能页时隐藏角色与房间操作入口、暂停漫游，合页保留位置；失焦、后台暂停及减少动效均复用现有控制。减少动效时立即移动且不自动漫游。

“我的钓具”进入正式装备页；“鱼缸 · 展示柜”进入正式个人中心 Showcase 页签。原钓鱼世界、选塘、入塘及实际钓鱼状态与原生 Overlay 保持原业务路径，小屋漫游不会写入服务端钓鱼阶段。

## 编辑与检查入口

Unity 菜单：`Fish Social → Home Room → Apply approved layered room` 重建并保存房间；`Verify and render saved room` 校验保存结果并输出 `Library/HomeRoom/unity-home-room.png` 与 `verification.txt`。常规 Cozy Journal 重新制作入口也会建立 HomeRoom，避免恢复旧的右下角角色。

使用现有 `SaveJournalPrefab` 保存，保留嵌套预制体的规范别名与引用。

原生截图见 [unity-home-room.png](previews/home-room-unity/unity-home-room.png)。Unity 编译、资源同源校验、保存后原生渲染通过。EditMode 共 127 项通过：9 项小屋专项及 118 项现有预制体、持久化、页面绑定、布局与动效回归。自动化验证覆盖资源、摆件数量、单角色、移动边界、点击唤醒、拖动偏移与取消、页面暂停、后台暂停、减少动效、家具入口和原生 UI 射线命中；逐项结果见 [verification.json](previews/home-room-unity/verification.json)。已同步修正隐藏旧首页菜单的 disabled surface 保存状态，保持原预制体验证契约。

## 当前边界

摆件与缸内鱼仍为当前方案的示意陈列，尚未按账号的拥有列表、装备槽或展示柜鱼种动态替换。缸内鱼、水草、水体仍为一张独立鱼缸图；未制作缸内动画。网页工具栏的原背景对比与隐藏摆件属于评审工具，不加入正式玩家界面。

本次验证使用 Unity 编辑器及离线数据，不包含 Windows 打包、Steam 登录和真实服务器会话联调。
