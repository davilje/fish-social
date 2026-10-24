# STEAM-DESKTOP-22D / 22F：轨迹补帧与持续打窝

设计与实现日期：2026-10-24。代码及资源已接入正式 Overlay，Windows 原生运行和视觉待验收。

## 可查看的结果

[动作对照页](../art/fishing-motion/index.html) 直接读取正式 Overlay 角色与道具 PNG，并使用同源采样数据。支持十角色切换、动作切换、原方案与新方案同屏、速度、逐帧、可调打窝时长和主动结束打窝。

## 22D：保持原时长的分层轨迹采样

| 动作 | 原关键姿势 | 新运动采样 | 单段时间 |
| --- | ---: | ---: | ---: |
| 抛竿 `cast` | 4 | 13 | 600 ms |
| 收杆 `reel` | 6 | 16 | 600 ms |
| 空军收杆 `reel_empty` | 6 | 16 | 600 ms |
| 打窝 `groundbait` | 8 | 18 | 980 ms（完整单次原时间表） |

补充的是道具轨迹中间采样，不是新绘制的猫咪中间姿态。身体、头、远侧手和近侧手仍取同一个原始关键姿势，保持尺寸和拆分边界；不混合两幅角色图、不按包围盒缩放、不改变已修复的头部遮挡顺序。鱼竿围绕当前握点转动，鱼、浮漂和窝料沿对应轨迹移动。新的中间帧鱼线使用冻结的细线 DrawingImage，连接变换后的竿尖和末端；不把短鱼线 PNG 放大为粗黑条。原关键帧仍使用原 PNG。

`scripts/export-fishing-motion.py` 从已保存的 `docs/art/fishing-motion/source-rig.json` 和当前共享 PNG 边界生成 `motion-samples.json`。源 rig 可用 `--studio <animation.json>` 从原工作台重新导入。输出同时写到 `desktop-overlay/OverlayResources/fishing/` 和 Unity 配套的 `fish-social-unity/FishSocialOverlay/OverlayResources/fishing/`，并生成 Web 对照数据。

`FishingClipPlayback` 保存扩展采样到源关键帧的映射；`LayerPath`、初次文件校验和纹理尺寸登记使用源帧索引。当前四种动作的总时长均不变。`SourcePosition` 用于水面权重，避免增加采样数量后改变浮漂接触和收杆退出时机。

新增 PNG 数为 **0**。轨迹 JSON 约 **104 KiB**；缓存引用按实际资源路径去重，所有中间采样复用既有 PNG 与异步解码结果。细线绘图按采样缓存冻结对象。原 64 MiB 解码缓存预算、两 worker 有界队列、取消与迟到结果隔离保持有效。此处是资源预算，不是 Windows 进程内存或帧率实测。

## 22F：真实阶段决定打窝循环

原来 `groundbait` 是一次播放，980 ms 后转 `ready`，服务器仍在打窝时角色已停止动作。现在：

1. 首次进入 `groundbaiting`，记录单调时钟起点，播放原第 0 帧起手 100 ms。
2. 循环源帧 `[1,7)`：捧料、送手、抛撒、落水、收手，每轮 720 ms。重复快照不重启；不能根据增益剩余时间、层数或本地进度条到零提前停止。
3. 真实阶段退出，播放源第 6 帧收手 120 ms、第 7 帧准备 160 ms，共 280 ms 收尾，再进入最新等待状态。
4. 新的打窝周期重启起手；断线、拖动、离席、销毁等取消不残留循环。隐藏时不解码／渲染，恢复根据单调时间采样；换角色仍保持当前真实周期，不重新播放一遍起手。

循环区间与收尾区间由源帧边界映射到扩展采样，未硬编码新增帧号。保留并行完成的 22E 入座／离席脚底和显示后计时规则。

## 验证与复跑

- 运行通过：Motion 12/12、Groundbait 10/10、水面 6/6、缓存 8/8、调度与队列 10/10，共 46 项跨平台行为测试；源／资源检查 51/51。
- 正式 WPF 与包含 28 组回归的原生测试项目编译通过。新增 D 四组、F 三组，并保留并行 E 座位迁移组。28 组在当前 macOS 上未运行。
- `dist/overlay-motion-22df-2026-10-24.zip` 为 Windows x64 自包含测试包，包含当前 A–F 代码、正式 Overlay、原生测试入口和共享资源。解压后运行 `FishSocialOverlay/Run-Native-Tests.cmd`，日志输出到同目录 `native-tests.log`。`BUILD-INFO.json` 保存源码／二进制哈希及验证边界。不会自动替换正在运行的游戏。

浏览器检查已覆盖十角色 × 四动作 × 四个时间点，共 160 次采样加载，及打窝结束和逐帧控件；无页面异常。结果与截图保存在 `docs/art/fishing-motion/`。自动检查不替代原画连续性、手竿配合和实机视觉验收。

```sh
python scripts/export-fishing-motion.py
python scripts/export-fishing-water-contact.py
dotnet run --project desktop-overlay/tests/FishSocialOverlay.MotionTests.csproj
dotnet run --project desktop-overlay/tests/FishSocialOverlay.GroundbaitTests.csproj
dotnet run --project desktop-overlay/tests/FishSocialOverlay.WaterContactTests.csproj
python3 -m unittest discover -s desktop-overlay/tests -p 'test_*.py'
dotnet build desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj -p:EnableWindowsTargeting=true -p:UseAppHost=false
```

Python 导出需要 Pillow；浏览器检查使用 `node scripts/verify-fishing-motion-preview.cjs`，可用 `PLAYWRIGHT_MODULE` 和 `CHROME_PATH` 指定已安装的 Playwright 与 Chrome。

Windows 上运行 `dotnet run --project desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj`，验证真实 WPF PNG 共用、细线渲染、换角色、隐藏恢复、打窝阶段退出与销毁。当前 macOS 环境未执行 Windows 原生测试，不将交叉编译算作该项通过。

## 实机验收

- 橘猫、三花、大肥鱼分别抛竿／收杆／空军：0.6 秒保持一致，竿绕握点、鱼线不断开，没有角色缩放或头手接缝变化。
- 短打窝、5 秒、30 秒打窝：不在 980 ms 停住；重复快照、截止时间修正不会重播起手或提前收尾。
- 打窝中打开菜单、隐藏再恢复、换角色、离席、断线、重新打窝：当前状态唯一，不出现旧周期恢复或孤立时钟任务。
- 96／128／192 角色尺寸与 100%／150% DPI：中间鱼线保持细线，浮漂和鱼继续接触水面；记录首轮冷解码与后续热缓存帧率／峰值内存。

未修改 Unity 的钓鱼业务状态机、服务器协议、原关键姿势 PNG 和原动作 manifest。G–J 未在本轮实现。
