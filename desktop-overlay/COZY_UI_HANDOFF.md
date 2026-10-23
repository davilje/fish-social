# Windows Overlay 手账 UI 交接

2026-10-23 更新；本文件替代旧 staging 交接说明。完整变更与验证见 [Overlay 桌面手账素材与轻量交互收口](../docs/design/ui/2026-10-23-overlay-journal-refinement.md)。

## 当前实现

- WPF 和 Unity 导出统一引用 `cozy/journal/surfaces/`，不再使用旧 plate 按钮和旧 paper 面板。图标、头像使用桌面同源文件。
- `python3 scripts/art/sync-overlay-journal-assets.py` 从仓库根执行，同步 39 个文件；`OverlayResources/hud/journal-assets.json` 记录源路径、SHA-256、采样比例与切片。
- 按钮源图采样比例 0.25、九宫格 14；paper-aside 比例 0.5、九宫格 22；input-default 比例 0.5、九宫格 14。源 PNG 字节保持不变。不要将归一化后的 WPF 切片写入桌面源 Sprite。
- 按钮三态按下位移 1 DIP，取消 click pulse；气泡 120ms 淡入／淡出，保留 5 秒停留。角色钓鱼帧动画保持原实现。
- 已移除 `cap_status / txt_status / txt_pond / txt_spot`；过滤旧配置，XAML 和点击穿透命中也同步移除。顶部仅保留不接收点击的短暂错误提示。
- Unity 不再下发正常单次结算弹窗；WPF 也忽略 `hook_keep / hook_return / hook_escape`。不自动发确认、不重复领取。新手教学提示与主窗口离塘总账保留。
- `overlay-hud.json` 为当前配置；修改正式预制体后用 Unity **Fish Social → Export Overlay HUD**。旧 Python YAML 工具遇到继承按钮将拒绝不完整写入。

## 发布和验收

资源和代码必须一起发布。旧 EXE 不会因为换了素材自动移除状态栏、弹窗和按钮回弹。沿用项目的 Windows Overlay 构建流程，分发整个 `OverlayResources`。

已完成：34 项 Python 静态检查、5 项 Unity 真实事件／预制体／临时完整导出测试、Unity 编译和 24 个 WPF C# 文件语法检查。macOS 未运行 Windows WPF，不等于 BAML 编译或原生视觉验收。

Windows 待验收：三种视口 960×560、800×400、600×300；100%/150% DPI 三态纸张接缝、底部按钮与展开聊天；中文输入、发送、右键、透明点击穿透；连续钓获／回鱼／脱钩无弹卡且动画与结算正常。
