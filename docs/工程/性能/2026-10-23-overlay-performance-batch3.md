# Overlay 性能优化第三批

日期：2026-10-23。关联：STEAM-DESKTOP-21。状态：已实现，Windows 原生运行和性能待验收。

## 实现范围

### 静态场景后台加载

- 鱼塘背景、静态装饰、座位图片的路径查找和 PNG 解码改为后台执行，图片冻结后回到 Dispatcher 发布。静态资源独立使用 2 个 worker、64 个任务容量及 48 MiB LRU；动画仍使用第二批的独立队列和 64 MiB 缓存。
- 同一路径和加载模式共享解码任务；取消单个等待者不打断其他使用者，最后一个等待者取消尚未执行的任务。队列满时异步等待重试，不阻塞 UI。
- 场景版本和取消令牌隔离快速换塘、座位移除、关窗后的旧结果。装饰按原 z 顺序整体发布；背景加载时显示当前塘的降级形状，不把旧塘底图留在新钓位下面。
- 原始背景尺寸仍用于缺少尺寸声明的旧布局。异步完成仅在实际尺寸变化时通知视口；只使用同一鱼塘的最新快照刷新角色和座位，正常完成不会重置用户平移。

### 点击命中预制 alpha

- 背景解码时在后台提取原始分辨率的单字节 alpha 平面，以 32 行临时缓冲逐块读取。图片进入画面前注册命中数据，鼠标命中路径不再执行 CopyPixels、格式转换或文件读取。
- 保持原像素映射、透明孔洞、边缘渐隐相乘和阈值 8，不进行降采样。2170×725 图片的常驻命中数组从 6,293,000 字节 BGRA 变成 1,573,250 字节 alpha，**这部分载荷减少 75%**，并非总进程内存下降 75%。
- alpha 通过弱键关联图片，图片淘汰且不再显示后不会被命中表永久保留。

### 九宫格与遮罩

- 九宫格 Bitmap／Brush 合并到 16 MiB LRU，按位图尺寸计费并保留少量条目开销；使用弱键分配源图 ID，避免对象哈希碰撞误用缓存。现有九宫格裁切和边框计算不变。
- 48 MiB 静态图、16 MiB 九宫格均约束缓存持有的估算载荷，不是进程内存上限。仍在 Image／Brush 中使用的图片，缓存淘汰后继续有效。
- 视口尺寸和 DPI 不变时复用边缘遮罩，不重复创建渐变。默认继续使用原来的双渐变。
- 增加 `FISH_SOCIAL_OVERLAY_COMBINED_MASK=1` 实验入口：按物理像素后台生成两个渐变乘积的单张预乘 alpha 遮罩，完成后替换双遮罩；构建期间沿用正确的双渐变。换尺寸、换 DPI 和销毁会隔离过期结果。**尚未据此宣称 GPU 合成开销下降，也未默认启用。**

### 诊断

- 原每秒／ACK 状态日志新增 `staticArtBytes`、`staticArtDecodes`、`staticArtPending`、`nineSliceBytes`、`nineSliceEntries`。
- 启动记录渲染 tier、DPI、透明窗口状态。`FISH_SOCIAL_OVERLAY_PROFILE=1` 才订阅 CompositionTarget.Rendering，窗口隐藏或最小化时停止订阅，关窗解除。
- 每 5 秒汇总实际遮罩模式、回调间隔 p95／max、Private Bytes、累计 CPU 毫秒。采样数组有界，无逐帧日志；回调间隔不是 GPU 帧耗时或呈现 FPS。订阅 Rendering 自身可能影响空闲合成，A/B 必须保持相同采样开关。

## 已执行验证

| 验证 | 结果 |
| --- | --- |
| 新增跨平台像素／命中／遮罩计算 | 6/6 通过 |
| 第二批调度／取消运行回归 | 10/10 通过 |
| 第一批缓存运行回归 | 8/8 通过 |
| 全量 Python 源码／资源检查 | 48/48 通过 |
| 正式 WPF 和 Windows 原生测试交叉编译 | 0 errors；5 条既有 netcoreapp3.1 提示 |
| Windows 原生回归 | 18 组已编译，未执行 |

