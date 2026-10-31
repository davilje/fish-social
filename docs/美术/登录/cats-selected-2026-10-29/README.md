# 五猫登录背景 · 当前浅蓝青色相与饱和度调整 v2

2026-10-31：纠正上一版蓝色压暗的问题，从登录原图 login-background-fish-fixed-2026-10-30.png 重新编辑天空与湖水的蓝青色相、饱和度。保持明亮观感、原有粉彩笔触和猫咪构图。当前正式原画 [login-background-hue-saturation-v2.png](login-background-hue-saturation-v2.png)，使用内置 image_gen，提示词 [color-prompt-hue-saturation-v2.txt](color-prompt-hue-saturation-v2.txt)。源文件 exec-4c6bb80b-f7a0-41b9-9768-c97da416d44e.png。

湖水固定区域取样（x > 1100，420 < y < 510，并按原图蓝青色筛选）平均 HSV 饱和度由 0.528 至 0.304（约降低 42%），平均 CIELAB L* 从 74.24 至 78.95，结果更浅亮，没有继续压暗。统计只反映该取样区域，不是全图或逐像素色值锁定。

[统一预览 · 登录](../../../../现行预览.html#login) 默认显示当前 Unity 完整界面，支持 7 档窗口及原画前后对照。正式资源 GUID 保留。此前登录和鱼塘选择相关 23 项测试通过，此次更新原图后重新渲染和检查浏览器预览。验证文件在 output/login-map-hue-saturation-v2/。本轮未重新打包客户端。上一版 login-background-calm-2026-10-31.png 保留历史，不再正式使用。

以下为此前版本记录。

# 五猫登录背景 · 烤鱼细节修复

2026-10-30。用户确认采用柔和调色效果，现已替换 Unity 登录背景。

[统一预览 · 登录](../../../../现行预览.html#login) · [正式修复原图](login-background-fish-fixed-2026-10-30.png) · [调色前原画](login-background-cats-selected.png)

- 正式资源：`fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/scenes/login-background-cats-selected.png`。保留资源 GUID，DesktopShell / LoginRoot / SceneBackground 继续引用该资源。正式纹理与修复原图 SHA-256 一致，记录在 `manifest.json`。
- 适度降低环境黄绿、青色饱和度并柔化明暗对比，保留猫咪清晰轮廓与粉彩笔触；调色图来自内置 image_gen，提示词见 [color-prompt-2026-10-30.txt](color-prompt-2026-10-30.txt)。本轮直接应用已确认图片，无再次生成。
- Unity 2021.3.29f1c1 重新渲染 7 档完整窗口：1280×720、1600×900、1920×1080、2560×1440、1280×800、1024×768、2560×1080。统一预览默认显示正式调色后的 Unity 渲染，提供调色原画、调色前原画和 v4 对照；渲染 URL 加入版本参数避免缓存旧图。
- 登录场景测试 8/8 通过，检查认证层切换、背景覆盖比例、控件边界及猫咪与按钮间距。日志、测试 XML 在 `output/login-color-adoption-2026-10-30/`。
- 2026-10-29 原始采用版本为 `exec-3bcbc624-3a12-4706-89e4-8a7308c27c05.png`，保留为本目录的 `login-background-cats-selected.png`，用于调色前对照。[原绘图提示词见第一轮](../cats-v5/generation-prompt.txt)。
- 所有预览入口均使用根目录「现行预览.html#login」。旧 v4/v5/v6 独立网址跳转到统一入口，当前渲染页面仅作为统一预览的内嵌内容。

项目资源已更新；本轮未构建 Windows 包，也未做 Windows 实机登录验证。

## 2026-10-30 烤鱼细节修复

修正黑猫手中鱼的重复鱼头／眼睛结构，整理其他手持烤鱼及盘中两条鱼的头尾、鱼鳍和遮挡关系。以已采用柔和调色图为编辑底稿，内置 image_gen 局部修订，提示词见 `fish-fix-prompt-2026-10-30.txt`。修复图应用到同一正式纹理，保留 GUID；Unity 七档渲染已刷新。统一预览默认显示修复后的完整窗口，新增「修复前原画」便于核对。本轮日志、测试结果在 `output/login-fish-fix-2026-10-30/`。
