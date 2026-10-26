# Overlay 读条、水面接触与聊天展开

2026-10-24；STEAM-DESKTOP-22A–C 已实现，Windows 原生运行与视觉待验收。

## 实现

- 读条：`OverlayHookProgressStyle` 用矢量奶油纸盘、浅棕轨道、暖橙渐变圆弧替代旧 PNG 进度图。中心仍用当前棕色咬钩／打窝图标，缩至圆环宽度的 52%。保留真实截止时间、比例与结束隐藏。移除原每次更新创建 PNG 径向遮罩的分支。
- 水面：原动画统一依据猫咪脚底缩放，原浮漂中心 y=463.5 与脚底 y=450 很接近，落到座位高度而非水面。原始 PNG 不改动；导出共享图层 alpha 边界到 `water-contact.json`，以座位前沿下方 6px 作为水面接触线。鱼线围绕竿尖端拉长，浮漂落到接触线，咬钩鱼进入接触线以下；抛竿逐步接入，收杆逐步退回原动作。身体、头、手和竿保持原图层。座位几何变化也会触发更新。未来新场景仍需实际校对岸线。
- 聊天：折叠栏整体及开关在 PreviewMouseLeftButtonDown 路由展开并消费当前按下，避免只依赖可能被子控件处理的冒泡 MouseUp。键盘 Button.Click 保留；展开后的输入只在输入区域获得焦点，避免点发送／收起被输入框抢焦点。展开／收起使原生命中缓存立即失效。

## 验证

- 跨平台水面测试 **6/6**：64／96／128／192 尺寸的浮漂接触、竿尖固定、线端到水面、咬钩鱼入水；抛竿／收杆连续权重、缺失几何保护。
- 源／资源测试 **51/51**：包含接触元数据双目录一致及所有猫种／动作帧覆盖。旧源锁定清单中仅更新用户明确要求变化的读条更新和聊天按下回调两项。
- 正式 WPF 和 Windows 原生测试项目编译通过；原生组数 **20**，聊天组从直接调用 Click 扩展到真实 InputHitTest + PreviewMouseLeftButtonDown 路由；新增矢量进度／结束隐藏检查。
- Windows x64 自包含包：`dist/overlay-ui-fixes-2026-10-24.zip`，解压得到 `FishSocialOverlay/`。适用于与当前项目同版本 Unity 主程序配套部署；不会自动替换正在运行的用户程序。
- 本机 macOS，**未执行** Windows 20 组和实机录屏；不据编译通过宣称原生交互／视觉验收通过。

## 复跑与验收

```sh
python3 -m unittest discover -s desktop-overlay/tests -p 'test_*.py'
dotnet run --project desktop-overlay/tests/FishSocialOverlay.WaterContactTests.csproj
dotnet build desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj -p:EnableWindowsTargeting=true
```

Windows 上运行 `dotnet run --project desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj`。实际进塘检查：

1. 点击折叠聊天栏空白处和图标，分别验证展开／收起；三种窗口尺寸与 100%／150% DPI；中文输入、Enter、发送、回显及重进塘。
2. 原版橘猫、三花和大肥鱼：抛竿结束浮漂在水面、竿尖不断线、咬钩鱼不悬空，收杆逐步回归；切换不同鱼塘检查岸线。
3. 咬钩与打窝进度用新配色，时间耗尽、脱钩、收杆、离席后没有残留。

动画图层重新导出后，运行 `scripts/export-fishing-water-contact.py`（需要 Pillow），同步接触边界再构建；测试检查源与发布资源一致。补帧、离席顺序、打窝循环、背景、特效、音效与世界地图仅记录于 STEAM-DESKTOP-22D–J，本轮未实现。
