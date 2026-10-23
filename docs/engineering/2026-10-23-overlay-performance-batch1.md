# Overlay 性能优化第一批：生命周期与资源缓存

日期：2026-10-23。状态：代码已实现，Windows 原生运行与性能验收待执行。

对应 [性能审计](2026-10-23-overlay-performance-audit.md) 的第一批改造。未修改 Web 页面、Unity 状态机、动画帧时长或钓获结算逻辑；后台解码、增量布局和共享动画时钟属于后续批次。

## 已实现

### 角色生命周期

`OverlayPetActor.Dispose()` 幂等停止并解除帧、状态刷新、悬停和气泡定时器，清理气泡动画、菜单、悬停引用及帧图片引用。延迟回调和 Apply/ShowSpeech/ShowHint 遇到已销毁对象时不再激活它。

`PondScenePresenter.SyncOthers` 删除玩家、`ClearOthers` 换塘、`Dispose` 关闭场景统一回收角色。窗口 OnClosing 先解除聊天提示持有的角色，再销毁场景，并停止聊天 ACK 定时器。动画切片切换继续用原来的 ReleaseFishingLayers，不会误销毁角色。

### 引用与热缓存

新增与 WPF 类型无关的 `OverlayResourceCache<T>`，正式图片缓存直接使用此实现，便于执行真实缓存行为测试。

- 缓存 key 继续包含解码尺寸与图片路径，多个尺寸共存。
- 同猫种同动作的多个玩家共享片段使用计数；首个使用者对该片段的实际图片路径加引用，最后一个退出才解除。
- 公共竿、线、鱼、浮漂、鱼饵使用相同路径，不同猫种共同保留。删除了旧的“按动作目录整批清除”逻辑。
- 零引用图片留在热缓存；命中更新 LRU 顺序，超出预算时只清理未使用的图片。
- 默认预算 **64 MiB 解码像素数据**，由实际像素宽高和位深计算。它不是进程内存上限，也不含 WPF/GPU 开销。正在使用的资源允许暂时超过预算，释放引用后再收敛；不为满足预算强行删除当前动作需要的图片。
- 当前资源管理仍由 UI 线程调用，没有引入不安全的后台字典访问。预热和冷帧解码仍同步，后续批次再移出 UI 线程。

### 预热与尺寸

场景先 ApplyChrome 确定实际角色尺寸，再预热和应用动作。预热以当前 body 宽高计算图层尺寸，并按猫种＋尺寸记录；角色尺寸变化可重新预热。皮肤切换先根据新皮肤确定图层摆放，再读取第一帧。

取消 `SetDecodeSize` 全局清缓存，新 actor 构造、角色放大缩小不再清除其他角色已加载的图片。

### 观察指标

沿用 `%TEMP%/FishSocialOverlay-latency.log` 已有的状态采样日志（通常每秒或 ACK 触发），新增：

| 字段 | 含义 |
|---|---|
| actors | 已创建且尚未 Dispose 的角色数，不等同于 GC 内存快照中的可达对象数 |
| activeClips | 正在使用的猫种＋片段组合数，不是角色总数 |
| cachedFrames | 解码图层图片缓存条目数，包含不同目标尺寸 |
| cachePixelBytes | 缓存像素载荷估算，不是进程工作集或显存 |
| layerDecodes | 进程累计成功 PNG 解码次数，热循环应显著少于冷启动 |

没有为统计新增定时器或逐帧文件写入。

## 验证结果

- **8/8 缓存行为测试实际运行通过**。用的就是正式缓存源文件；涵盖混合猫种共享、同种多引用、热帧复用、LRU、活跃资源预算保护、尺寸并存、替换记账、30 次模拟场景引用循环。Mono 和 .NET 8 均运行通过。
- **39/39 Python 源／资源回归通过**，含新增的退出回收与尺寸预热接入检查。保护未变的动作推进、Manifest 解析、座位交互和资源哈希。
- **正式 WPF 应用和 Windows 回归程序交叉编译通过：0 errors**。使用临时安装的 .NET SDK 8.0.408 和 `EnableWindowsTargeting=true`，保留生产 `netcoreapp3.1` 目标，包含 C#、XAML/BAML 编译。构建有 5 条原有目标框架过期提示，不属于本次编译失败。
- Windows 回归入口已编译、**未运行**：6 组检查，涵盖原 PNG 解码校验、真实 mixed-cat 贴图共享、同种用户引用、真实 Actor 预热尺寸、DispatcherTimer 停止及弱引用回收、30 次真实场景进出清理。
- 本轮未修改 Unity 程序，未重新执行 Unity 测试。未在 macOS 模拟或宣称 Windows 帧率、GC 回收、交互延迟或 30 次真实换塘性能已验收。

原静态 fixture 还保留上轮 HUD JSON 哈希；本次只将它校正为进入任务前的 `1a3c8769` 导出版本，未改变该 HUD JSON。对本轮修改的缓存／生命周期代码用专项检查替代整文件不可变锁，未更改帧推进方法的保护基线。

## 复跑

仓库根目录：

```sh
python3 -m unittest discover -s desktop-overlay/tests -p 'test_*.py'
dotnet run --project desktop-overlay/tests/FishSocialOverlay.CacheTests.csproj
```

macOS/Linux 上只编译 Windows 项目：

```sh
dotnet build desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj -p:EnableWindowsTargeting=true
```

Windows 配有现有项目要求的 WPF 运行时与 SDK 时，执行原生回归：

```powershell
dotnet run --project desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj
```

该程序自行创建测试 Application，使用实际图层资源和角色类；应完整输出 6 组 PASS。任何异常均应视为失败。项目引用会复制需要的 OverlayResources；测试源码与生成目录已从正式应用编译输入中排除。

## Windows 验收

1. 按项目现有 Windows 发布流程重新构建 EXE，运行新版本；本轮交叉编译产物未替换已安装游戏。
2. 同塘使用两种猫：一方切动作，另一方继续播放；日志的解码次数不应因此重复增加。
3. 完成热身后连续钓鱼、反复切塘 30 次，比较 actors/activeClips 是否与当前场景一致，缓存是否在活跃资源释放后回到预算内；用内存快照验证旧 actor 可回收。
4. 新角色加入或尺寸改变，已有角色动作不闪空且无需重新加载整套动作。
5. 与改造前相同机器、相同场景比较 CPU、Private Bytes、动作切换长任务和交互延迟。第一批仍有同步冷解码，不把尚存的首次加载卡顿当作后台解码已完成。
