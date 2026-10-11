# 橘猫钓鱼动画接入 Overlay

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | 橘猫钓鱼动画接入 Overlay |
| 编号 | **STEAM-DESKTOP-ART-04** |
| 类型 | Unity 桌面 / Overlay 表现 |
| 负责人 | Unity 桌面 / Overlay |
| 状态 | **已确认** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P1 |
| 设计时间 | **2026-10-11** |
| 来源 | 桌面归档 `FishingAnimationAssets`（工作台 v7，33 帧 / 8 状态 / 7 层） |
| 上位需求 | `STEAM-DESKTOP-07`、`STEAM-DESKTOP-07G`、`STEAM-DESKTOP-ART-03` |
| 关联全景 | 桌面宠物表现层；钓鱼阶段仍以服务端 `FishingPhase` 为准 |

---

## 1. 背景与目标

### 1.1 背景

桌面归档 `FishingAnimationAssets` 是离线烘焙的帧动画。Python 预先画出每一层的透明 PNG，浏览器工作台用 7 个画布按该帧的 `layerOrder` 叠加。弯曲、位移和遮挡已经在 PNG 里，运行时不再做骨骼或物理。

工作台画布 640×512，状态顺序 `ready → bait → cast → wait → bite → effort → retrieve → catch`，共 33 帧。图层固定 7 个：`cat`、`rod`、`hands`、`line`、`bobber`、`fish`、`bait`。同一帧的 7 张图同尺寸、同原点，只改绘制顺序，不单独平移。

遮挡不能烘焙成一张图后再拆开。归档里实际出现四种顺序：

| 动作 | 从后到前 |
|------|----------|
| 默认（挂饵、等待、咬钩、发力，以及抛竿后两帧） | `cat → rod → hands → line → bobber → fish → bait` |
| 抛竿前两帧（竿在头后） | `rod → cat → hands → line → bobber → fish → bait` |
| 收杆整段，以及收获第 1 帧 | `rod → line → fish → cat → hands → bobber → bait` |
| 收获第 2–7 帧（爪子抓住鱼） | `rod → line → cat → fish → hands → bobber → bait` |

`hands` 是从当前猫帧抠出的前景爪，用来盖住竿或鱼。猫的底图仍是完整的，关掉竿不会在身上留洞。没有物体的层是全透明图。

当前 Overlay 只有一张 `Image`（`OverlayPetActor._image`）。它做不到「竿在猫后、爪在竿前、鱼在爪下」这种逐帧换序。主窗口若仍用单张精灵，和 Overlay 会不一致；本需求的分层播放以 Overlay 为准。

玩家看见的猫在 WPF Overlay，不在 Unity 场景里。Unity 只把服务端阶段映射成 `petVisualState`，经 Named Pipe 下发。Overlay 不接收图片，也不自己推断钓鱼规则。

### 1.2 目标

- 橘猫在 Overlay 上按服务端阶段播放工作台动作，并且 **7 层同帧叠加**，每帧使用归档里的 `layerOrder`。
- 咬钩先播后仰 4 帧，再只循环发力 6 帧，直到阶段离开 `hooked`。
- 收杆、展示收获各播一遍并停在末帧。
- 竿、线、漂、鱼、饵与猫、爪分目录存放。以后只替换猫和爪，不必重导钓具。
- 没有分层资源的猫种继续走 ART-03 的单张序列帧。
- 服务端阶段、时长和鱼获结算不变。

### 1.3 非目标

- 不把 `build_*.py`、浏览器工作台、`data.js` 放进安装包。运行时不解析整份 `animation.json`，不读 `rig` 去重新弯竿。
- 不把 7 层画到宠物槽外面，不改钓位间距、昵称、咬钩环和点击热区。640×512 整幅仍缩进现有 `actor-pet` 槽。
- 不做玩家可见的图层开关。那是工作台调试功能。
- 不做自定义皮肤导入界面。目录按「猫爪 / 钓具」分开，便于以后换猫，本需求只交付橘猫。
- 不按鱼种替换 `fish` 层。该层独立，是为了遮挡正确，不是为了换成背包里的鱼。
- 不把工作台的 silver / brown / cream / blue 当成 calico、gray、siamese、tuxedo、white。
- 不做打窝、拖动、离线的分层动作。`groundbaiting` 仍用 `sit`，`dragging` / `offline` 仍回退 `idle`。`ready` 不接入。
- 不补鱼摆尾、脱钩物理、鱼竿实时曲线。
- 不改 `FishingPhase`、Socket 协议。Named Pipe 仍只传 `petVisualState` 字符串，不传帧序号和 PNG。
- 不改移动端页面。Unity 主窗口里的宠物和 Overlay 使用同一套分层清单。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 橘猫玩家 | 挂饵、抛竿、等待、咬钩、收杆、展示鱼获 | 爪盖住竿；抛竿后引时竿在身体后面；收杆时鱼和竿在猫后面；举鱼时爪盖住鱼 |
| 同塘其他人 | 看这只橘猫 | 同一套本地分层图，由对方的 `petVisualState` 驱动 |
| 其他猫种 | 同样的钓鱼流程 | 仍是单张序列帧，外观与现在一致 |
| 美术 | 以后重导某一层 | 只替换对应 PNG 和清单里的顺序，不改阶段协议 |

