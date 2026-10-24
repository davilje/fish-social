# STEAM-DESKTOP-22H 共用身份与 Unity 接入记录

2026-10-24；开发基线 commit `4ecc32482ff13a5529a25ba541e8b917ad18cc27`。本记录不变更 planning 状态。开始时已有设计、计划表和 CHANGELOG 等未提交修改；开发中另有 I 音频任务并行。本任务仅补 H 与必需的共享协议，不以并行音频资源代表 I 联测通过。

## 实现

- 服务端 `fishingPresentation.ts` 在真实阶段转换分配稳定 `presentationActionId`、`presentationEventId` 和单调 `presentationRevision`；序列化和重复阶段不生成新身份。装饵/新打窝开启新动作，抛竿、等待和咬钩沿用该动作。新增字段经共享类型、Unity DTO、用户 merge 和 IPC 信封贯通。
- `FishingPresentationSession` 管鱼塘会话、当前 owner/epoch、角色 generation、预测 command 命名空间、权威追认别名、静默 snapshot 和取消。WPF 命令带独立进程 `commandSessionId`；同一命令重复到达不重建动作。旧 revision、旧 epoch、已取消 action、换皮/换位前的回调不消费。结束后新 action 清除旧取消状态。
- 消费键分消费者保存，视觉不能吞声音；每条循环流只存最高 occurrence，当前 action 防重，过期 action/离席角色的键及时释放。普通 cue 不排队。`MainWindow` 在已发布 H marker 后把同一信封送给 Unity，发送前计算本进程排队耗时；接收端另量本进程待分发耗时，总预算上限 250ms。两个进程的 Stopwatch 值不直接相减。
- `DesktopAppBootstrap.WaterMarkerReceived` 是给 I 的共用事件接口，带相同 session/action/event/ownerEpoch/generation/tier；`FishingPresentationSession.TryConsume(..., consumer)` 为音频保留独立消费位。H 不新增音频播放器。I 当前并行适配状态由总交付报告另列，不能以接口存在代替至少 30 个事件的实机画声联测。
- 尺寸缺省 S，不从 `phaseContext.sizeM`、未来入包鱼获或调试鱼列表推断 H 尺寸。真实水面 view 可提供允许显示的米体长或归一到 128 槽位的可见鱼身范围；阈值分别 .30/.80/1.50m 与 16/32/56px。浮漂、等待和窝料强制使用落水物规格 S。槽位比例由 ContactProvider 独立提供，Canvas/DPI 不进入鱼档位判断。

## Unity 正式兜底

`SpriteFramePetRenderer` 只在本帧最终九层图像已赋值且 view 可见时发布源帧。卡顿跳过的中间 `ShowLayer` 不发布历史 marker；手动资源预览不发游戏事件。源 marker 读取统一生产入口导出的 `StreamingAssets/Fishing/source-water-markers.json`，不用扩帧索引或固定等待。

`UnityFishingWaterMarkerBridge.AnchorProvider` 接收 `IUnityFishingWaterAnchorProvider`。该真实 view 必须给出接触点、岸内裁剪矩形、效果所在水面 RectTransform、槽位比例和已允许显示的尺寸。无真实接触、无水面 root 或接触点超裁剪范围时静默；不会拿猫图中心造水面。当前 Home portrait 没有世界水面 ContactProvider，因此缺省不显示水花，这是明确降级，不声称 portrait 已具有 22B 世界水面解算。

有真实 provider 时，`UnityFishingWaterEffects` 使用正式 `effects.json` 和同版 18 张 PNG 渲染：全局固定 24 RawImage / 8 裁剪组，每角色 2 组，其他玩家容量至多 6 组/12实例，给自己保留 2组/12实例。低优先级不排队，组按优先级回收；固定单一 unscaled Update，闲置停止；资源在预热中异步读盘，正式 tick 不读盘。装饰 RawImage 的 raycastTarget 全为 false。L/XL 双水冠用左右半图，强动作替换普通扰水，咬钩替换等待，FX04 时长 300–600ms。质量关闭、取消、结束与 owner 更换最多 100ms 收敛。水面 view 销毁后清理对应池组，下次真实 view 创建时只修复丢失的固定池槽。

标准/简化/关闭复用现有设置页持久偏好，全局减少动效可将标准上限降为简化。简化仅保留自己的入水、咬钩和真实拍水关键反馈；远端完整 view 目前没有绑定 Unity ContactProvider，正式多人显示由 WPF 完成，不能以预算核心测试声称 Unity 远端世界视图已实机验收。

