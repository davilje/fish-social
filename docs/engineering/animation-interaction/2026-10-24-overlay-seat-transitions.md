# STEAM-DESKTOP-22E 入塘与离席表现修复

设计／实现：2026-10-24。状态：代码已实现，Windows 原生运行与视觉待验收。

## 原因与修改

原先 `PondScenePresenter` 在收到空 `SpotId` 的同一帧，先换成等候区布局，再立即调用 `Place`。角色的 `leave` 动画仍在播放，所以站起动作在等候区出现。等候落点又被当成图片中心，和动画 manifest 的脚底锚点不是同一坐标。

- `ApplyScenePlacement` 统一以脚底保存场景坐标；有座位时从已设计的 `actor-pet` 矩形换算脚底，等候区落点直接作为脚底。`FootInSlot` 与分层图片使用同一 manifest 的 `referenceFoot`，保留现有座位、美术和水面接触线。
- `OverlaySeatMotion` 只管理表现：首次快照直接落位；随后从等候区平滑到达钓位再坐下；离席保留原座位位置与布局，待 `leave` 尾帧完整显示后再移至等候区。平移为 180–650ms，使用既有站立姿势，不新增行走美术。
- 收杆／展示／复位链仍完成后接站起；快速钓鱼指令会等坐下结束后执行最新表现。服务器座位释放与可用操作不被延迟。
- 重复快照不重启移动或站起；重新入座取消旧离席目标；等候队列重排从当前脚底继续移动；切塘／移除角色沿用 Dispose，取消异步帧并解除共享时钟订阅。
- 坐下／站起每一帧在九层图片完整发布后才开始计时，避免冷解码、长时间隐藏后的时钟追赶跳过站起尾帧。缺失图片时仍按时推进，不把角色永久卡在已释放座位。

## 验证范围

- 可移植迁移核心 **8/8**：站起门禁、连续精确落点、重复快照、重新入座、移动中改向、场景重置、等候队列重排、零距离与隐藏时间追赶。
- 新增 Windows WPF 专项：橘猫／三花／大肥鱼 × 96／128 尺寸，检查实际分层图片脚底、到位再坐下、尾帧停留、重复快照、离席中重入、收杆后离席及隐藏恢复。正式程序与测试项目交叉编译通过；**未在 macOS 上运行 WPF 测试**。
- 开发中同一工作区另有 22D／22F 修改。保留这些变更，使用独立的临时源码副本编译，避免两个构建写入同一 obj/apphost。E 更新源锁定清单中的 `StartLayerClip`、`AdvanceLayered`、`ApplyLoadedVisualState`；核对补帧源帧映射后同步 `Parse`／`LayerPath` 两项兼容基线。全量源／资源检查 **51/51** 通过。
- Windows x64 自包含测试包：`dist/overlay-seat-22e-2026-10-24.zip`。含构建时 22D／22F 的工作区代码快照；本轮验证范围为 E，D／F 专项验收归对应任务。包内 `BUILD-INFO.json` 记录构建源码哈希与验证边界。此前 `dist/overlay-ui-fixes-2026-10-24.zip` 是 A–C 旧包，**不包含 E**。测试包不会自动替换已运行的 Unity／Overlay。

## 复跑

```sh
dotnet run --project desktop-overlay/tests/FishSocialOverlay.SeatMotionTests.csproj
python3 -m unittest discover -s desktop-overlay/tests -p 'test_*.py'
dotnet build desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj -p:EnableWindowsTargeting=true
```

Windows 上运行 `dotnet run --project desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj`。本机不具备 Windows WPF 运行环境，编译通过不等于实机验收。

## Windows 验收

1. 进入三种鱼塘，橘猫／三花／大肥鱼在等候区脚底落位正确；点击钓位，移动到位后坐下，不先坐下再滑动。
2. 坐下后离席：角色保持原钓位站起，完整显示末帧后才离开，昵称和命中区域跟随。
3. 收杆结束前离席、离席中快速重新入座、入座预测失败：最新目标有效，不被旧离席拉回等候区，不永久停在原座位。
4. 最小化再恢复、视口横移、不同窗口尺寸：站起不被隐藏时间跳过；检查动态位置与点击区域一致。
5. 其他玩家加入／离席／断线、反复换塘：队列重排无突然跳动，旧角色不会复活或留下时钟任务。

Windows 实机验收完成前，不勾选规格中的原生运行和视觉验收项。
