# Overlay 桌面手账素材与轻量交互收口

日期：2026-10-23。代码、资源和 Unity 预制体已更新；Windows WPF 构建、实机视觉与联机验收待执行。

## 本轮行为

- Overlay 按钮三态、聊天底板、输入框、提示气泡、右键菜单使用桌面手账同源素材；图标和头像从桌面资源同步。PNG 文件逐字节一致，保留可复跑的同步脚本和 SHA-256 清单。
- 按钮只切换三态并下移 1 DIP，取消点击 1.1 倍回弹；聊天与角色提示气泡只做 120ms 线性淡入、淡出。聊天停留 5 秒，钓鱼角色的抛竿、收竿、钓获、脱钩、复位帧序与时间保持原实现。
- 正常鱼塘的入包、回鱼、脱钩不再下发 Overlay 弹窗。WPF 同时过滤旧主程序发来的 `hook_keep / hook_return / hook_escape`，不发送额外确认或领取命令。真实结算事件、计数、背包和自动回鱼业务继续执行。
- 移除常驻状态／鱼塘／钓位信息卡、预制体节点与点击命中区；旧 HUD JSON 加载时过滤这些节点。真实连接错误仍可在顶部短暂显示，提示不拦截鼠标。
- 底部保留聊天、开始／收杆、打窝、离席和退出鱼塘；右侧空间按实际退出按钮计算。960×560、800×400、600×300 沿用视口重排与聊天宽度收缩。
- 新手引导的教学操作提示（上钩操作和教学完成）继续存在，避免中断教学；主窗口的离塘收获总账不属于本轮被移除的单次结算卡。

## 素材来源与适配

桌面源目录：`fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/`。
Overlay 目录：`desktop-overlay/OverlayResources/hud/cozy/`。

| 用途 | 同源文件 | WPF 纹理采样缩放 | WPF 九宫格边距 |
|---|---|---:|---:|
| 按钮正常／悬停／按下 | `手账/surfaces/button-quiet-{default,hover,pressed}.png` | 0.25 | 14 |
| 聊天、菜单和大提示纸张 | `手账/surfaces/paper-aside.png` | 0.5 | 22 |
| 输入框及紧凑提示纸张 | `手账/surfaces/input-default.png` | 0.5 | 14 |
| 图标、头像 | `icons/*.png`、`characters/avatars/*.png` | 1 | 不适用 |

原文件不重绘、不压缩。`CozyTheme.LoadImage` 只对 WPF 内存中的 surface 纹理采样尺寸做归一化，避免把 Unity 资源的 56px 按钮边框硬塞进 36 DIP 的按钮。HUD `spriteSlice` 为归一化后的纹理像素，不应反写覆盖桌面源 Sprite 的 border。

同步：`python3 scripts/美术/sync-overlay-journal-assets.py`。共 39 个素材（5 个底板、24 个图标、10 个头像）。来源、目标、哈希及缩放参数见 `desktop-overlay/OverlayResources/hud/journal-assets.json`。

## 导出与部署

1. Unity `Fish Social → 补齐 OverlayHud` 更新预制体；当前 `OverlayHud.prefab` 已完成更新。
2. `Fish Social → Export Overlay HUD` 导出布局、同源图和字体。内置 Arial 无可分发资源，导出时省略 fontFile，由 WPF 的 Microsoft YaHei UI / Segoe UI 回退；自定义字体仍按资产导出。
3. 在 Windows 执行项目现有 Overlay 打包流程。必须发布新 EXE **及整个 OverlayResources**；仅拷贝 JSON 不能移除旧 EXE 的 XAML 状态栏和弹窗逻辑。
4. 旧 Python YAML 导出器不能解析继承自按钮预制体的节点，现会拒绝不完整导出、保护原 JSON；正式导出走 Unity。

## 验证与边界

- Python 源与资源检查：34/34。覆盖同源素材哈希、XAML 引用、按钮／聊天尺寸、布局重排、弹窗屏蔽、命令保留及原有角色动画关键方法。
- Unity EditMode 专项：5/5。用真实 `OnFishCatchSettled / OnFishMiss` 检查计数、事件、动画状态与空弹窗字段；加载保存的 OverlayHud，并完整导出到临时目录，验证 5 张底板与桌面源逐字节一致、状态卡不再存在；另含路径与九宫格导出契约。
- Unity 项目编译通过；24 个 WPF C# 文件 Roslyn 语法检查通过；这不是 WPF/BAML 编译。
- Python 备用导出拒绝未解析的继承节点，原 JSON 内容保持不变。
- 当前机器无 Windows WPF 运行环境，未生成新原生 EXE；未声称 Windows 实际截图、100%/150% DPI、点击穿透、IME 或联机验收完成。验收时在三种窗口尺寸分别检查底部操作、展开聊天和菜单，连续观察钓获／回鱼／脱钩无卡片遮挡，检查角色动画、入包及金币结果。

机器可读结果：`docs/美术/手账/production/overlay-journal-validation.json`。
