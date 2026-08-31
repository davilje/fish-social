# Steam 桌面 Overlay：右键切换窗口显示范围

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 产品右键菜单切换窗口视口：裁切显示范围，不按比例缩放内容 |
| 编号 | **STEAM-DESKTOP-16** |
| 类型 | **功能** |
| 负责人 | Overlay / Unity 桌面工程师（几乎只改 WPF Overlay；Unity 启动参数保持默认即可） |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop / hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-08-31** |
| 完成时间 | **2026-08-31** |
| 上位需求 | `STEAM-DESKTOP-09D`、`STEAM-DESKTOP-07D`、`STEAM-DESKTOP-13A`、`STEAM-DESKTOP-14B`、`STEAM-DESKTOP-ART-03` |
| 关联全景 | Overlay 窗口与产品右键菜单 |

---

## 1. 背景与目标

### 1.1 背景

Overlay 窗口与 HUD 设计画布锁死 **960×560**（09D / ART-03）。宽塘靠 14B 在视口内横移，不能改窗口本身。挂机时玩家希望把 Overlay **变小**，少挡桌面。

「变小」**不是**把整窗连同猫、按钮、字一起 `Scale`/`Viewbox` 缩小（那样 64×64 猫和 10px 字会糊、会按比例变矮）。而是 **改显示范围**：窗外的塘顶/左右被裁掉，窗内像素仍 1:1。

### 1.2 目标

- 场景空白处 **产品右键菜单** 增加「窗口大小」，三档循环或点选：
  - **标准** `960×560`
  - **中** `800×400`
  - **小** `600×300`
- **高度只切顶部**：窗口底边在屏幕上不动；少掉的高度从塘顶（天空）裁掉。
- **宽度往里收**：窗口左右往中收；HUD 左/中/右锚点跟着新宽度重摆，不能把聊天栏、钓鱼钮、右侧菜单裁出窗外。
- 猫仍 **64×64**，按钮与字号不缩放。

### 1.3 非目标

- 不用 `Viewbox` / `LayoutTransform` / `RenderTransform.Scale` 做整窗缩放。
- 不做任意像素拖拽改大小、不做滚轮缩放、不做竖向平移场景。
- 不改 Unity HUD Prefab 为多套画布；**不**再导出 800/600 的 `overlay-hud.json`。
- 不改 `server/` / `shared/` / `mobile/`；Named Pipe 不传图。
- 不改 09A 玩家社交右键（本项只挂 **场景产品菜单**）。
- 不改 14B 场景内容宽度（1920 仍是塘宽，只是视口变窄）。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 玩家 | 右键塘景空白 | 菜单有「窗口大小」→ 标准 / 中 / 小，当前档打勾 |
| 玩家 | 选「中」或「小」 | 窗口变矮变窄；底边不动；顶边下移；左右往中收；底栏与左右 HUD 仍完整可见 |
| 玩家 | 宽塘 + 小窗 | 左右平移箭头仍在；可平移范围按 **新视口宽** 钳制 |
| 玩家 | 切回标准 | 恢复 960×560；底边仍对齐切换前的底边 |
| 玩家 | 右键他人猫 | 仍是 09A 社交菜单，无窗口大小 |

---

## 3. 功能范围

### 3.1 功能列表

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 产品菜单入口 | P0 | `BuildProductContextMenu` 增加「窗口大小」子菜单 |
| 2 | 改窗口客户区 | P0 | `Width`/`Height` 切到预设；禁止 Scale |
| 3 | 屏上锚点 | P0 | `Top += oldH - newH`；`Left += (oldW - newW) / 2` |
| 4 | 场景裁切 | P0 | 视口裁塘：竖向露底 `viewH`，横向视口宽 `viewW` |
| 5 | HUD 重锚 | P0 | 设计坐标 960×560 → 映射到 `viewW×viewH`，见 §3.3 |
| 6 | 13A / 14B / 穿透 | P0 | 渐隐、平移钳制、命中按 **当前视口** |
| 7 | 本地记忆 | P1 | 下次启动 Overlay 恢复上次档位（默认标准） |

### 3.2 交互

```text
产品右键（空白/可见塘图，非他人猫）
  └─ …现有项…
  └─ 窗口大小 ▶
        ├─ 标准 960×560
        ├─ 中   800×400
        └─ 小   600×300
  └─ 隐藏到托盘 / 退出
```