当前认可源动作没有真实破面/拍水 marker；FX05 仅保留双重校验接入能力，现有动作静默。FX06/07/09 均无 H 订阅或绘制路径。

## 已执行验证

- 共享 TS 编译通过：`node node_modules/typescript/bin/tsc -p shared/tsconfig.json`。
- Server 相关测试 37/37：H 稳定身份 2、现有睡眠回归 28、并行 I 已有身份链回归 7。记录：`docs/art/water-effects-22h/evidence/server-presentation-tests.log`。
- .NET 纯 C# 2090 项检查通过：快照/预测追认/重复命令/旧 revision/取消早于 ACK/漏掉 baiting-casting 后首次 waiting-hooked-effort 仍追认同一预测动作/钓鱼与打窝族隔离/下一真实轮恢复/角色更换/owner/双消费者/尺寸边界/资源预算；包含 1000 个真实动作各 999 次循环的有界账本验证。记录：`evidence/shared-session-tests.log`，执行项目：`desktop-overlay/tests/FishSocialOverlay.PresentationSessionTests.csproj`。
- Unity 2021.3.29f1c1 macOS EditMode 208/208 通过，含新 H 注入真实 contact provider / 加载正式 PNG / 无 provider 静默 / native owner 互斥 / raycast 关闭测试，及钓鱼动画、休息、协议和睡眠回归。记录：`evidence/unity-editmode.xml` 与 `.log`。首次失败为并行 I 编译进行中与既有 private queue 反射兼容问题，已修复。
- 后续增加真实水面 root 销毁/重建 30 次的 Unity 池恢复行为断言后，H+sleep 定向 18/18 XML 显示通过（`evidence/unity-final-targeted.xml`）；该次编辑器退出阶段发生 Mono `mono-threads.c:702` 断言崩溃，不能记为正常进程退出，日志为 `unity-final-targeted-shutdown-crash.log`，本轮 crash blob 已压缩移入同目录保留。最后一次 compile-only 检查包含最终 owner 切换轮询代码并正常退出 0，日志为 `unity-final-compile.log`。前述 208/208 的完整回归是正常退出的独立证据。
- 最终代码与最新同步 metadata 再次全回归 **208/208 通过，进程正常退出 0**（2026-10-24 21:30，21.73s），包含新增 30 次 root 销毁/重建恢复断言及漏包动作追认修复；`evidence/unity-final-full.xml` 与 `.log` 是最终验收证据。之前那次 Mono 退出异常保留为历史诊断，复测已取得正常退出。
- 最后核验 `source-water-markers.json` 的 WPF、Unity StreamingAssets 与随包 native 三份字节一致，SHA-256 `7086dc1760a4a22c4e7e5987cce4a2ea2031d1a60f99092e6f8226cecb555ca7`。

## 实机待验收

以上是代码行为测试、Unity 编辑器执行和资源接入证据。尚未执行 Windows 原生桌面的真实点击穿透、0/150/400ms 冷解码录屏、20 角色同条件 A/B、30 次进出塘+10分钟主线程/内存/线程量测，以及至少 30 次音画差采样。因此没有把 p95≤1ms、p99≤2ms、音画差≤80ms 写成实测成绩。Unity 注入 provider 的测试验证 admission/真实纹理解码与裁剪对象创建，不替代正式世界 view 或 Windows 的视觉审美验收。

## 复现命令

以下命令在仓库根目录运行；`node`、`dotnet` 和 Unity 路径可替换为本机实际安装。

```sh
node node_modules/typescript/bin/tsc -p shared/tsconfig.json
node node_modules/vitest/vitest.mjs run server/src/__tests__/fishingPresentation.test.ts server/src/__tests__/fishingSleep.test.ts server/src/__tests__/fishingPresentationIdentity.test.ts
dotnet run --project desktop-overlay/tests/FishSocialOverlay.PresentationSessionTests.csproj
/Applications/Unity/Hub/Editor/2021.3.29f1c1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD/fish-social-unity" -runTests -testPlatform EditMode -testFilter 'DesktopFishingWaterEffectsTests|DesktopFishingAnimationTests|DesktopFishingRestMotionTests|DesktopProtocolResponseTests|DesktopFishingSleepTests' -testResults /tmp/fish-22h-unity-tests.xml -logFile /tmp/fish-22h-unity-tests.log
/Applications/Unity/Hub/Editor/2021.3.29f1c1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath "$PWD/fish-social-unity" -logFile /tmp/fish-22h-unity-compile.log
```
