# STEAM-DESKTOP-22D / 22F：轨迹补帧与持续打窝

设计与实现日期：2026-10-24。代码及资源已接入正式 Overlay，Windows 原生运行和视觉待验收。

## 可查看的结果

[动作对照页](../../美术/钓鱼动作/index.html) 直接读取正式 Overlay 角色与道具 PNG，并使用同源采样数据。支持十角色切换、动作切换、原方案与新方案同屏、速度、逐帧、可调打窝时长和主动结束打窝。

## 22D：保持原时长的分层轨迹采样

| 动作 | 原关键姿势 | 新运动采样 | 单段时间 |
| --- | ---: | ---: | ---: |
| 入座 `sit` | 4 | 8 | 480 ms |
| 挂饵 `bait` | 4 | 20 | 1000 ms |
| 抛竿 `cast` | 4 | 13 | 600 ms |
| 等待 `fishing` | 1 | 13 | 500 ms（循环） |
| 咬钩后仰 `bite` | 4 | 12 | 560 ms |
| 持续发力 `effort` | 6 | 24 | 880 ms（循环） |
| 咬钩接发力 `hooked` | 10 | 39 | 1440 ms（前 560 ms 起手，后 880 ms 循环） |
| 收杆 `reel` | 6 | 16 | 600 ms |
| 空军收杆 `reel_empty` | 6 | 16 | 600 ms |
| 展示收获 `catch` | 7 | 21 | 1200 ms |
| 收获后复位 `reset_catch` | 4 | 8 | 480 ms |
| 空军后复位 `reset_empty` | 4 | 10 | 480 ms |
| 打窝 `groundbait` | 8 | 18 | 980 ms（完整单次原时间表） |
| 离席 `leave` | 4 | 10 | 580 ms |

补充的是分层轨迹中间采样，不是新绘制的猫咪中间姿态。身体、头、远侧手和近侧手仍取同一个原始关键姿势，保持尺寸和拆分边界；不混合两幅角色图、不按包围盒缩放、不改变已修复的头部遮挡顺序。入座、挂饵、展示收获、复位、离席增加绕共同脚底 `[264,450]` 的整体微转（每段最大 6°），各身体图层及手持鱼一起运动，局部变换保持不动；跨遮挡顺序的切换仍使用原关键姿势。其他道具轨迹围绕当前握点转动，不能独立把鱼移离抓鱼的手。新的中间帧鱼线使用冻结的细线 DrawingImage，连接变换后的竿尖和末端。原关键帧仍使用原 PNG。

等待浮标在原位置左右轻摆 1.5 个原画坐标单位，通过采样配置将原 hold 变为循环，角色保持静止。发力／咬钩的最后一段补到各自 loopStart，循环不会误回到咬钩起手。静态 `idle`、`ready` 保留 hold，不虚增动作。身体姿势、弯曲形变仍使用原画；轨迹采样不能替代新增绘制姿势。

`scripts/export-fishing-motion.py` 从已保存的 `docs/美术/钓鱼动作/source-rig.json` 和当前共享 PNG 边界生成 `motion-samples.json`。源 rig 可用 `--studio <animation.json>` 从原工作台重新导入。输出同时写到 `desktop-overlay/OverlayResources/fishing/` 和 Unity 配套的 `fish-social-unity/FishSocialOverlay/OverlayResources/fishing/`，并生成 Web 对照数据。

`FishingClipPlayback` 保存扩展采样到源关键帧的映射；`LayerPath`、初次文件校验和纹理尺寸登记使用源帧索引。所有动作每个源帧时长不变。`SourcePosition` 用于水面权重，避免增加采样数量后改变浮漂接触和收杆退出时机。

新增 PNG 数为 **0**。缓存引用按实际资源路径去重，所有中间采样复用既有 PNG 与异步解码结果。细线绘图按采样缓存冻结对象。原 64 MiB 解码缓存预算、两 worker 有界队列、取消与迟到结果隔离保持有效。此处是资源预算，不是 Windows 进程内存或帧率实测。

