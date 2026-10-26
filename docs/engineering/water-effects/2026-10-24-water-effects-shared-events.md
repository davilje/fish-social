# STEAM-DESKTOP-22H 共用身份与 Unity 接入记录

2026-10-24；开发基线 commit `4ecc32482ff13a5529a25ba541e8b917ad18cc27`。本记录不变更 planning 状态。开始时已有设计、计划表和 CHANGELOG 等未提交修改；开发中另有 I 音频任务并行。本任务仅补 H 与必需的共享协议，不以并行音频资源代表 I 联测通过。

## 实现

- 服务端 `fishingPresentation.ts` 在真实阶段转换分配稳定 `presentationActionId`、`presentationEventId` 和单调 `presentationRevision`；序列化和重复阶段不生成新身份。装饵/新打窝开启新动作，抛竿、等待和咬钩沿用该动作。新增字段经共享类型、Unity DTO、用户 merge 和 IPC 信封贯通。
- `FishingPresentationSession` 管鱼塘会话、当前 owner/epoch、角色 generation、预测 command 命名空间、权威追认别名、静默 snapshot 和取消。WPF 命令带独立进程 `commandSessionId`；同一命令重复到达不重建动作。旧 revision、旧 epoch、已取消 action、换皮/换位前的回调不消费。结束后新 action 清除旧取消状态。
- 消费键分消费者保存，视觉不能吞声音；每条循环流只存最高 occurrence，当前 action 防重，过期 action/离席角色的键及时释放。普通 cue 不排队。`MainWindow` 在已发布 H marker 后把同一信封送给 Unity，发送前计算本进程排队耗时；接收端另量本进程待分发耗时，总预算上限 250ms。两个进程的 Stopwatch 值不直接相减。
- `DesktopAppBootstrap.WaterMarkerReceived` 是给 I 的共用事件接口，带相同 session/action/event/ownerEpoch/generation/tier；`FishingPresentationSession.TryConsume(..., consumer)` 为音频保留独立消费位。H 不新增音频播放器。I 当前并行适配状态由总交付报告另列，不能以接口存在代替至少 30 个事件的实机画声联测。
- 尺寸缺省 S，不从 `phaseContext.sizeM`、未来入包鱼获或调试鱼列表推断 H 尺寸。真实水面 view 可提供允许显示的米体长或归一到 128 槽位的可见鱼身范围；阈值分别 .30/.80/1.50m 与 16/32/56px。浮漂、等待、收杆和窝料强制使用落水物规格 S。槽位比例由 ContactProvider 独立提供，Canvas/DPI 不进入鱼档位判断。

## Unity 正式兜底

`SpriteFramePetRenderer` 只在本帧最终九层图像已赋值且 view 可见时发布源帧。卡顿跳过的中间 `ShowLayer` 不发布历史 marker；手动资源预览不发游戏事件。相同 source frame 的插值也可能稍后真正触水，因此 `float.contact` / `groundbait.contact` / `retrieve.contact` 和 `fish.surface` 会在可见最终九层每帧重查真实 provider；成功消费仍按 action+occurrence 一次性去重。waiting、bite 和其他连续反馈仍按新源帧门控，音频仍按新源帧/结果确认门控。源 marker 读取统一生产入口导出的 `StreamingAssets/Fishing/source-water-markers.json`，不用扩帧索引或固定等待。

`UnityFishingWaterMarkerBridge.AnchorProvider` 接收 `IUnityFishingWaterAnchorProvider`。该真实 view 必须给出接触点、岸内裁剪矩形、效果所在水面 RectTransform、槽位比例和已允许显示的尺寸。无真实接触、无水面 root 或接触点超裁剪范围时静默；不会拿猫图中心造水面。当前 Home portrait 没有世界水面 ContactProvider，因此缺省不显示水花，这是明确降级，不声称 portrait 已具有 22B 世界水面解算。

有真实 provider 时，`UnityFishingWaterEffects` 使用正式 `Effects/Fishing/v4/effects.json` 和同版 18 张 PNG 渲染：全局固定 24 RawImage / 8 裁剪组，每角色 2 组，其他玩家容量至多 6 组/12实例，给自己保留 2组/12实例。低优先级不排队，组按优先级回收；固定单一 unscaled Update，闲置停止；资源在预热中异步读盘，正式 tick 不读盘。装饰 RawImage 的 raycastTarget 全为 false。真实出水的 L/XL 双水冠用左右半图；咬钩和水下挣扎仅用波纹，绝不生成水冠/水滴。只有实际抛竿触水、窝料触水、收杆触水及真实破面/拍水可有少量水花。波纹预热时按真实 PNG alpha 上沿整体下移，完整椭圆位于接触水线下；每组另设水线以下 RectMask2D，最终位移/缩放也不能越过水线。水冠/飞溅水滴允许在线上。强动作替换普通扰水，咬钩替换等待，FX04 时长 300–600ms。质量关闭、取消、结束与 owner 更换最多 100ms 收敛。水面 view 销毁后清理对应池组，下次真实 view 创建时只修复丢失的固定池槽。