---

## 3. 功能范围

### 3.1 功能列表

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 分层资源目录 + 短清单 | P0 | 从工作台 `layers/` 拷贝，不使用合成帧、GIF、图集 |
| 2 | Overlay 七层同槽叠加 | P0 | 固定 7 个 `Image`，按帧改 `ZIndex` 和贴图 |
| 3 | 阶段到 clip 的映射 | P0 | Unity 只多发一个 `bait`；Overlay 不读 `fishingPhase` 决定动画 |
| 4 | `hooked` 入场一次 + 发力循环 | P0 | 前 4 帧播完后只循环后 6 帧，7 层使用同一个帧号 |
| 5 | 单次动作停在末帧 | P0 | `bait`、`cast`、`reel`、`catch` |
| 6 | 无分层资源时回退单张图 | P0 | 现有 `pets/<petId>/<clip>/N.png` 保持可用 |
| 7 | Unity 场景内宠物七层 | P0 | 主窗口 HomePet / HeaderPet 与 Overlay 同一清单、同一播放规则 |

### 3.2 交互与 UI

玩家没有新按钮。钓鱼操作仍走现有 Overlay 菜单。

```text
seated / groundbaiting     → sit      单张图，不走分层
baiting                    → bait     4 帧后停住
casting                    → cast     4 帧后停住；前两帧竿在猫后
waiting                    → fishing  1 帧
hooked                     → hooked   先 bite 4 帧，再循环 effort 6 帧
resolving / stopping       → reel     6 帧后停住；竿、线、鱼都在猫后
seated 且 hasPendingCatch  → catch    7 帧后停住；第 1 帧鱼在猫后，其后鱼在猫与爪之间
idle / disconnected        → idle     单张图
```

`baiting` 和 `casting` 必须分成两个 clip。现在两者都映射成 `cast`，一切阶段就会把动画从头播。

`hooked` 的时长由服务端阶段决定。进入 `hooked` 开始入场，离开后才收杆。Overlay 不用 `hookDeadlineMs` 切动画。圆环、昵称、状态图标仍画在整只宠物之上，不插入 7 层之间。

节拍沿用 125 ms。不引入逐帧 `durationMs`。等待只有 1 帧，定时器不必转。

点击热区仍是现有的 `actor-hit`（没有则用 `actor-pet`）。7 张图层 `IsHitTestVisible = false`，不按透明像素做命中，也不扩大热区。

### 3.3 阶段与素材

不改 `shared/`。

| 服务端阶段 | clip | 帧来源 | 播放 |
|------------|------|--------|------|
| `baiting` | `bait` | `clips/bait` 4 帧 | 一次，停末帧 |
| `casting` | `cast` | `clips/cast` 4 帧 | 一次，停末帧 |
| `waiting` | `fishing` | `clips/wait` 1 帧 | 静止 |
| `hooked` | `hooked` | `bite` 4 帧接 `effort` 6 帧，共 10 帧 | 索引 `< 4` 递增；之后只在 `4…9` 循环 |
| `resolving`、`stopping` | `reel` | `clips/retrieve` 6 帧 | 一次，停末帧 |
| `seated` 且有待领鱼获 | `catch` | `clips/catch` 7 帧 | 一次，停末帧 |

`hooked` 不足 10 帧时整段循环，避免缺图卡死。其他猫种没有分层清单时，行为与 ART-03 相同。

---

## 4. 分层方案

### 4.1 画什么

每个橘猫演员在现有 `_stage` 里增加一个层容器，里面固定 7 个 `Image`，和现在的单张 `_image` 互斥：

```text
_stage                          尺寸仍是 actor-pet（布局 128×128）
├── _placeholder                没图时的色块
├── _image                      无分层清单时使用，行为不变
└── _layerHost                  有清单时显示，ClipToBounds = true
    ├── cat
    ├── rod
    ├── hands
    ├── line
    ├── bobber
    ├── fish
    └── bait
```