## 下落和水下深度修正

`OverlayFishingWaterContact.FishDepth` 从原来的 14 改为 64 个原画坐标单位：咬钩／发力时鱼顶部至少位于水面下该深度，收杆沿原权重平滑恢复到展示位置。`GroundbaitOffset` 在撒出后的源位置 `[3,6)` 按平方进度增加「座位水面差 + 90 原画单位」的下落位移，握料／起手／收手阶段为零。窝料仍沿原来的横向路径运动，真实阶段和循环区间不变。

预览新增水面线与水下画布范围，读取正式图层边界与同源参数；矩阵顺序为局部轨迹、脚底整体微转、水面修正。数值以 640×512 原画坐标定义，Overlay 按角色实际显示比例转换。扩展预览画布不修改 PNG 尺寸，也不缩放人物。用户此前认可的是首版四动作预览，本轮扩展和加深效果另行查看。

## 22F：真实阶段决定打窝循环

原来 `groundbait` 是一次播放，980 ms 后转 `ready`，服务器仍在打窝时角色已停止动作。现在：

1. 首次进入 `groundbaiting`，记录单调时钟起点，播放原第 0 帧起手 100 ms。
2. 循环源帧 `[1,7)`：捧料、送手、抛撒、落水、收手，每轮 720 ms。重复快照不重启；不能根据增益剩余时间、层数或本地进度条到零提前停止。
3. 真实阶段退出，播放源第 6 帧收手 120 ms、第 7 帧准备 160 ms，共 280 ms 收尾，再进入最新等待状态。
4. 新的打窝周期重启起手；断线、拖动、离席、销毁等取消不残留循环。隐藏时不解码／渲染，恢复根据单调时间采样；换角色仍保持当前真实周期，不重新播放一遍起手。

循环区间与收尾区间由源帧边界映射到扩展采样，未硬编码新增帧号。保留并行完成的 22E 入座／离席脚底和显示后计时规则。

## 验证与复跑

- 本轮运行通过：Motion 18/18、Groundbait 10/10、水面 9/9、缓存 8/8、调度与队列 10/10、座位迁移 8/8，共 63 项跨平台行为测试；源／资源检查 51/51。回归曾发现发力尾段角度参数相同而没有实际位移，已改为根据下一竿尖位置计算绕握点转角并复测通过。
- 正式 WPF 与包含 28 组回归的原生测试项目编译通过。新增 D 四组、F 三组，并保留并行 E 座位迁移组。28 组在当前 macOS 上未运行。
- `dist/overlay-motion-all-actions-2026-10-24.zip` 为本轮 Windows x64 自包含测试包，包含更新后的正式 Overlay、28 组原生测试入口和共享资源。解压后运行 `FishSocialOverlay/Run-Native-Tests.cmd`，日志输出到同目录 `native-tests.log`。`BUILD-INFO.json` 保存源码／二进制哈希及验证边界。旧 `overlay-motion-22df` 包为此前四动作版本，不含本轮扩展；本轮不会自动替换正在运行的游戏。

浏览器检查已覆盖十角色 × 十四动作 × 四个时间点，共 560 次采样加载，以及水下深度、渐进撒料、打窝结束和逐帧控件；无页面异常。结果与六张截图保存在 `docs/美术/钓鱼动作/`。自动检查不替代原画连续性、手竿配合和实机视觉验收。

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
- 入座／离席／挂饵／展示／复位的整体微转脚底不移动、角色无缩放、手持鱼不脱手；持续发力循环末尾不停顿，等待浮标持续轻摆；打窝撒料落点与鱼水下深度在不同座位高度一致。

未修改 Unity 的钓鱼业务状态机、服务器协议、原关键姿势 PNG 和原动作 manifest。G–J 未在本轮实现。
