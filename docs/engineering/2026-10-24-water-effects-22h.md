# STEAM-DESKTOP-22H：水面效果实现与验证

后续更新：用户已确认v4简约水魔法风格，两端已切换正式v4资源；见[最新接入与验证](2026-10-24-water-effects-22h-v4.md)。以下v2记录及测试包hash保留为历史证据。

本轮基线 commit：`4ecc32482ff13a5529a25ba541e8b917ad18cc27`。开发日期：2026-10-24。

## 预览与资源

[猫咪同屏预览](../art/water-effects-22h/index.html) 支持 11 种角色、96/128/192 槽位、浅深水面、四档 A/B、六类效果、标准／简化／关闭、暂停和逐帧，以及 20 座位预算对照。浏览器从仓库根目录提供 HTTP 服务后打开 `/docs/art/water-effects-22h/`，不能仅用本地 `file://` 打开引用跨目录资源的页面。

- [浅水标准截图](../art/water-effects-22h/preview-light.png)、[深水 XL 截图](../art/water-effects-22h/preview-dark-xl.png)、[简化截图](../art/water-effects-22h/preview-reduced.png)。
- [资源／许可说明](../art/water-effects-22h/README.md)、[213 个 Web 组合验证](../art/water-effects-22h/verification.json)、[真实配置绑定检查](../art/water-effects-22h/bindings-verification.json)。
- 正式候选 `desktop-overlay/OverlayResources/effects/fishing/v2/`：8 水花、8 水纹、2 水滴，原创 SVG 源稿和确定性生成脚本，不是占位图。原创效果图形按 CC0-1.0 交付，既有猫咪许可保持原状；`manifest.json` 记录逐文件来源、PNG/SVG/配置 hash 和候选状态，用户视觉验收前不宣称“已认可”。
- 16 张 128×64 和 2 张 64×64 RGBA，共 557,056 bytes（0.53125 MiB）解码像素；8px 透明留边。Unity `Assets/StreamingAssets/Effects/Fishing/v2/` 与 `FishSocialOverlay/OverlayResources/effects/fishing/v2/` 两份随包副本逐项 hash 相同。
- 游戏设置“托盘与其他”页沿用原手账样式新增钓鱼水面特效三档，保存偏好；全局减少动效开启时使用简化，关闭仍优先。Overlay 不新增控制条。

## 基线与边界

用户派发后读取 `AGENTS.md`、22H v2、22I 尺寸／同步要求和 H/I 共用契约。实际源码仓库位于 `/Users/davilje/fish-social`，聊天初始目录 `/Users/red/Desktop/fish` 是美术工作区。开始时已有 22H/I/J 方案、提示词、猫咪研究、计划表等未提交修改，本轮保留这些内容，仅回写 H 的实现信息。

现有 22B 水面求解器位于 `desktop-overlay/OverlayFishingWaterContact.cs`。22D/F 源运动采样、22E 入离席、认可的原猫咪 PNG 和源帧不重建。当前认可的发力片段把鱼保持在水面下（FishDepth 为 64 个原画单位），没有真实的破水／拍水动作。因此 FX04 的鱼身接触几何检查拒绝当前水下帧，FX05 也因没有真实源动作标记保持静默；当前片段不能为了展示效果制造触水。美术预览中的 FX04/05 是模拟候选，不代表现有猫咪动画已经具有该动作。

当前执行环境为 macOS，不能运行 WPF 窗口、Windows 原生 alpha 命中、系统回环音频或 Windows 主线程性能采样。交叉编译、可移植行为测试、浏览器模拟和 Windows 实测必须分别记录。

## 正式实现与文件清单