7 个 `Image` 使用同一块矩形：在宠物槽内按 640∶512 等比缩放，水平居中，底边对齐槽底。`Stretch` 用填满这块矩形，因为解码尺寸已经是这个比例。这样 7 层的像素仍然对齐，脚底不会在换层时跳动。

层容器放在 `_stage` 内。昵称、气泡、咬钩环、状态图标保持现在的更高 `ZIndex`，整组盖在 7 层之上。

从后到前：清单里 `layerOrder` 的第 0 个 `ZIndex` 最小，最后一个最大。换帧时先改顺序，再换 7 张贴图。不允许猫走第 3 帧、鱼还停在第 2 帧。

某一层文件不存在：该层清空，其他层照常画。这和「全透明占位」等价。猫底图是完整的，清空 `rod` 或 `fish` 不会在身上挖洞。

### 4.2 资源放哪

仓库源目录（构建再拷到 exe 旁）：

```text
desktop-overlay/OverlayResources/fishing/orange/manifest.json
desktop-overlay/OverlayResources/fishing/orange/body/<clip>/<index>/cat.png
desktop-overlay/OverlayResources/fishing/orange/body/<clip>/<index>/hands.png
desktop-overlay/OverlayResources/fishing/shared/<clip>/<index>/rod.png
desktop-overlay/OverlayResources/fishing/shared/<clip>/<index>/line.png
desktop-overlay/OverlayResources/fishing/shared/<clip>/<index>/bobber.png
desktop-overlay/OverlayResources/fishing/shared/<clip>/<index>/fish.png
desktop-overlay/OverlayResources/fishing/shared/<clip>/<index>/bait.png
```

`<clip>` 只用运行时名字：`bait`、`cast`、`fishing`、`hooked`、`reel`、`catch`。`hooked/0`…`3` 来自工作台 `bite`，`hooked/4`…`9` 来自 `effort`。`fishing` 来自 `wait`，`reel` 来自 `retrieve`。

全透明的层可以不写文件。清单里的帧数是准的，加载器不能因为某层缺文件就停在前一帧。

`pets/<petId>/<clip>/N.png` 保留给没有分层清单的猫，以及橘猫的 `idle` / `sit`。不要把合成帧再导出到这个目录冒充分层结果。

导出是拷贝加写清单，不重跑 macOS 上的 Python 构建。源图用归档里的 `output/animation-studio/clips/<phase>/layers/`。

### 4.3 清单

只放播放需要的字段。每个 clip 一份帧序列，每帧 7 个层名的顺序。`hooked` 用 `loopStart` 表示从哪一帧开始循环。

```json
{
  "version": 1,
  "canvas": [640, 512],
  "layers": ["cat", "rod", "hands", "line", "bobber", "fish", "bait"],
  "bodyLayers": ["cat", "hands"],
  "clips": {
    "bait": {
      "playback": "once",
      "frames": [
        ["cat", "rod", "hands", "line", "bobber", "fish", "bait"]
      ]
    },
    "cast": {
      "playback": "once",
      "frames": [
        ["rod", "cat", "hands", "line", "bobber", "fish", "bait"],
        ["rod", "cat", "hands", "line", "bobber", "fish", "bait"],
        ["cat", "rod", "hands", "line", "bobber", "fish", "bait"],
        ["cat", "rod", "hands", "line", "bobber", "fish", "bait"]
      ]
    },
    "fishing": {
      "playback": "hold",
      "frames": [
        ["cat", "rod", "hands", "line", "bobber", "fish", "bait"]
      ]
    },
    "hooked": {
      "playback": "intro-loop",
      "loopStart": 4,
      "frames": []
    },
    "reel": {
      "playback": "once",
      "frames": []
    },
    "catch": {
      "playback": "once",
      "frames": []
    }
  }
}
```

`frames` 的长度就是帧数。上面 `hooked`、`reel`、`catch` 的空数组只是示例省略；落地文件必须写全每一帧的顺序。`reel` 的 6 帧都是 `rod → line → fish → cat → hands → bobber → bait`。`catch` 第 1 帧与收杆相同，第 2–7 帧是 `rod → line → cat → fish → hands → bobber → bait`。

清单缺字段、层名不在 7 个之内、或 `loopStart` 越界：这只猫的该 clip 回退单张图，不抛到界面上。

### 4.4 内存

7 层 × 33 帧若按 640×512 解码，全局大约 280MB，同塘多只橘猫也不能各备一份。

加载时用同一解码尺寸缩小后再 `Freeze()`，放进进程内一份缓存，键为 `clip + 帧号 + 层名`。解码宽度取宠物槽宽度（布局 128，即 128×102 左右），7 层必须用同一宽高，否则会对不齐。全部钓鱼层加在一起大约十几 MB。槽尺寸变化时丢掉缓存重解。