切换后菜单关掉。拖窗口规则不变（点可见塘图 `DragMove`）。

### 3.3 HUD 锚点（设计画布 → 视口）

`overlay-hud.json` **仍必须 960×560**。运行时把每个 **根控件**（无 parent 或 group 根）映到视口：

记设计矩形 `(x, y, w, h)`，设计底边距 `b = 560 - y - h`，右边距 `r = 960 - x - w`。

| 控件 | 水平 | 垂直 | 视口中的位置 |
|------|------|------|----------------|
| `dock_chat` | 左 | 底 | `x'=x`，`y'=viewH-b-h`；必要时减 `w`（九宫格） |
| `dock_fishing` | 中 | 底 | `x'=(viewW-w)/2`，`y'=viewH-b-h` |
| `txt_error` | 中 | 底 | 与钓鱼栏同中、同底边距逻辑 |
| `cap_status` | 右 | 底 | `x'=viewW-r-w`，`y'=viewH-b-h` |
| `menu_rail` | 右 | 底 | 同上（轨从底往上长） |
| `btn_pan_left` | 左 | 视口垂直居中 | `x'=x`，`y'=(viewH-h)/2` |
| `btn_pan_right` | 右 | 视口垂直居中 | `x'=viewW-r-w`，`y'=(viewH-h)/2` |
| Debug 钮 | 右 | 底（相对 cap） | 保持在状态胶囊上方、右边距不变 |
| 子控件 | 相对父 | 相对父 | `dock_chat` 内部仍用 JSON 相对坐标；父宽变则九宫格重绘 |

**碰撞（小窗）：** `600` 宽时左聊天 + 中钓鱼 + 右状态按原宽会重叠。

1. 先按锚点摆；
2. 若 `dock_chat` 右缘与 `dock_fishing` 左缘间距 &lt; 8px：缩小聊天底图宽度（已有 `spriteSlice`），内部 preview/log 跟着收；
3. 仍不够：略减左右边距（最低左 8 / 右 8），**不缩小按钮字号、不缩猫**。

`menu_rail` 设计高 370，小窗高 300 时顶部可被窗口裁切；**默认收起**，只留靠底的 toggle / 打开主界面 / 退出，避免点不到。

### 3.4 场景裁切

塘布局高度仍 560（或塘 JSON `canvas.height`）。视口：

```text
visibleSceneY = sceneHeight - viewH     // 只切顶
visibleSceneX = currentPanOffsetX       // 14B；钳制 [viewW - sceneW, 0]
```

实现建议：外层 `ScenePanViewport`（或等价）设为 `viewW×viewH` 且 `ClipToBounds`；`SceneContentCanvas` 保持场景像素尺寸，`TranslateTransform`：

- `Y = -(sceneHeight - viewH)`（顶被裁）
- `X = panOffset`（14B，把常量 `ViewportWidth=960` 改成当前 `viewW`）

猫、座位、底图 **不缩放**。窄塘 `sceneW ≤ viewW`：无箭头、不横移。

### 3.5 规则

- 三档写死，不允许中间值。
- 切档时底边屏幕 Y 不变；宽以窗口中线为轴向内收。超出工作区则钳到可见（底边优先保住）。
- Unity `--width/--height` 仍默认 960×560；**启动后 Overlay 自己改尺寸**，不必重启进程。
- 点击穿透、13A 40px 渐隐按 **当前** `viewW×viewH` 重建，不跟场景 1920 宽走。

---

## 4. 实现分析（给开发）

### 4.1 为什么不能整窗 Scale

| 做法 | 结果 | 结论 |
|------|------|------|
| `Viewbox` / `ScaleTransform` | 960×560 画进 600×300，猫约 40px，字发糊 | **禁止** |
| 只改 `Window.Width/Height` 不改布局 | 右侧/底栏被 WPF 裁掉 | 宽度必须重锚 HUD |
| 场景层负 `Canvas.Top` 切顶 | 底边对齐、天空消失，猫尺寸不变 | **高度用这个** |
| HUD 跟场景一起负 Top | 底栏还在，但右轨/左聊天在变窄后出界 | HUD **单独**按 §3.3 算 |

结论：**两层**——场景 = 裁切视口；HUD = 锚点重摆。设计源仍一份 960×560 JSON。