| 部分 | 主要文件及行为 |
|---|---|
| 资源与预览 | `scripts/art/build-water-effects-22h.cjs` 确定性生成源稿、PNG、manifest 和三份资源；`verify-water-effects-22h.cjs` 校验透明边、hash、镜像及真实浏览器控件；`verify-water-effects-bindings-22h.cjs` 核对正式配置与渲染器绑定。 |
| WPF | `OverlayWaterEffectsPolicy.cs`、`OverlayWaterEffects.cs`；`OverlayPetActor.cs` 在最终九层原图发布、22B 变换完成后消费源帧；`PondScenePresenter.cs` 提供真实水面锚点、岸线、相邻座位中线和视口裁剪，优先保留自己及最近三个可见角色。 |
| 预算与命中 | 固定 24 Image／8 组，全局单时钟、每角色至多 2 组，给自己保留 2组／12实例；低优先级淘汰而不排队。装饰 WPF 命中关闭，原生 alpha 判断仍只看既有角色原画。收尾／关闭／失去 owner 最多 100ms 收敛。 |
| 源动作 | `scripts/art/export-water-source-markers.py` 从真实源 PNG 导出 74 条鱼身边界；浮漂／窝料 marker 直接读取 H/I canonical `presentation-markers.json`，Unity 使用同源派生 metadata，不另设时间点。已移除四个不真实的破水／拍水候选标记。 |
| 共用事件 | `server/src/fishingPresentation.ts`、`shared/types.ts`、Unity `FishingPresentation.cs`、DTO/merge、`DesktopAppBootstrap.cs`、`NativeOverlayProcessController.cs`、Overlay IPC/MainWindow；稳定 session/action/event、revision、owner epoch、actor generation，静默快照、预测追认、取消、消费者独立去重。详见[共用实现记录](2026-10-24-water-effects-shared-events.md)。 |
| Unity 兜底 | `SpriteFramePetRenderer.cs`、`Pet/UnityFishingWaterMarkerBridge.cs`、`UnityFishingWaterEffects.cs`、`UnityWaterEffectBudget.cs`。真实 ContactProvider 存在才绘制，沿用相同资源和预算；当前 Home portrait 没有世界水面 provider，保持静默。当前远端世界 view 未绑定 Unity provider，多人正式显示由 WPF 承担。 |
| 设置 | `UI/DesktopFishingEffectsSettingsView.cs` 与既有设置页集成，持久化三档，监听全局减少动效；owner 切换在生命周期更新时立即发布静默基线。 |
| 行为与原生测试 | `WaterEffectsTests.cs`、`FishingPresentationSessionTests.cs`、`OverlayWaterEffectsNativeTests.cs`、Unity `DesktopFishingWaterEffectsTests.cs` 和服务端身份测试。 |

档位只从允许显示的米体长或 128 槽位可见鱼身范围决定，未知 S；浮漂、等待、窝料使用落水物自身规格。L/XL 双水冠用左右半图，水滴保持 64×64 原图尺寸。FX04 各档时长钳制为 300–600ms。没有根据隐藏未来鱼种或体长扩大效果。

## 已执行验证

所有数字分别保留口径，断言数不当作场景数或帧率成绩。

| 验证 | 结果与证据 |
|---|---|
| 水效纯 C# | **1180 条断言通过**，覆盖正式配置、四档边界、未知档、重复／过期、冷回调 token、取消、隐藏恢复、优先级／预留预算、700/720ms 限频、结束阶段禁发。[日志](../art/water-effects-22h/evidence/water-effects-tests.log)。0/150/400ms 是冷回调身份模拟，未代表实际 WPF 解码耗时。 |
| 共用身份纯 C# | **2090 条检查通过**，含 1000 个动作各 999 次循环的有界账本验证、预测／权威合并（包含首包已到waiting/hooked/effort的漏包追认）、双消费者、owner 和取消后下一轮恢复。[日志](../art/water-effects-22h/evidence/shared-session-tests.log)。 |
| 原有可移植回归 | WaterContact **9/9**、Motion **18/18**、Groundbait **10/10**、SeatMotion **8/8**，直接运行正式水面求解、源帧采样、打窝和座位代码。 |
| Python 源／资源保护 | **51/51 通过**，[日志](../art/water-effects-22h/evidence/python-source-tests.log)。开始时三处 hash 在基线 HEAD 即陈旧；本轮逐项审查并刷新 11 处受共享路由、原图发布或原有陈旧影响的源码快照，新增 `integration_review_22h_20261024` 记录。PNG／样式保护保持；UI 音频命令检查改为正式 `ui_audio` 白名单路径；行为覆盖另由上述测试承担。 |
| Web／资源 | **213 个组合通过**，11 角色×3槽位×3品质×2水面，加六类 A/B 与控件检查；0 页面异常／404，预览最大 13 实例／4 组；18张资源、配置和三份镜像一致。[报告](../art/water-effects-22h/verification.json)。这不是 20 人原生性能测试。 |
| 正式配置绑定 | 六类／七 marker（含 slap alias）一致，FX06/07/09 无绑定，PNG 真实宽高与渲染一致。[报告](../art/water-effects-22h/bindings-verification.json)。 |
| Server／共享 TS | 共享 `tsc` 通过；相关 **37/37**（H 身份2、睡眠28、已有 I 身份7）通过。[日志](../art/water-effects-22h/evidence/server-presentation-tests.log)。 |
| Unity 编辑器 | macOS 2021.3.29f1c1 相关 EditMode **208/208** 通过、进程退出0，含真实 root 销毁／重建30次断言；此前定向 **18/18** XML通过后退出发生 Mono 断言崩溃，保留异常日志，最终全套复测正常。各次执行分开存证，详见[共用记录](2026-10-24-water-effects-shared-events.md)。 |
| Windows WPF | 正式程序及 **33组** 原生测试交叉编译／win-x64自包含发布成功，新增H三组、另含I原图发布组；**未执行 Windows 原生测试**。 |

## 对照 spec 的验收范围