标准/简化/关闭复用现有设置页持久偏好，全局减少动效可将标准上限降为简化。简化仅保留自己的入水、真实收杆触水、咬钩和真实拍水关键反馈，均只用波纹；远端完整 view 目前没有绑定 Unity ContactProvider，正式多人显示由 WPF 完成，不能以预算核心测试声称 Unity 远端世界视图已实机验收。

FX05 `fish.surface` 现在覆盖实际 `reel` source0 插值过程中鱼身跨水线：只按实际发布 fish layer 接触成立发出，`stopping/resolving` 固定 occurrence 0，按真实可见鱼身取 S/M/L/XL；无接触时静默。`fish.slap` 仍只允许 hooked/effort，当前源动作无真实拍水 marker，继续静默。恢复 FX09 `retrieve.contact`，仅允许 `stopping/resolving`：实际发布的 `reel` source0 的 line/水线交点，或 `reel_empty` source0 的 bobber 真实触水点；与音频 `retrieve.start` 无推导关系。每动作 occurrence 固定 0，一次性；主动 stop 保留该动作，仅开放收杆接触，其余旧效果结束；快照不补播。leave/exit/失败/断线或其他硬取消仍优先压制收杆。`result/catch/escape/seated/idle` 不能产生收杆水花。FX06/07 无 H 订阅或绘制路径。

## 已执行验证

- 共享 TS 编译通过：`node node_modules/typescript/bin/tsc -p shared/tsconfig.json`。
- Server 相关测试 37/37：H 稳定身份 2、现有睡眠回归 28、并行 I 已有身份链回归 7。记录：`docs/art/water-effects-22h/evidence/server-presentation-tests.log`。
- .NET 纯 C# 2128 项检查通过：快照/预测追认/重复命令/旧 revision/取消早于 ACK/漏掉 baiting-casting 后首次 waiting-hooked-effort 仍追认同一预测动作/钓鱼与打窝族隔离/下一真实轮恢复/角色更换/owner/双消费者/尺寸边界/资源预算/真实收杆 phase 白名单、单次消费、硬取消和快照优先/快速 stop 后首次 resolving ACK 不重播；包含 1000 个真实动作各 999 次循环的有界账本验证。记录：`evidence/shared-session-tests.log`，执行项目：`desktop-overlay/tests/FishSocialOverlay.PresentationSessionTests.csproj`。
- Unity 2021.3.29f1c1 macOS EditMode 208/208 通过，含新 H 注入真实 contact provider / 加载正式 PNG / 无 provider 静默 / native owner 互斥 / raycast 关闭测试，及钓鱼动画、休息、协议和睡眠回归。记录：`evidence/unity-editmode.xml` 与 `.log`。首次失败为并行 I 编译进行中与既有 private queue 反射兼容问题，已修复。
- 后续增加真实水面 root 销毁/重建 30 次的 Unity 池恢复行为断言后，H+sleep 定向 18/18 XML 显示通过（`evidence/unity-final-targeted.xml`）；该次编辑器退出阶段发生 Mono `mono-threads.c:702` 断言崩溃，不能记为正常进程退出，日志为 `unity-final-targeted-shutdown-crash.log`，本轮 crash blob 已压缩移入同目录保留。最后一次 compile-only 检查包含最终 owner 切换轮询代码并正常退出 0，日志为 `unity-final-compile.log`。前述 208/208 的完整回归是正常退出的独立证据。
- 最终代码与最新同步 metadata 再次全回归 **208/208 通过，进程正常退出 0**（2026-10-24 21:30，21.73s），包含新增 30 次 root 销毁/重建恢复断言及漏包动作追认修复；`evidence/unity-final-full.xml` 与 `.log` 是最终验收证据。之前那次 Mono 退出异常保留为历史诊断，复测已取得正常退出。
- 用户确认 v4 与真实收杆规则后的最终回归 **208/208 通过，正常退出 0**（2026-10-24 22:59，22.96s），证据 `evidence/unity-v4-final-full.xml` 与 `.log`；覆盖实际 v4 PNG alpha 完整椭圆位于水线下、独立 mask 经变换不越线、咬钩只有波纹、真实 `reel` line / `reel_empty` bobber 接触、同 source5 插值由未触水到触水仅发一次、实际 reel fish.surface 由未跨线到跨线按 12/60px 可见鱼身分 S/XL 并绘制 4/6实例、resolving 中真实鱼出水不提前取消、无接触静默、每动作收杆一次与取消压制；窝料严格 2 波纹+1 水冠+1 水滴，只有两弧以真实接触点对称偏移，水冠/水滴出发中心就是实际触水点。简化 FX09 与 WPF 同为自己的纯波纹。最终 compile-only 也正常退出 0，日志 `evidence/unity-v4-final-compile.log`。首次新增双 clip 测试夹具错误复用旧 revision 被正确 fence，修正夹具单调序号后通过。
- 上述 v2 阶段 metadata 一致性证据保留；v4 最终 `source-water-markers.json` 的 WPF、Unity StreamingAssets 与随包 native 三份字节一致，包含实际收杆源点；SHA-256 `57071c0178b44917e46d5f006f7719f4b03cfdf9cf688974fccf05399d2d4980`。

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
