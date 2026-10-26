# 开发提示词：橘猫钓鱼动画 Overlay 七层叠加（STEAM-DESKTOP-ART-04）

你是 Fish Social 的 **Unity 桌面 / Overlay 工程师**。只实现本编号。不要改 Node 钓鱼权威，不要把工作交给前端或后端 Agent。

## 必读

1. `docs/策划/规格/桌面端/橘猫钓鱼动画接入Overlay.md`（**已确认** / **STEAM-DESKTOP-ART-04**）
2. `docs/策划/规格/桌面端/Steam桌面Overlay分塘底图与HUD同步.md`（ART-03：无分层清单时的单张序列帧与回退）
3. `desktop-overlay/OverlayPetActor.cs`（`OverlayFrameCache`、`_image`、`_stage`、点击热区）
4. `fish-social-unity/Assets/Scripts/Desktop/Pet/PetStateController.cs`
5. 素材源（只读，不打进安装包）：桌面 `FishingAnimationAssets/output/animation-studio/` 的 `clips/*/layers/` 与 `animation.json`

## 顺序

1. 写导出脚本，把工作台图层拷到 `desktop-overlay/OverlayResources/fishing/`，并生成 `orange/manifest.json`。猫和爪进 `orange/body/`，竿、线、漂、鱼、饵进 `shared/`。运行时 clip 只用 `bait`、`cast`、`fishing`、`hooked`、`reel`、`catch`。`hooked` 由 bite 4 帧接 effort 6 帧，`loopStart` 为 4。逐帧 `layerOrder` 从 `animation.json` 抄入清单，不要手写。全透明层可以不落文件。
2. Overlay：有清单且该 clip 的猫层第 0 帧存在时，在现有宠物槽内用 7 个 `Image` 同帧叠加，按清单改 `ZIndex`。矩形按 640∶512 等比缩进 `actor-pet`，水平居中、底边对齐。昵称、咬钩环、状态图标仍盖在 7 层之上。图层不接收点击。
3. 播放：`bait` / `cast` / `reel` / `catch` 播完停在末帧；`fishing` 静止；`hooked` 前 4 帧一次，之后只循环后 6 帧。7 层共用一个帧号。解码缩到宠物槽宽度后冻结，同屏橘猫共用一份缓存。
4. Unity：`baiting` 映射为 `bait`，其余映射保持 spec 表格。Named Pipe 仍只传 `petVisualState`。
5. 没有清单、清单损坏，或不是橘猫：继续 ART-03 单张序列帧。`bait` 无清单时回退 `cast`。
6. 自检 spec §6。

## 验收

对照 spec §6。完成后按 Skill `planning-progress-sync` Checklist B：**须用户确认** 后再把 spec 改为已实现并刷新计划表。

- [ ] 抛竿前两帧竿在猫后，收杆时鱼和竿在猫后，举鱼时爪盖住鱼
- [ ] 咬钩不自动收杆；离开 `hooked` 才播收杆
- [ ] 其他猫种外观不变；热区、环和昵称层级不变
- [ ] 不改 `server/`、`shared/`、`mobile/`
- [ ] 安装包不含 GIF、工作台 HTML、`data.js`

## 禁止

- 运行时解析整份 `animation.json` 或按 `rig` 重新弯竿
- 第二 Unity Player、Pipe 传图或传帧序号
- 把画面画出宠物槽、改钓位间距
- 玩家可见的图层开关、自定义皮肤导入、按鱼种换 `fish` 层
- 主窗口重做 7 个 SpriteRenderer
- 用 `layered-12` 或 `big-fish-loop` 代替工作台的收杆和收获

## 派发

```text
@docs/策划/提示词/桌面端/steam-desktop-art-04-layered-fishing-dev.prompt.md 按此实现 STEAM-DESKTOP-ART-04
```