| spec 第7节 | 已有证据 | 剩余验收 |
|---|---|---|
| 猫咪风格／四档变体 | 18张原创图、11猫同屏明暗 A/B、SVG 与hash | 用户同屏视觉评审 |
| 六类触发／撤销结果特效 | 正式白名单、真实源marker、阶段禁发测试；无接触时FX04/05静默 | Windows真实动作录屏 |
| 档位／槽位／DPI／共用声档 | 可见身体与米体长临界测试、未知S、96/128/192 Web、共享信封 | Windows高DPI、I实录采样 |
| 落点／岸线／邻座／命中 | 22B回归、真实anchor及邻座裁剪代码、装饰不入alpha路径 | 全鱼塘／边缘座位原生画面和真实桌面点击 |
| 重复／重连／冷加载／生命周期 | policy/session/Unity行为测试；冷回调身份模拟 | 实际0/150/400ms解码录屏及Windows快速进出 |
| 多人／XL／24实例／8组／4MiB | 固定池、预算及预留断言，0.53125MiB真实解码，Web对照 | 20人原生A/B、30次进出、10分钟稳定性 |
| Web与Windows分别记录 | Web报告、交叉构建和原生待跑包 | Windows录屏／性能、至少30次H/I差值 |

## Windows 与 H/I 待执行验收

以下为待执行项目，不是实测结果，也没有把设计目标填写为测试成绩：

- Windows 原生 WPF 行为、96/128/192 角色槽位、100%/150%/200% DPI、明暗水面、边缘座位与桌面点击穿透。
- 20 角色同条件启用／关闭 A/B，预热后 30 次进出塘及持续 10 分钟，记录池容量、内存、线程、订阅返回基线；新增 H/I 主线程目标 p95≤1ms、p99≤2ms。
- 与正式 I 素材及仲裁器联测至少 30 个关键事件，60fps 画面与系统回环音频测 p50/p95/最大差值；目标≤80ms，>120ms 记录诊断。
- 猫咪同屏素材视觉评审。原创候选已接入代码不代表用户已验收素材。

## 联测接口与不做的结果特效

H 的 `presentation_marker` 信封携带会话、角色、动作／事件、owner epoch、actor generation、源 marker、occurrence 和当前可见身体决定的尺寸档。Unity `WaterMarkerReceived` 是交给 I 的语义接口；视觉消费、IPC relay 和未来音频消费使用不同消费位。H 不打开声音设备、不接收任意文件路径，也不根据快照序号或固定延时生成动作。

当前工作区的 I 适配已读取共享 session/action/event/ownerEpoch 和源 marker 可见档位，素材制作及 I 专项成绩由 I 独立报告。H 保留 `WaterMarkerReceived` 共用接口；未完成至少30次实机画声采样，因此不声称 H/I 音画已验收同步。

FX06/07/09 保持无资源／无订阅。`resolving`、`retrieve`、`reel`、`catch`、`escape`、`reset` 等阶段不新增 H 效果；既有效果在 100ms 内收敛。普通停钓不变成脱钩事件，真实结果沿用既有角色动作和读条管理。

## 可审查 Windows 包与复现

[Windows 自包含测试包](../../dist/overlay-water-effects-22h-2026-10-24.zip)，250,595,150 bytes（约239MiB），SHA-256：`77eec1edb202d2550c12f0964fe9228cc8954ddb4f0e69b04948b3aef10ae41b`。包含 WPF 正式程序、资源、33组原生测试、`Run-Native-Tests.cmd` 和逐源码／二进制hash `BUILD-INFO.json`；该包未替换用户安装、未部署服务器。实时联动仍须配套 Unity／server 共享协议变更，不能仅替换 Overlay 就声称全链路上线。构建日志见[存证](../art/water-effects-22h/evidence/windows-publish.log)。

从仓库根目录运行（需相应 Node、.NET8、Pillow/sharp/Playwright 环境）：

```sh
python3 scripts/art/export-water-source-markers.py
node scripts/art/build-water-effects-22h.cjs
node scripts/art/verify-water-effects-22h.cjs
node scripts/art/verify-water-effects-bindings-22h.cjs
dotnet run --project desktop-overlay/tests/FishSocialOverlay.WaterEffectsTests.csproj
dotnet run --project desktop-overlay/tests/FishSocialOverlay.PresentationSessionTests.csproj
python3 -m unittest discover -s desktop-overlay/tests -p 'test_*.py'
python3 scripts/desktop/package-water-effects-22h.py --dotnet dotnet
npm run planning:master-xlsx
```

服务端／Unity精确命令见[共用实现记录](2026-10-24-water-effects-shared-events.md)。预览可用 `python3 -m http.server 8774 --bind 127.0.0.1` 从根目录提供服务。

## 计划表同步

本轮只回写 STEAM-DESKTOP-22H 的交付说明、spec、CHANGELOG 和计划源脚本，并运行 `npm run planning:master-xlsx` 生成两份 xlsx／看板。状态保留“已确认”、完成日期留空：代码与资源已接入，本地验证已执行，Windows、用户视觉和 H/I 实机验收尚未完成；没有顺带标记 I/J 或把设计目标填成实测。
