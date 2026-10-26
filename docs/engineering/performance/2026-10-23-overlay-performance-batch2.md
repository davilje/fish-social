# Overlay 性能优化第二批

日期：2026-10-23。关联：STEAM-DESKTOP-21。状态：代码已实现，Windows 原生运行和性能待验收。

## 实现

### 分层动画后台加载

- Manifest 读取、全部图层文件完整性检查、PNG 原始尺寸校验和解码在后台执行。两个 worker，最多接纳 256 个任务，超额请求不进入队列，活动帧稍后重试。
- worker 只产生不可变元数据和 Freeze 后的 BitmapImage。Packs、TextureScales、Missing、待处理请求表与 64 MiB LRU 的发布和修改均由 Dispatcher 串行执行；发布入口检查线程归属。
- 同一路径和目标尺寸的并发请求共用一个解码任务。取消一个角色的等待不会打断其他共享用户；最后一个等待者离开时取消尚未执行的任务。已经执行的最多两个任务允许完成，过期结果不会写回角色。
- 用状态／画面版本隔离换猫、换塘、尺寸变化、切帧与销毁后的异步结果。九层完整就绪才一次性提交，等待期间保留上一个完整画面。预热按最终尺寸顺序提交，每个预热者只有一个未完成请求。
- 已验证的 Manifest 不再每次开片段检查磁盘。资源损坏／缺失仍在首次校验或冷解码被拒绝；运行中热替换素材需要重新启动 Overlay。同步 BitmapFor 留作原生资源测试参考，正式 Actor 不调用。

### 布局和聊天增量更新

- 删除角色 Place 中的 UpdateLayout／TranslatePoint，直接使用 RelayoutCluster 已计算的身体中心；昵称和阶段不变时不重新测量，Chrome 几何相同直接复用。
- 角色局部 Chrome 按钓位缓存，换塘清空；静态钓位布局与座位图仅在塘／几何变化时重建，常规快照只更新占用状态。修正钓位数量相同、ID 变化时旧钓位可能残留的分支。
- 聊天使用完整可见内容签名，支持中间消息修改；追加和滚动消息窗口保留已有 TextBlock，更新文本只改对应行。折叠状态只更新摘要，展开再更新列表。
- 滚动调整交给正常布局之后的 Dispatcher Loaded 操作，同批请求合并；移除旧行时补偿阅读位置。正式 HUD 与 XAML 降级入口均接入折叠状态。

### 共享时钟与可见区域

- 每个角色的帧计时器和刷新计时器改成共享调度订阅，底层只有一个 16ms Dispatcher 驱动，时间来自 Stopwatch。
- 仍按 Manifest 的每帧实际时长播放；16ms 只是调度检查间隔，不将原动画强行插帧成 60fps。阻塞后按到期时间推进，循环片段可跳过完整周期，只提交追赶后的最新画面。
- 原 ApplyVisualState 状态机主体原样保留为 ApplyLoadedVisualState，并添加源码锁；AdvanceLayered、收杆／脱钩／展示／复位的终态与后继逻辑保持原样。单次调度最多推进每订阅 128 个边界，未完成的时间债务保留至下次，避免丢弃终态。
- 窗口隐藏／最小化停止共享驱动；离屏角色暂停动画订阅、圆环／悬浮刷新和新画面请求。视口包含 256 DIP 保守外扩，避免鱼竿、姓名和气泡提前停止；平移、缩放视口、重新显示时恢复并按已流逝时间追赶。IPC 状态继续接收，咬钩截止时间仍以业务时间计算。
- tooltip、气泡淡出等一次性交互保留原生命周期；场景平移及 WPF 内置动效不是每角色时钟，本批未改写它们。

### 指标

沿用每秒／ACK 的 `overlay_state_applied` 日志，新增 `pendingDecodes`（排队＋执行中任务数）、`rejectedDecodes`（队列满后待重试次数）、`actorLayouts`（累计角色布局次数）。原 `applyMs`、`actors`、`activeClips`、`cachePixelBytes`、`layerDecodes` 继续提供。日志没有新增逐帧写入。

## 已执行验证

- 10/10 调度与队列行为测试：非均匀帧时长、延迟追赶、终态顺序／只触发一次、回调移除、隐藏恢复、循环跳帧、有界追赶、并发／容量上限、失败回收、排队取消、共享等待取消。上述为 10 个测试组，其中队列组同时覆盖多个断言。
- 8/8 第一批缓存行为回归通过。
- 新增 4 项第二批接入检查通过。全量 Python 共 43 个测试，41 个通过；另 2 个检查包含 3 处音效并行修改导致的源码哈希不匹配：`App.xaml.cs`、`PondScene_OnMouseLeftButtonDown`、`PondScene_PreviewMouseRightButtonUp`。没有为消除这些不属于本批的差异更新它们的基线。
- 正式 WPF 与 Windows 原生回归项目交叉编译通过，0 errors；5 条原有 netcoreapp3.1 目标框架提示。
- 11 组 Windows 原生回归已编译，尚未运行。包括真实异步图像、取消共享请求、保留完整旧帧、过期结果不能复活角色、实际布局中心与坐标、聊天保留控件、隐藏后脱钩／复位链，以及第一批全部回归。
- `git diff --check -- desktop-overlay` 通过。素材、XAML 布局和 Unity 表现层不在本批改动范围；同工作区的首页、音效与其他 Unity 页面修改属于并行任务。

本轮仅在 macOS 运行可移植测试与 WPF 交叉编译，没有 Windows 帧率、CPU、内存或交互延迟的改善百分比结论，也未替换已安装游戏中的 Windows EXE。

## 复跑和验收

仓库根目录：

```sh
dotnet run --project desktop-overlay/tests/FishSocialOverlay.SchedulingTests.csproj
dotnet run --project desktop-overlay/tests/FishSocialOverlay.CacheTests.csproj
python3 -m unittest discover -s desktop-overlay/tests -p 'test_*.py'
dotnet build desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj -p:EnableWindowsTargeting=true
```

Windows 原生运行：

```powershell
dotnet run --project desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj
```

重新发布 Overlay EXE 后，在同一机器分别对比第一批版与第二批版：

1. 固定分辨率、塘和 1／5／20 个角色；冷启动与热循环各录制一次，比较输入到画面的延迟、UI 线程长任务、CPU 与 Private Bytes。调度改动会改变 applyMs 包含的工作，不能只用这一项代替端到端改善。
2. 冷资源连续换猫／换塘，检查画面无混层、旧猫不覆盖新猫、离场角色不复活；pendingDecodes 应有界并在稳定后回落，持续 rejectedDecodes 增长代表仍有过载。
3. 固定昵称和阶段发送快照，actorLayouts 应保持不变；改昵称／切阶段才上升。聊天新增消息应保留旧行，折叠期间列表不构建；编辑中间消息、滚动查看历史后再接收消息均正确。
4. 在收杆、脱钩和展示渔获过程中隐藏／最小化窗口后恢复；终态和复位不重复、不卡住。把角色平移出视口再返回，仍显示最新状态，圆环时间准确。
5. 30 次换塘、混合猫种和窗口开关后，复查缓存预算、旧角色回收与队列归零。实际 CPU／帧时间结果和视频补入验收记录后再标记已验收。

本批未处理塘背景／静态装饰首次加载、GPU 透明窗口和双遮罩合成、图集或渲染后端迁移；这些需要下一批采样定位。