### 4.2 建议改动点（不扩需求）

| 模块 | 现状 | 改法 |
|------|------|------|
| `MainWindow.xaml` | 窗与多个 Canvas 写死 960×560 | 视口宿主跟窗口走；场景内容 Canvas 仍按塘宽×塘高 |
| `MainWindow.xaml.cs` | 产品菜单无尺寸项；启动读 `--width/--height` | 子菜单切档；`ApplyViewportSize`；P1 本地档 |
| `OverlayHudLayout.cs` | `CanvasWidth/Height` 常量校验 JSON | JSON 仍校验 960×560；`TryApply`/`Relayout` 吃当前视口 |
| `OverlayScenePan.cs` | `ViewportWidth = 960` | 改为当前视口宽，重算钳制与箭头显隐 |
| `OverlayEdgeVignette` | `ApplySize(960,560)` | 切档后 `ApplySize(viewW, viewH)` |
| `OverlayClickThrough` | 已用 `ActualWidth/Height` | 切档后应自动对；回归点透明穿桌面 |
| `OverlayPondLayout` | `ViewportWidth/Height` 常量 | 视口与塘画布分离：塘可 1920×560，视口可变 |
| Unity | 导出 HUD 必须 960×560 | **不改**导出器；不要做三套 Prefab |

不需要新 IPC。主窗口页签与 Overlay 尺寸无关。

### 4.3 数据流

```text
右键选档
  → Overlay 改 Window + Top/Left
  → 场景 TranslateY 切顶 + 14B 按新 viewW 钳制 X
  → HUD Relayout 锚点
  → 13A ApplySize
  → （P1）写入本地 viewport-preset
无 Unity / Socket / 服务端
```

### 4.4 风险

| 风险 | 缓解 |
|------|------|
| 小窗底栏三块重叠 | §3.3 先缩聊天宽 |
| 菜单轨高于 300 | 小档强制收起 |
| 切档后自己座位出视口 | 按 14B：尽量把 `ownSpot` 横移进新视口 |
| 误用 Scale 图省事 | 验收：量猫 64px、按钮 70×32 |
| 多屏 / 底边出屏 | 钳制工作区，优先保底边 |

回滚：去掉菜单项，视口锁回 960×560。

---

## 5. 技术影响

### 5.1 数据模型

可选 P1：`%LocalAppData%/FishSocial/overlay-viewport.json` 或 Overlay 目录旁 settings，仅 `{ "preset": "960x560" | "800x400" | "600x300" }`。

### 5.2 API / Socket

无。

### 5.3 涉及文件（预估）

- `desktop-overlay/MainWindow.xaml`、`MainWindow.xaml.cs`
- `desktop-overlay/OverlayHudLayout.cs`
- `desktop-overlay/OverlayScenePan.cs`
- `desktop-overlay/OverlayEdgeVignette.cs`
- `desktop-overlay/OverlayPondLayout.cs`（视口常量与场景尺寸分离）
- 不改 `mobile/`、`server/`、`shared/`；不改 Unity HUD 导出器

---

## 6. 验收标准

- [x] 产品右键有「窗口大小」三档；玩家右键菜单没有此项
- [x] 三档客户区分别为 960×560、800×400、600×300（允许 ±1px 系统边框误差）
- [x] 切到更矮：窗口底边屏幕坐标不变（±2px）；少的高度是塘顶被裁，不是上下对缩
- [x] 切到更窄：窗口中线大致不动；聊天、钓鱼栏、状态、右轨仍在窗内，不被裁没
- [x] 猫显示仍 64×64；「开始钓鱼」等按钮逻辑尺寸不按比例缩小
- [x] 未使用整窗 Scale/Viewbox（抽查视觉：字号与 960 档一致）
- [x] 宽塘在 800/600 仍可左右平移；钳制按新视口宽；13A 四边仍约 40px
- [x] 透明区点击穿透仍成立（14）
- [x] 不改 `mobile/` / 打窝与钓鱼权威

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-31 | 策划 | 已确认：视口裁切三档，高度切顶、宽度 HUD 内收，禁止比例缩放 |
| 2026-08-31 | Overlay | 已实现：产品菜单三档、切顶 TranslateY、HUD Relayout、14B 按当前 viewW 钳制、P1 本地记忆 |
| 2026-08-31 | 策划 | 用户验收通过 |