新增原生测试覆盖真实 PNG alpha、共享加载和取消、缓存压力与活跃引用、快速换塘和销毁、装饰几何、九宫格淘汰，以及双遮罩／合并遮罩的渲染 alpha 对照。可移植测试覆盖全部 256 个 alpha 值、拉伸映射、边界、1 字节载荷，以及多尺寸／DPI 的遮罩公式和取消。

将新增异步实现涉及的整文件锁拆为未变动的几何／路径／遮罩公式锁与行为测试；素材和布局锁仍保留。第二批遗留的 3 处音效哈希已与当前已提交的 App 和鼠标处理代码核对，同步基线并新增单次音效路由检查；未修改音效生产代码。

本轮在 macOS 完成可移植测试和交叉编译；没有 Windows 帧率改善百分比，也未替换已安装游戏内的 EXE。机器可读结果见 [验证记录](2026-10-23-overlay-performance-batch3-validation.json)。

## 复跑

仓库根目录：

```sh
dotnet run --project desktop-overlay/tests/FishSocialOverlay.StaticArtTests.csproj
dotnet run --project desktop-overlay/tests/FishSocialOverlay.SchedulingTests.csproj
dotnet run --project desktop-overlay/tests/FishSocialOverlay.CacheTests.csproj
python3 -m unittest discover -s desktop-overlay/tests -p 'test_*.py'
dotnet build desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj -p:EnableWindowsTargeting=true
```

Windows 安装项目所需 SDK／运行时后，执行真实 WPF 测试：

```powershell
dotnet run --project desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj
```

## Windows 验收与 A/B

1. 重新构建并按现有发布流程部署测试 EXE。先比较第二批和第三批的默认双遮罩版本：固定机器、分辨率、DPI、鱼塘、1／5／20 个角色、素材冷／热状态，测首次进塘与连续 30 次换塘的 UI 长任务、输入延迟、CPU、Private Bytes 和缓存指标。不能用变小的 applyMs 单项代替端到端改善。
2. 快速换塘并关闭窗口，检查无旧背景／座位回写、无已销毁角色复活；稳定后静态队列应归零。热资源解码计数应稳定；超过预算后允许冷图重解码。
3. 在 100%、150% DPI 及跨屏切换时检查透明孔洞、岸边薄线、四边渐隐、座位／菜单交互与点击穿透。检查背景加载后的自身钓位居中和手动平移。
4. 再单独比较第三批内的双遮罩和合并遮罩，其他条件一致，避免把静态加载优化混入 GPU 对照。以下变量必须由实际启动 Overlay 的父进程继承；每次改变后重启该进程及 Overlay，沿用项目正常启动入口：

```powershell
$env:FISH_SOCIAL_OVERLAY_PROFILE = "1"
$env:FISH_SOCIAL_OVERLAY_COMBINED_MASK = "0"
# 通过正常入口启动，采集双遮罩基线，退出并保存日志。
$env:FISH_SOCIAL_OVERLAY_COMBINED_MASK = "1"
# 通过相同入口重新启动，等待日志 mask=combined 后采集。
```

5. 两次均保持安全窗口模式关闭、相同采样时长和 DPI。日志位于 `%TEMP%/FishSocialOverlay-latency.log`。CPU 使用相邻累计值的差值；GPU／呈现帧时间使用 Windows 性能工具采样，不把 callbackGapP95Ms 当 FPS。另做采样关闭的对照，评估诊断订阅扰动。
6. 合并遮罩只有通过原生像素对照、点击边缘检查且测得收益后再决定默认开启。默认双遮罩保留原视觉表现。

## 后续边界

布局 JSON 解析仍同步；本批未改 IPC 调度合并、图集或渲染后端。损坏图片有预算内失败缓存，运行中替换资源需重启。进程实际内存包含活跃图片、WPF／GPU 资源和临时解码，不等于缓存计费总和。Windows 18 组原生测试、真实联机与性能／视觉验收仍待完成。
