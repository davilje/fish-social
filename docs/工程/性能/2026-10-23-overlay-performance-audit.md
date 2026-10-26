# Overlay 卡顿与优化审计

日期：2026-10-23。源码基线：`1a3c8769`。本轮为分析，没有改动 Overlay、Unity 或 Web 运行逻辑。

当前环境为 macOS，不能执行 Windows WPF。本报告区分“源码可确认的缺陷”“可确认的工作量”和“需要 Windows 采样确认的性能占比”。资源大小是本地文件统计，不是运行时内存或耗时实测。

## 结论

当前 Overlay 是独立 WPF 进程，Unity 通过 Named Pipe 提供状态。因此卡顿不能直接归因于 uGUI 或主窗口的手账动效。优先修复角色生命周期、缓存误失效和 UI 线程解码；再减少全量布局、统一动画时钟。现阶段没有证据要求重写整个渲染框架。

最明确的缺陷：角色从视觉树移除后，其 100ms DispatcherTimer 没有停止；动画缓存引用按猫种和片段计数，清理却只按片段删除其他猫种和共享图层；64px 预热与 128px 正式布局不匹配，会触发全局清缓存。

## 1. P0：移除角色没有关闭定时器

证据：
- `desktop-overlay/OverlayPetActor.cs:258` 创建 tooltip、frame、refresh 定时器；refresh 每 100ms 启动。
- 全文件没有 `_refreshTimer.Stop()`、Dispose 或 Unloaded 回收；`ReleaseFishingLayers`（542）只释放贴图引用，不关定时器。
- `desktop-overlay/PondScenePresenter.cs:578` 移除远端角色，以及 590 的 `ClearOthers`，调用 ReleaseFishingLayers、移除视觉节点后就丢弃字典引用。

DispatcherTimer 的事件委托会保留对象。因此角色离开、反复换塘后，旧 actor 有被持续保留和调度的路径。这是可确认的生命周期缺陷；Windows 内存快照仍需验证实际留存数量和 CPU 占比。

若遗留 100 个 actor，仅 refresh 定时器就有名义上约 1000 次/秒调度请求（UI 忙时实际执行频率更低）；不等于每次都有昂贵绘制。frame 定时器也没有在移除时统一停止，Release 后在空帧分支可能持续空转。

优化：提供幂等的 Dispose/Detach，停止全部计时器、解除事件、关闭悬停/气泡、释放图层；SyncOthers 删除、ClearOthers、窗口关闭均统一调用。优先完成这一点，再考虑共享时钟。隐藏窗口应暂停表现刷新，恢复时按当前状态重同步，不暂停真实钓鱼结算。

## 2. P0：缓存清理粒度错误，循环反复冷解码

证据：`desktop-overlay/OverlayFishingLayers.cs:95` 的 Release 按 `petId + clip` 减少引用；计数归零调用 `DropClip(clip)`（232）。DropClip 通过目录中的动作名匹配，移除所有猫种该动作的 Bitmaps 与 Missing，没有检查其他猫种或共享道具的引用。

触发例：橘猫与三花都在 hooked，橘猫转入 reel，橘猫 hooked 的使用数归零；DropClip("hooked") 会清掉三花 hooked 和共用道具缓存。三花已有 Image.Source 不会立刻消失，但下一帧查询会重新读文件解码。单猫每次完整退出动作也立即清缓存，下一轮钓鱼再次冷加载。

优化：按实际资源 key 管理引用，公共五层独立共享引用；零引用资源进入有字节预算的 LRU，而不是立即全清。仅淘汰不在使用且超过预算的资源。不同猫种并行动作、反复循环必须覆盖回归。

## 3. P0/P1：预热尺寸不匹配，UI 线程文件加载和解码

证据：
- `OverlayPetActor.cs:489` 的 WarmupActionClips 按 BodySize=64，为 11 个动作首帧各加载 9 层，最多 99 个资源请求。
- `PondScenePresenter.cs:503` 先预热和 Apply，再 ApplyChrome；当前布局 actor-pet 是 128×128。
- `OverlayPetActor.cs:1296` LayoutFishingLayers 在自身尺寸变化时调用 `OverlayFishingLayers.SetDecodeSize`（111）；该方法清空整个静态 Bitmaps/Missing。
- 新 actor 构造也先以默认尺寸布局；新角色进入可触发 64→128 的全局缓存切换。静态缓存 key 已含尺寸，仍全局清理没有必要。
- `OverlayFishingLayers.BitmapFor`（121）在 ShowLayered 的 UI 回调中执行 File.Exists、File.OpenRead、PNG 尺寸检查和 BitmapImage.OnLoad 解码。
- `LoadPack`（255）首次扫描全部片段所有层文件。缺失层直接 return null，没有缓存这次失败；后续请求可能重复扫描。

