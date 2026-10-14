# 2026-10-14 桌面 UI 布局修复

基线开发用分支 67917a3。覆盖 Unity uGUI 预制体及 Windows WPF Overlay；没有修改 Steam/package 用户改动、网络、认证或钓鱼帧动画。

## 七项修复

1. 共用主壳返回首页、功能弹窗关闭、资料返回/选择器关闭、结算完成按钮改为右上锚定：anchorMin/anchorMax/pivot=(1,1)。复用原绑定，不新建另一套关闭按钮。主壳沿用原“返回首页”行为，不将关闭界面变成退出游戏。
2. Unity 图标来自 Assets/Resources/Desktop/Theme/Cozy/icons/*.png。图片本身已经是棕色雕刻，Image.color 再设深棕会叠色变黑；序列化及运行时都改为 white 保持原纹理。Overlay 原先使用 icons-light 的 alpha 后重新填纯色，失去雕刻明暗；现在直接显示同源 icons 原 PNG，旧 icons-light 配置兼容重定向，不新增图片。
3. 普通动作按钮保留原业务文字，有限 best-fit + 单行显示，图标垂直居中并给文字保留间距。鱼种卡片、多字段列表、动态正文和显式多行标签不强行压成单行。
4. 排行榜前三名置于顶部横排；下方为说明栏和右侧排名表。背包/图鉴/商店/装备改为左分类或说明、中央偏右列表、右侧详情。个人中心页签仍左侧固定导航、内容右侧，内部详情在最右。
5. 排查全部活跃按钮，无空白假动作按钮；保留已有业务操作、错误重试、相册翻页和调试入口（受原显隐规则控制），没有为缩减按钮而删掉功能。修复重复叠在同一槽位的 Overlay 菜单按钮，不新增按钮。
6. Overlay 图标按钮清空旧字体符号文字、居中；菜单雕刻 PNG 22px，其他小操作 16px。展开菜单九个槽位独立，折叠菜单仍保留原三个入口，小视口有纵向适配。
7. 聊天输入行至少 36px，上下内边距最多 4px；移除模板重复外边距和 MaxLines 高度约束，宿主连接实际垂直对齐。输入、占位、发送的行高一致；paper-compact 使用 10px 九宫格。Unity OverlayHud、组件导出 JSON、WPF 运行时资源映射/重导出同时修复，避免旧布局回写。

## 组件与交付

保留 21 个基础组件/Variant 和 41 个页面/列表 Variant。所有原功能路径、序列化点击/输入事件保留。新增 Unity Edit Mode 测试验证右上锚点、运行时图标不叠色及动作单行；本机没有 Unity，未运行这组引擎测试。独立静态效果稿在 previews/，只是序列化布局预览，不是引擎截图；示例列表项仅在预览存在，不写入业务 Prefab。

## 验证与运行

28 项 Overlay 静态回归通过；递归 materialize 全部预制体，检查事件/节点保留、关闭锚点、前三横排及没有活跃空白动作。验证报告 layout-refinement-validation.json。

Windows 上需要重新构建 Unity Player 和 desktop-overlay，部署完整 Overlay 输出（EXE/DLL/XAML 资源/OverlayResources），不能只替换 JSON 或继续运行原打包二进制。本次未修改 fish-social-unity/FishSocialOverlay 下旧编译产物。

实际 Canvas、Windows 字体、DPI、IME、长昵称/长商品名、窗口缩放与联机状态仍由用户运行验收。请优先检查右上返回、排名前三、背包详情与聊天输入完整字符；若仍出现黑色纯色图标，核对启动的 Overlay 输出与新版 CozyTheme.cs 是否同一次构建。
