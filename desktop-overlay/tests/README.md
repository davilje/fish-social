# Overlay 回归

从仓库根目录运行：

- 源／资源检查：`python3 -m unittest discover -s desktop-overlay/tests -p 'test_*.py'`
- 跨平台缓存行为：`dotnet run --project desktop-overlay/tests/FishSocialOverlay.CacheTests.csproj`（.NET 8，8 项）
- Windows WPF：`dotnet run --project desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj`（沿用生产 netcoreapp3.1，6 组）。实际加载素材、构造角色、泵 Dispatcher、检查 Dispose、弱引用回收与 30 次场景进出。
- 非 Windows 仅编译：`dotnet build desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj -p:EnableWindowsTargeting=true`。编译通过不代表原生测试运行通过。

Windows 项目引用正式 Overlay 程序并复制 OverlayResources；缓存测试直接编译正式 OverlayResourceCache.cs。两个测试项目的输出／中间目录独立，正式应用排除 tests 下源码。所有验证范围、限制与人工验收见 [第一批交付](../../docs/engineering/2026-10-23-overlay-performance-batch1.md)。