本地统计：橘猫 16 个片段、合计 74 帧，每次完整存在性检查 74×9=666 项。当前文件树共 3330 张动画 PNG，压缩体积约 124.8 MiB；不能把这个值当解码内存。128px actor 对应图层约 174×139；大飞鱼 bodyTextureScale=2 的身体层会按双倍目标采样。未建议把所有 PNG 同时预解码。

优化顺序：先确定正式尺寸，再按同一缓存 key 预热；取消因单个 actor 尺寸变化清全局缓存；后台有限并发解码，Freeze 后交还 UI 线程；按下一动作和近几帧预取，有界队列、版本令牌与离场取消。后台访问目前静态字典必须同步或由单个资源管理线程拥有，不能直接给 BitmapFor 套 Task.Run。缺图失败应短期负缓存，并在资源版本变化时失效。

## 4. P1：每份状态都刷新全场布局，并强制 UpdateLayout

证据：
- `PondScenePresenter.Apply`（197）每次状态执行 SyncSpots、ApplyOwnActor、SyncOthers。
- 每个 actor 先 Apply（442，含 RelayoutCluster），再 ApplyChrome（479，又含 RelayoutCluster），最后 Place（517，调用 UpdateLayout）。
- RelayoutCluster（675）重新设置尺寸和位置、测量昵称/气泡，并在显示圆环时重建其 mask。
- 座位查询即使缓存命中，也在 `OverlaySeatArt` 先进行 File.Exists；每份全量状态都会重复走。
- Unity 非紧急脏状态合并窗口为 1/15 秒（DesktopAppBootstrap.cs:260）；紧急自身事件可绕过。不是固定每秒发送 15 次，也不是每渲染帧发送。

若 20 个 actor、持续 15 份状态/秒，仅此链路名义上产生约 600 次 RelayoutCluster 和 300 次 UpdateLayout 调用/秒；相同属性值可能被 WPF 短路，因此这不是实际完整布局次数。当前静水湾、竹林布局各有 20 个 actor 槽。

优化：拆开不可变布局、低频昵称与统计、状态切换和本地时间进度；数据/位置/尺寸不变即跳过。布局先应用一次，位置用已知锚点计算，去掉每 actor 每快照的 UpdateLayout。座位与装饰只在塘或布局版本变化时重建。

## 5. P1：按回调次数推进动画，卡一次就把后续动作拖长

证据：`OverlayPetActor.cs:1090` 的 OnFrameTick → AdvanceLayered（1355）每次只推进一帧；ShowLayered（1318）根据当前源帧 duration 设置下一次计时器间隔，没有单调时钟累计、时间落后补偿。

UI 线程如果被解码阻塞 200ms，恢复后只前进一帧，丢失的时间会变成动作额外耗时。状态机又有等待 reel/catch 完成的保护，积累后会出现服务器已进入下一相位、猫还在上一动作的观感。

优化：采用共享表现时钟和 Stopwatch 单调时间；按累计片段时间选择当前帧，保留起始/关键结算/结束事件且保证只触发一次，跳过过期的非关键帧。UI 修改仍在 UI 线程，不应简单把现有 DispatcherTimer 换为线程池定时器。

需区分资源的刻意低帧率：当前 reel/reel_empty 是 100ms/帧；catch 为 100–360ms/帧；fishing 是单帧 hold。它们本来就不是 60fps 连续动作。消除卡顿可以恢复正确时长，但不会凭空补出美术中没有的中间帧。

## 6. P2：其他优化与待验证项