只保留当前 clip。没有任何演员还在用的 clip 可以释放。切换 `petVisualState` 时 7 层一起换，不逐张异步补洞导致闪一层。

### 4.5 和现有单张播帧的关系

```text
有 fishing/orange/manifest.json
  且该 clip 在清单里
  且至少猫层能读到第 0 帧
        → 七层播放，藏起 _image

否则
        → 现有 OverlayFrameCache
           pets/<petId>/<clip>/N.png
           以及 idle / fishing 回退
```

`bait` 这个新字符串在没有清单时回退到 `cast`，再按现有规则回退，避免橘猫资源还没拷齐时变成空白。

---

## 5. 技术影响

### 5.1 数据模型

无新表、无新字段。清单是 Overlay 本地文件。

### 5.2 API / Socket

无。`petVisualState` 增加 `bait`。其余仍是 `cast`、`fishing`、`hooked`、`reel`、`catch`、`sit`、`idle`。

### 5.3 涉及文件（预估）

- `desktop-overlay/OverlayResources/fishing/`：PNG 与 `manifest.json`
- `desktop-overlay/OverlayPetActor.cs`：层容器、同帧换图、`ZIndex`、`hooked` 循环起点、`bait` 的 one-shot
- `fish-social-unity/Assets/Scripts/Desktop/Pet/PetStateController.cs`：`baiting` → `bait`
- 拷贝清单的脚本可放 `scripts/` 或 `tools/`，不进服务端，也不进玩家菜单

不改 `server/`、`shared/`、`mobile/`。不改 `FishSocialOverlay/OverlayResources` 作为源；那是打包副本。

---

## 6. 验收标准

- [ ] 橘猫挂饵 4 帧后停住；进入抛竿后从头播放，不把挂饵再播一遍。
- [ ] 抛竿前两帧竿在猫身后，爪仍在猫身前；后两帧竿回到猫身前、仍在爪后面。
- [ ] 等待保持一帧，漂和线可见（缩在宠物槽内即可）。
- [ ] 咬钩先播完后仰 4 帧，之后只循环发力 6 帧；7 层帧号始终相同。阶段停在 `hooked` 时不自动收杆。
- [ ] 收杆 6 帧中，竿、线和鱼都在猫后面，爪在猫前面，停在末帧。
- [ ] 展示收获第 1 帧鱼在猫后；第 2 帧起鱼在猫与爪之间，线、漂、饵为空也不出现实色块；停在最后一帧。
- [ ] 去掉或隐藏 `rod` 时，猫身上没有破洞。
- [ ] 昵称、咬钩环、状态图标盖住钓鱼图层，不被鱼或竿插到中间。
- [ ] 点击区域与改动前一致，点在邻座热区不会点到这只猫伸出来的透明层。
- [ ] calico 等五只猫仍是单张序列帧，回退规则不变。
- [ ] 清单缺失或损坏时，橘猫回退单张图，Overlay 不崩溃。
- [ ] IPC 状态里没有图片路径和帧序号。同塘另一只橘猫只跟对方的 `petVisualState` 走。
- [ ] 安装包里没有 GIF、`data.js` 和工作台 HTML。同屏多只橘猫只保留一份解码缓存。

---

## 7. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 缩进 128 槽后，越顶抛鱼仍然看不清 | 分层解决遮挡，不解决尺寸。放大特写另开需求 |
| 7 层各缩一次，边缘和合成后再缩小略有差别 | 接受。7 层用同一解码尺寸，保证彼此对齐 |
| 换帧时某一层解码慢，闪一下 | 进入 clip 时 7 层当前帧一起准备好再显示 |
| 导出漏了某一帧的 `layerOrder` | 清单由脚本从 `animation.json` 抄顺序，不手写 |
| 主窗口仍是单张图，和 Overlay 遮挡不一致 | 本需求以 Overlay 为准；主窗口不重做 |

依赖：`FishingAnimationAssets/output/animation-studio/clips/*/layers/` 和同目录 `animation.json`。不要用 `layered-12` 或 `big-fish-loop` 顶替工作台的收杆和收获。

---

## 8. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-10-11 | 策划 | 草案：工作台合成帧接入现有单张序列帧 |
| 2026-10-11 | 策划 | 改为 Overlay 七层同帧叠加；合成帧不再作为运行时画面 |
| 2026-10-11 | 策划 | 用户确认方案，状态改为已确认 |
| 2026-10-11 | 桌面端 | 按用户要求把同一套七层播放同步到 Unity 主窗口宠物 |
