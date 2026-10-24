# Overlay 回归

从仓库根目录运行：

- 源／资源检查：`python3 -m unittest discover -s desktop-overlay/tests -p 'test_*.py'`
- 跨平台缓存行为：`dotnet run --project desktop-overlay/tests/FishSocialOverlay.CacheTests.csproj`（.NET 8，8 项）
- Windows WPF：`dotnet run --project desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj`（沿用生产 netcoreapp3.1，当前 28 组）。实际加载素材、构造角色、泵 Dispatcher、检查 Dispose、弱引用回收与 30 次场景进出。
- 非 Windows 仅编译：`dotnet build desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj -p:EnableWindowsTargeting=true`。编译通过不代表原生测试运行通过。

Windows 项目引用正式 Overlay 程序并复制 OverlayResources；缓存测试直接编译正式 OverlayResourceCache.cs。四个测试项目的输出／中间目录独立，正式应用排除 tests 下源码。所有验证范围、限制与人工验收见各批交付记录。

## 第二批

- 跨平台时钟／队列／取消行为：`dotnet run --project desktop-overlay/tests/FishSocialOverlay.SchedulingTests.csproj`（.NET 8，10 项，编译正式调度／队列／等待代码）。
- Windows 11 组包含第一批 6 组，新增增量布局与实际中心坐标、聊天控件复用／中间消息编辑／收起延迟更新、真实异步 PNG 共用与取消／迟到结果隔离、隐藏后终态恢复、视口裁剪边界。程序显式安装 DispatcherSynchronizationContext，并等待异步预热完成再验证解码计数。
- 第二批验证记录与 Windows 性能验收：[第二批交付](../../docs/engineering/2026-10-23-overlay-performance-batch2.md)。

## 第三批

- 跨平台 alpha／遮罩计算：`dotnet run --project desktop-overlay/tests/FishSocialOverlay.StaticArtTests.csproj`（.NET 8，6 项，直接编译正式 AlphaMap／MaskRaster）。
- Windows 新增 7 组，共 18 组：真实 PNG alpha、共享静态加载与取消、缓存压力／活跃引用、快速换塘与销毁、异步装饰几何、九宫格预算、双遮罩与合并遮罩渲染像素对照。
- 当前 macOS 验证：24 项可移植运行测试、48 项 Python 检查通过；WPF 编译通过，18 组原生测试未运行。
- 指标、开关及 Windows 独立 A/B 步骤：[第三批交付](../../docs/engineering/2026-10-23-overlay-performance-batch3.md)。

## 后续交互回归

新增第 19 组验证三种窗口尺寸下聊天开关、装饰层穿透、输入命中与菜单删除；场景组补默认塘不预加载和加载完成前不可见。Unity 34 项专项及 Windows 实机步骤见 [进出鱼塘与资料刷新修复](../../docs/engineering/2026-10-23-overlay-return-regressions.md)。

## 2026-10-24 水面接触与交互

- 新增可移植水面接触测试：`dotnet run --project desktop-overlay/tests/FishSocialOverlay.WaterContactTests.csproj`，6 组，使用正式求解器与真实导出边界。
- Python 源／资源检查现为 51 项。Windows 原生现为 20 组，聊天覆盖真正的输入命中与 PreviewMouseLeftButtonDown 路由，新增手账矢量读条与截止隐藏。
- 本轮运行边界与 Windows 验收：[交付说明](../../docs/engineering/2026-10-24-overlay-pond-presentation.md)。

## STEAM-DESKTOP-22E 入座与离席

- `dotnet run --project desktop-overlay/tests/FishSocialOverlay.SeatMotionTests.csproj`：8 组可移植迁移测试。
- WPF 新增座位脚底、到位后坐下、站起尾帧、重复快照、快速重入、收杆后离席和隐藏恢复专项。Mac 仅能交叉编译，Windows 必须另行运行。
- [22E 实现与验收](../../docs/engineering/2026-10-24-overlay-seat-transitions.md)。

## STEAM-DESKTOP-22D / 22F 补帧与打窝循环

- `dotnet run --project desktop-overlay/tests/FishSocialOverlay.MotionTests.csproj`：18 组，直接编译正式运动清单、源帧映射与水面求解代码，加载十角色十四动作，覆盖循环尾段、脚底固定、手持鱼、畸形矩阵回退。
- `dotnet run --project desktop-overlay/tests/FishSocialOverlay.WaterContactTests.csproj`：现为 9 组，增加 64 原画单位的鱼深度、渐进撒料下落及手持／收尾保护。
- `dotnet run --project desktop-overlay/tests/FishSocialOverlay.GroundbaitTests.csproj`：10 组，真实阶段决定循环、720ms 循环、280ms 收手收尾、短周期、重复和隐藏追赶。
- WPF 新增四组运动采样／源 PNG 缓存／细线绘制／窗口尺寸测试，以及三组打窝阶段、中断和换皮测试；连同 22E 共 28 组，仅在 Windows 实际运行后才能标记原生通过。
- 原生运动组已扩展十四动作、脚底固定整体微转、手持鱼、等待／发力循环及窗口尺寸下撒料偏移。当前跨平台行为合计 63 项通过，Web 560 次采样通过；原生组仅编译，未在 macOS 运行。
- [22D / 22F 实现与验收](../../docs/engineering/2026-10-24-overlay-motion-22df.md)。
