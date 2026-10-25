# 登录背景：湖畔晴日与五猫烤鱼

当前正式版本：[五猫完整窗口与透视修复 v4](cats-v4/README.md) · [完整窗口效果预览](cats-v4/index.html)。下方保留 v1/v2 历史记录；其中 480×320 的适配假设已失效，不能作为当前运行验收依据。

2026-10-26。用户明确要求以当前 Overlay 钓鱼场景画风重做登录背景。先交付背景原画及 Web 设计预览；用户于 2026-10-26 确认应用 v2，现已替换 Unity 登录背景，登录逻辑未改动。

- 原画：`login-background-v1.png`，1536×1024，3:2；适配现有 480×320 登录窗口。
- 预览：`index.html`。沿用现有 Logo、提示文字和布局坐标：Logo (144,16,192,70)、提示 (32,156,416,38)、按钮 (64,206,352,54)；按钮样式为 HTML 示意，不代表 Unity 像素级验收。
- 风格基线：当前 Overlay `ponds/series/pastel-v1/lake-01-mint.png` 与 `pond-01-mint.png`。粉彩绘本块面、薄荷树冠、奶油阳光、湖蓝水面。
- 构图：左侧白桦与下方花草围合，中央开阔天空和湖面供动态 UI 展示。文字、Logo 和按钮不烘焙进图片。
- 生成方式：内置 image_gen 工具，以两张项目场景图为风格参考。完整提示词见 `generation-prompt.txt`。原始生成图未做像素编辑，复制到本目录。
- 接入建议：以单张背景替换 LoginRoot 的底图，适配 3:2 等比显示；现有纸面 Reflow_BrandStage 若整面覆盖需同步调整，避免遮住场景。保留控件为独立 UI。当前只验证 Web 预览，不声明 Unity／Windows 实机通过。

## v2：按用户反馈重新绘制

用户指出 v1 的细节密度、纹理与绘画方式不符，景物过多。v2 改为侧视草岸构图：裁切的侧边树、低远林、草岸和底部水面，删除花丛、前景围合石堆和纵深湖湾，扩大中央天空留白。以 Overlay 原始粉彩画风截图为唯一生成参考，采用较大色块与概括轮廓。不是对 v1 做模糊滤镜。

默认预览现为 `login-background-v2.png`（1536×1024）；支持切换上一版。v1 留作被否定方案对照，后续已确认应用 v2，见下方采用记录。新增参考 `style-reference.png` 是项目此前的 Overlay 画风源截图（含角色和 UI 仅供参考）；它不是当前运行场景的实机截图。生成方式为内置 image_gen，完整提示词为 `generation-prompt-v2.txt`。用户随后确认采用 v2。

## 正式采用（2026-10-26）

- 正式纹理：`fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/scenes/login-background-v2.png`，与 v2 原图 SHA-256 一致。
- `DesktopShell.prefab` 的 `LoginRoot` 改用新背景，Image 为 Simple／Preserve Aspect；原 Reflow_BrandStage 已处于关闭状态，无需修改。其余控件布局、文案、绑定保持原样。
- 导入：Sprite Single、1536×1024、sRGB、Bilinear、Clamp、无压缩、无 mipmap、不可读；3:2 适配现有 480×320 登录窗口。
- Unity 2021.3.29f1c1 编译、Prefab 校验、主题应用后背景引用检查完成。实际离屏渲染为 `unity-login-480x320.png`、`unity-login-960x640.png`；HTML 按钮仅为设计示意，正式控件效果以 Unity 图为准。
- 本次为项目资源替换，没有生成新的 Windows 安装包，也未在 Windows 实机登录。

现有 EditMode 登录测试 2/2 通过：`LoginCardMatchesSmallWindowCanvas`、`VisibleLoginGraphicsStayInsideSmallWindow`。日志与测试 XML 位于 `output/login-background-v2/`。
