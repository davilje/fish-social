# STEAM-DESKTOP-22H 水面资源

当前正式资源为用户已确认风格的[v4简约水魔法](magic-v4/README.md)，Overlay／Unity均已切换至v4。以下v2.1说明作为历史版本记录；原版仍可在预览中对照。

[打开猫咪同屏预览](index.html)。需从仓库根目录提供 HTTP 服务，例如 `python3 -m http.server 8774`，访问 `/docs/art/water-effects-22h/`。预览读取正式候选 PNG；角色直接引用现有 v19 分层文件，不复制猫咪资产。

本次交付 8 个水花、8 个水纹、2 个水滴，共 18 张独立 RGBA PNG。四档使用独立手绘曲线路径：S 为轻巧短瓣，M 为低圆冠，L 为左右扇形，XL 为宽厚翻水。A/B 改变高瓣方向、起落轮廓和尾纹。无霓虹、尖刺、粒子噪点、矩形底色；中心留空，为现有鱼身留出可见区域。

## 版本、来源、许可

- 正式候选资源：`desktop-overlay/OverlayResources/effects/fishing/v2/`，版本 `22h-water-v2.1`。
- Unity fallback 相同副本：`fish-social-unity/Assets/StreamingAssets/Effects/Fishing/v2/`。
- Unity 随包 native Overlay 相同副本：`fish-social-unity/FishSocialOverlay/OverlayResources/effects/fishing/v2/`。源动画接触配置 `source-water-markers.json` 同步至两端对应 fishing 目录；StreamingAssets 文件与目录附带确定性 GUID 的 `.meta`。
- 18 份可编辑原创矢量：本目录 `sources/`；由 `scripts/art/build-water-effects-22h.cjs` 确定性栅格化。
- 水花图案为本任务原创代码矢量绘图，未下载／使用第三方素材，按 **CC0-1.0** 交付。猫咪只作风格与空间参照，角色许可保持仓库原状，CC0 不扩及既有角色。
- `manifest.json` 逐项记录 PNG SHA-256、SVG SHA-256、宽高、解码体积，并记录 `effects.json` SHA-256。生成脚本会刷新副本与预览配置。
- 资源状态为 `production-candidate-awaiting-human-visual-review`：已供正式代码加载的可审查资产，不表示用户已视觉验收。候选版与日后认可版本通过版本及内容 hash 区分。

## 坐标及消费 schema

16 张水纹和水花 PNG 固定 **128 × 64**、RGBA，锚点 **[64, 44]**；2 张水滴 PNG 为 **64 × 64**、锚点 **[32, 44]**，实际水滴约 8–9px。全部内容至少留 8px 透明边。18 张解码像素共 **557,056 bytes / 0.53125 MiB**，低于 4MiB 限制。纹理按 URL / 路径缓存共享，不按每个水滴解码。

角色基准槽位为 128：绘图变换先将纹理的 metadata 锚点（水花/水纹 `(64,44)`、水滴 `(32,44)`）移到**真实水面接触点**，再乘 `slotSize / 128`。96 / 192 槽位分别乘 .75 / 1.5；DPI 由场景处理，不能再次乘进逻辑范围。水滴独立使用 64 × 64 的目标 Image，不能放进 128 × 64 目标区域拉伸。鱼档位内容本身已采用不同范围，不能再把整张 128px 画布缩成表中的 24 / 40 / 60 / 84px。这些宽度是可见水体的制作初值，非纹理矩形宽度。

`effects.json` schemaVersion 2：

| 字段 | 含义 |
|---|---|
| `logicalCanvas`, `anchor`, `logicalSlot` | `[128,64]`, `[64,44]`, `128` |
| `tiers.S/M/L/XL` | `width`, `height`, `durationMs`, `dropCount`, `arcCount`, `crownCount`, `spawnCount` |
| `cues[]` | `id`, `cue`, `anchor`, `sizeTier`, `durationMs`, `spawnCount`, `priority`, `reducedVariant`, `allowedPhases`, `cooldownMs` |
| `sizeTier` | `shared` 取 H/I 共用档；`object` 保持落水物自己的 S 规格 |
| cue 的 `durationMs/spawnCount = 0` | FX03/04/05 由当前 tier 的值提供；不是禁播标志 |
| `FX05.markers` | 仅 `fish.surface` / `fish.slap`，另须 `requiresSourceMarker` 与 `requiresContact` |
| `assets[]` | `id`, `file`, `kind`, `sizeTier`, `variant`, `layer`, `anchor`, `width`, `height` |
| `assets[].id` | `ripple-{s,m,l,xl}-{a,b}`、`splash-{s,m,l,xl}-{a,b}`、`drop-round` / `drop-short` |
| `reduced`, `budget`, `endedFadeMs` | 200ms 的 1–2 弧，无水滴；24实例 / 8组上限及自己的预留；100ms结束收敛 |

