# 五猫登录背景 · 正式采用

2026-10-29。用户明确选定 `exec-3bcbc624-3a12-4706-89e4-8a7308c27c05.png`（v5 的第一轮生成图），作为当前登录背景。

[完整窗口预览](index.html) · [采用原图](login-background-cats-selected.png)

- 原图原样复制到文档与 Unity 资源，三者 SHA-256 一致。不是随后简化的 v5 或笔触修订 v6。
- 正式资源：`fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/scenes/login-background-cats-selected.png`。DesktopShell / LoginRoot / SceneBackground 已引用该资源，保留全窗口等比覆盖与独立登录控件。
- Unity 2021.3.29f1c1 完成 7 档窗口实际渲染（1280×720、1600×900、1920×1080、2560×1440、1280×800、1024×768、2560×1080），预览页默认展示实际 Unity 渲染，可切换原画及旧版背景。
- 登录场景 EditMode 测试 8/8 通过，检查认证层切换、背景覆盖比例、控件边界及猫咪与按钮间距。日志／XML：`output/login-selected-2026-10-29/`。
- 本轮未生成图像；采用此前内置 image_gen 的产出，[原提示词见第一轮](../cats-v5/generation-prompt.txt)。本轮未构建 Windows 包，旧客户端需重新打包；未做 Windows 实机登录验证。
