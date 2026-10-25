# 五猫登录场景 v4：完整窗口与统一透视

2026-10-26。针对用户截图中的“旧场景铺满窗口、新背景缩在中央卡片”修复，并重绘五只猫围坐吃烤鱼的场景。已接入 Unity 项目；尚未重打 Windows 客户端或完成 Windows 实机验收。

[查看对照预览](index.html) · [1920×1080 Unity 渲染](unity-login-1920x1080.png) · [背景原图](login-background-cats-v4.png)

## 原因与修复

程序已按桌面窗口尺寸启动，但 LoginRoot 仍固定为 480×320，且旧主菜单 Backdrop 一直显示。先前 v2/v3 的渲染只隔离了登录卡片，替换了 CanvasScaler 的参考尺寸，因此没有覆盖完整画布上的问题。旧的两个小卡片测试已被完整窗口回归替换。

- LoginRoot 改为全窗口拉伸，删除旧卡片 Image 与不必要的 RectMask2D。不能仅禁用 Image，主题应用会重新启用它。
- 独立 SceneBackground 使用 AspectRatioFitter / EnvelopeParent 等比覆盖；宽高比不同时裁切外沿，不拉伸猫咪，不留出旧背景。
- 登录、进入主界面、退出登录统一切换 LoginRoot / MainRoot / Backdrop。主菜单背景只在已登录状态显示；Prefab 初始也关闭 Backdrop。
- 保留生产 CanvasScaler 的 1672×941、宽高匹配 0.5，登录控件与背景独立。控件置于上方留白，超宽窗口下登录按钮仍与最高的猫耳留有间距。

## 画面与资源

采用轻俯视草岸构图，岸线弯向右后方湖面；鱼盘使用俯视椭圆，五只猫按前后距离围坐。黑猫、奶牛猫、狸花猫「丧彪」、橘猫、三花猫的花色以项目角色参考图为依据。保留粉彩笔触、薄荷湖面与简化树景；猫咪为静态背景的一部分。

使用内置 image_gen 编辑生成，参考上一版场景与项目五猫参考板；完整提示词在 [generation-prompt.txt](generation-prompt.txt)。生成原图未经像素加工，复制到正式资源，二者 SHA-256 一致，见 [manifest.json](manifest.json)。

正式资源：`fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/scenes/login-background-cats-v4.png`，1672×941，Sprite Single、sRGB、Bilinear、Clamp、无压缩、无 mipmap。

## 验证与复现

Unity 2021.3.29f1c1 完整 Shell 离屏渲染 7 档：1280×720、1600×900、1920×1080、2560×1440、1280×800、1024×768、2560×1080。实际渲染使用生产缩放和主题，不隔离登录卡片。预览中的 Unity 模式展示这些渲染图片，按钮并非可执行的 Steam 登录。

`CozyLoginSceneTests` 与 `CozyUiLibraryTests` 合计 45/45 通过，覆盖认证根节点切换、主题应用后卡片不复现、背景覆盖及比例、控件边界、按钮与猫咪间距、五猫左右裁切边界。日志与 XML 在 `output/login-cats-v4/`。Web 的 7 档尺寸、3 种对照、图片加载、390px 页面宽度检查通过，无 JavaScript 错误。

重新应用与渲染：在 Unity batchmode 使用 `-executeMethod FishSocial.Desktop.Editor.CozyLoginSceneAuthoring.ApplyAndRender -quit`；只重渲染可用 `RenderAll`。测试过滤器为 `FishSocial.Tests.Editor.CozyLoginSceneTests;FishSocial.Tests.Editor.CozyUiLibraryTests`。

上述为编辑器渲染与自动测试，不代表 Windows 原生窗口、DPI 或 Steam 实机登录已验证。既有 Windows 包不会自动获得项目资源修改，需重新打包后检查启动、登录、退出登录与窗口缩放。