L / XL 水花使用同一张图的左右两半分别绘制，各计 1 实例；每道完整纹理水纹和每个水滴也分别计数。S / M 水花计 1 实例。水纹一张中的手绘断弧是一张已解码纹理，按一个实际 Image 计；不能把多张独立 Image 合并成一个组计数。最大标准 XL：6 滴 + 2 侧水瓣 + 2 纹理水纹 = **10 个实际绘制实例**。

v4依用户追加动效要求恢复FX09真实收杆触水（非retrieve.start）；FX06 / FX07继续没有绑定，不产生结果装饰。v2为历史基线，仍保留旧撤销列表。正式发射、去重、源标记、owner、取消以及裁剪由接入代码实施；预览页的选择器是审查工具。

## 预览与验证

预览支持暂停、前后 1/60 秒逐帧、时间轴、11 种现有猫咪、浅 / 深水、96 / 128 / 192 槽位、七类效果、A / B 变体与标准 / 简化 / 关闭。四档并排、20 座位预算场景及透明纹理原图同时展示。接触 X 来自实际入水源帧的 bobber / fish / bait 图层及22B变换；发射后固定于该水面落点，抛竿／撒饵触水前不发射。咬钩与水下挣扎只有波纹；完整波纹在水线下，飞溅水滴／水冠可以越过水线。不存在实际触水层时静默。

FX05 的“鱼出水尺寸样张”用正式reel轨迹对照S/M/L/XL；实际运行以已显示鱼体尺寸取档。bite/effort保持水下，不生成水花；reel source0候选约67ms真实跨线才发射，普通快照／硬取消不补播。

复现（本机需使 `sharp` 与 `playwright` 可解析；可设置 `NODE_PATH` 为工作区运行时的 node_modules）：

```sh
node scripts/art/build-water-effects-22h.cjs
node scripts/art/verify-water-effects-22h.cjs
node scripts/art/verify-water-effects-bindings-22h.cjs
```

验证脚本自行启动临时本地 HTTP 服务，关闭后不留下监听。使用已有 Chrome（或 `CHROME_EXECUTABLE`）；验证 PNG / SVG / 配置 hash、两端副本、18 个唯一内容、RGBA / 留边 / 解码预算；覆盖 11 猫 × 3 槽位 × 3 品质 × 2 水面，另遍历六 cue 与 A/B、暂停 / 逐帧 / 时间轴 / 播放。

资源与 Web 结果保存在 [verification.json](verification.json)；[bindings-verification.json](bindings-verification.json) 对实际 Overlay policy 与 renderer 的六 cue / 七 marker 绑定（含 fish.slap alias）、阶段白名单、撤销事件、64px 水滴尺寸和锚点做源码 / 配置一致性验证，不替代真实运行时测试。截图为 [浅水标准](preview-light.png)、[深水 XL / 192](preview-dark-xl.png)、[深水简化 / 96](preview-reduced.png)。这里的 20 人计数属于 Web 预算演示；Windows 透明命中、真实冷加载、主线程 A/B 分位、10分钟长稳、音画同步仍由正式接入专项验证提供证据，不能由浏览器截图或编译通过推定。

## 日系 RPG 水魔法候选（用户反馈迭代）

2026-10-24 新增[水魔法同屏预览](index.html?style=magic)与[AI 生成透明图集／提示词／来源说明](../../archive/2026-10-26/superseded/art/water-effects-22h/magic-v3/README.md)：青蓝流光、卷曲水冠、晶莹高光，4档各2水冠与2水纹。可随时切回原版；该候选仅进入预览，未替换下文 v2 正式包资源。106个新候选浏览器组合和透明alpha检查通过，见[报告](../../archive/2026-10-26/superseded/art/water-effects-22h/magic-v3/verification.json)。

2026-10-24 再次按用户“这个风格过头了，贴合游戏本身的细节量”反馈收敛为[v4简约水魔法](magic-v4/README.md)。同屏入口已改用v4：低饱和湖蓝、两三块颜色、少量高光与水滴，移除v3发光和闪光细节。v3文件留作过程参考。

2026-10-24 用户确认“该风格可以”，v4已整理为正式18纹理并接入两端；详见[认可风格的正式接入与验证](../../engineering/water-effects/2026-10-24-water-effects-22h-v4.md)。