- **隐藏/离屏继续更新**：hide_overlay 只调用 Hide；未见角色与视口可见性关联的表现调度。宽场景横移也继续推进全部 actor。可暂停隐藏和视口外绘制，逻辑时间继续，返回时重同步。
- **九宫格永久缓存**：OverlayNineSlice 的 RenderCache/BrushCache 按源图和目标尺寸缓存，没有上限。动态文字气泡可能产生很多尺寸组合。应统计缓存条目/字节，复用或限定尺寸、设置 LRU。已有缓存意味着不是每个按钮每帧都重绘九宫格。
- **聊天新消息全量重建**：OverlayChatPresenter.cs:90 清空再生成最多 20 条消息并 UpdateLayout；已有签名跳过无变化快照，不能误报“每快照重建聊天”。优先级低于角色刷新；改为增量插入、聊天收起时推迟日志列表构建。
- **大背景与首次命中**：当前 maps PNG 约 2170×725，单份 BGRA 约 6 MiB。换塘同步加载，首次点击穿透采样会 CopyPixels 整张背景，另分配约 6 MiB CPU 缓存。可在后台准备低分辨率 alpha 命中图；保证阈值与边缘渐隐一致。
- **透明合成**：透明窗口 + 两层场景 OpacityMask + 多角色九层图会增加合成工作，但不能仅凭 AllowsTransparency=True 就断言全部软件渲染。记录 RenderCapability.Tier、DPI、GPU/CPU 与帧时间；原有 FISH_SOCIAL_OVERLAY_SAFE_WINDOW=1 可作诊断对照，它同时改变背景与置顶，不是严格单变量实验，也不是最终产品修复。
- **紧急 Dispatcher 调度**：QueueStateDispatch 对每条紧急状态都投递 Input 回调；UI 阻塞期间可能排多个空 drain。最新状态合并已实现，暂不判断是主要瓶颈；改为单一 pending operation 和优先级升级，需保护 ACK 与相位转换事件。

## 已有优化，不应重复推倒

IPC 读取/解析在后台，Unity 写管道也有后台线程；状态有最新值合并；日志后台写入并限量；帧图片 Freeze、按目标尺寸解码且共享缓存；Image.Source 与 ZIndex 有相同值跳过；hold 片段会停止帧定时器；聊天有内容签名。

先修上述实现漏洞，不建议从 WPF 重写为另一套渲染器，也不建议继续通过删鱼竿、猫咪关键帧或所有 UI 反馈来换性能。

## Windows 验证方案与建议门槛

以下为拟定验收指标，不是已经测得的结果：

1. 1、5、20 角色；同猫种和混合猫种；冷启动、热循环、连续进出/换塘 30 次；窗口隐藏 5 分钟再恢复。另测 960×560 / 800×400 / 600×300 与 100% / 150% DPI。
2. 同时采 WPF 和 Unity：CPU 时间、Private Bytes、GC 分配、可达 actor 数、活跃 timer 数、图层缓存字节/命中率/解码次数、状态队列等待与 apply 的 p50/p95/max、动画显示时间与计划时间偏差。
3. 每次离塘后旧 actor/timer 数应回到基线；热身后多轮内存不持续线性增长；同片段混合猫种切换不导致其他 actor 的缓存重新解码。
4. 稳定动画阶段 UI 线程目标无磁盘读图/PNG 解码；可见交互目标 p95 反馈 <100ms，拖动/平移不出现 >50ms 的长任务；正确源帧时序必须保留，不以降低角色表现换通过。
5. 使用现有 `%TEMP%/FishSocialOverlay-latency.log` 的 overlay_state_applied/applyMs 与命令记录，并对齐 Unity Player.log 的 overlay_command_received、overlay_state_queued、frame_stall。当前 applyMs 通常约每秒采一份（或 ACK 触发），且不包含 Dispatcher 排队时间、WPF 后续渲染，也不覆盖独立帧计时器；不能用它单独证明全程流畅。
6. 先加低开销汇总指标和生命周期/共享缓存回归，再按 P0→布局→时钟→合成逐项 A/B。不要同时关闭多个功能后把改善全部归因于某一项。

## 官方机制依据

- [DispatcherTimer：调度精度与对象保活](https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatchertimer?view=windowsdesktop-9.0)。官方说明计时器受到 Dispatcher 队列优先级和其他工作影响，绑定对象方法会保留对象。
- [WPF Layout and Design](https://learn.microsoft.com/en-us/dotnet/桌面端/wpf/advanced/optimizing-performance-layout-and-design)。减少无必要的布局计算，UpdateLayout 会触发布局过程。
- [WPF 硬件与像素渲染成本](https://learn.microsoft.com/en-us/dotnet/桌面端/wpf/advanced/optimizing-performance-taking-advantage-of-hardware)。实际成本依赖渲染像素与硬件能力，需要实机测量。
