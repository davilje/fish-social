# Overlay 回归

从仓库根目录运行：

- 源／资源检查：`python3 -m unittest discover -s desktop-overlay/tests -p 'test_*.py'`
- 跨平台缓存行为：`dotnet run --project desktop-overlay/tests/FishSocialOverlay.CacheTests.csproj`（.NET 8，8 项）
- Windows WPF：`dotnet run --project desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj`（沿用生产 netcoreapp3.1，11 组）。实际加载素材、构造角色、泵 Dispatcher、检查 Dispose、弱引用回收与 30 次场景进出。
- 非 Windows 仅编译：`dotnet build desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj -p:EnableWindowsTargeting=true`。编译通过不代表原生测试运行通过。

Windows 项目引用正式 Overlay 程序并复制 OverlayResources；缓存测试直接编译正式 OverlayResourceCache.cs。三个测试项目的输出／中间目录独立，正式应用排除 tests 下源码。所有验证范围、限制与人工验收见 [第一批交付](../../docs/engineering/2026-10-23-overlay-performance-batch1.md)。

## 第二批

- 跨平台时钟／队列／取消行为：`dotnet run --project desktop-overlay/tests/FishSocialOverlay.SchedulingTests.csproj`（.NET 8，10 项，编译正式调度／队列／等待代码）。
- Windows 11 组包含第一批 6 组，新增增量布局与实际中心坐标、聊天控件复用／中间消息编辑／收起延迟更新、真实异步 PNG 共用与取消／迟到结果隔离、隐藏后终态恢复、视口裁剪边界。程序显式安装 DispatcherSynchronizationContext，并等待异步预热完成再验证解码计数。
- 第二批验证记录与 Windows 性能验收：[第二批交付](../../docs/engineering/2026-10-23-overlay-performance-batch2.md)。
