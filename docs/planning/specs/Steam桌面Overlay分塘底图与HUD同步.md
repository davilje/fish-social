# Steam 桌面 Overlay：分塘底图、猫咪序列帧与 HUD 同步

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 分塘底图、猫咪姿势序列帧、Unity HUD 与 Overlay 一一对应 |
| 编号 | **STEAM-DESKTOP-ART-03** |
| 类型 | **美术**（含 Unity 桌面 / Overlay 接入） |
| 负责人 | Unity 桌面 / Overlay 工程师 + 美术 |
| 状态 | **已确认** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P0 |
| 设计时间 | **2026-08-28** |
| 完成时间 | |
| 上位需求 | `STEAM-DESKTOP-ART-01`、`STEAM-DESKTOP-ART-02`、`STEAM-DESKTOP-09D` |
| 关联 | `STEAM-DESKTOP-07G`、`STEAM-DESKTOP-07C`、`STEAM-DESKTOP-08G` |
| 关联全景 | Steam 桌面 Overlay 表现层 |

---

## 1. 背景与目标

### 1.1 背景

当前 Overlay：

- 全塘共用一张 `OverlayResources/pond.png`，换塘底图不会变。
- 猫咪已按头像 `petId`（`orange` 等六种）选套，但姿势只有粗粒度 `idle/fishing/hooked/catching`，且多为单帧；没有待机 / 坐下 / 抛竿 / 钓鱼 / 咬钩 / 收杆的序列帧目录约定。
- HUD（菜单、钓鱼按钮、聊天条）写死在 `MainWindow.xaml`，无法在 Unity 里摆位置、换按钮图后再同步到 Overlay。

`STEAM-DESKTOP-ART-02` 只管**塘内场景**（背景装饰 + 钓位像素，停用 `MapToScene`），**不管** HUD 控件。本需求补齐：分塘底图、角色动画夹、HUD Prefab 导出。

### 1.2 目标

- 每个 `pondId` 一张独立底图；换塘立刻换图，缺图有明确回退。
- 每种猫（`petId`）六套姿势序列帧目录提前建好；运行时按钓鱼相位播对应夹，缺帧有回退不崩。
- Overlay HUD 在 Unity 用 **960×560 Canvas Prefab** 调位置与贴图；导出后 Overlay 按同一套像素矩形和 PNG 显示，点击仍走现有 Named Pipe 命令。

### 1.3 非目标（明确不做）

- 不在 Overlay 内跑 Unity / uGUI；不经 Pipe 传 Prefab 或贴图像素。
- 不改 Node / `shared` 钓鱼权威、`fishingPhase` 枚举含义、Socket 协议（只扩展 Overlay IPC 的 `petVisualState` 取值与资源路径）。
- 不把 ART-02 的钓位 JSON 与 HUD JSON 混成一个文件。
- 不重做主窗口 1280×720 商店/背包页（仅 Overlay HUD）。
- 不要求本期交齐全部正式序列帧；**目录、命名、加载与回退必须先落地**，可用 1 帧占位验收管线。
- 不做 Spine；仍为 PNG 序列帧。
- 不把 ChatGPT 整屏效果图当运行时资源。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 玩家 | 从静水湾换到晨雾竹塘 | Overlay 底图换成该塘专属 PNG |
| 玩家 | 坐下、抛竿、等鱼、上钩、收杆 | 自己和同塘玩家播对应姿势序列，猫种仍跟头像 |
| 美术 / 程序 | 在 Unity 里拖 Overlay 按钮 | 导出后 WPF Overlay 按钮位置、尺寸、贴图一致 |
| 程序 | 某塘或某姿势缺图 | 回退默认底图 / 该猫其它姿势 / 矢量占位，进程不崩 |

---

## 3. 功能范围

### 3.1 功能列表

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 分塘底图 | P0 | `ponds/<pondId>.png`；IPC 已有 `pondId`；换塘换图 |
| 2 | 猫咪姿势序列帧目录 + 加载 | P0 | 六姿势夹；按相位映射；缺帧回退 |
| 3 | `petVisualState` 细化为姿势 id | P0 | Unity 映射后下发；Overlay 只按字符串选夹 |
| 4 | Overlay HUD Prefab（Unity 编辑器） | P0 | 960×560，控件 id 与命令绑定，贴图可换 |
| 5 | 导出 HUD JSON + 拷贝 PNG | P0 | 菜单导出；构建拷到 Overlay 旁 |
| 6 | Overlay 按表生成/摆放 HUD | P0 | 有表则覆盖 XAML 默认坐标与贴图；无表回退现 XAML |
| 7 | 构建同步 | P0 | Debug/Release 包带上 `ponds/`、`pets/`、`hud/` |

### 3.2 分塘底图

权威路径（仓库源，构建拷到 exe 旁）：

```text
desktop-overlay/OverlayResources/ponds/<pondId>.png
desktop-overlay/OverlayResources/ponds/_default.png   ← 缺塘图时
desktop-overlay/OverlayResources/pond.png             ← 兼容旧文件，最后回退
```

- `<pondId>` 与服务端 / `DesktopGameData` 一致（如 `pond-calm`）。
- Overlay 读 `message.PondId` 加载；切换 `pondId` 必须换图并丢掉上一张缓存。
- 拉伸：`UniformToFill` 铺满 960×560。
- ART-02 若已有该塘布局 JSON：底图仍用本条路径（或 JSON 里 `kind=background` 的 `sprite` 指向同一文件）；钓位像素仍以 ART-02 为准。ART-02 未实现时继续 `MapToScene`。

### 3.3 猫咪序列帧

#### 目录

```text
desktop-overlay/OverlayResources/pets/<petId>/<clip>/0.png
desktop-overlay/OverlayResources/pets/<petId>/<clip>/1.png
…
```

`petId`：`orange` · `calico` · `gray` · `siamese` · `tuxedo` · `white`（与头像 id 相同）。

`clip`（姿势 id，即扩展后的 `petVisualState`）：

| clip | 中文 | 播放时机（`fishingPhase` → clip） |
|------|------|-----------------------------------|
| `idle` | 待机 | `idle`、`disconnected`；无钓位 |
| `sit` | 坐下 | `seated`、`groundbaiting` |
| `cast` | 抛竿 | `baiting`、`casting` |
| `fishing` | 钓鱼（竿在水里等鱼） | `waiting` |
| `hooked` | 咬钩中 | `hooked` |
| `reel` | 收杆 | `resolving`、`stopping` |

Overlay 窗口拖动仍可用现有 `dragging`（无目录则回退 `idle`）。`offline` 回退 `idle`。

兼容旧文件（加载顺序靠后）：`pets/<petId>/fishing-0.png`、`pets/<petId>/<clip>-0.png`。

建议每夹至少 4 帧验收；允许先只放 `0.png`。

#### 加载回退（每种猫、每个 clip）

1. `pets/<petId>/<clip>/N.png`（N 从 0 连续到缺）
2. `pets/<petId>/<clip>-0.png`
3. 同猫 `fishing/` 或 `fishing-0.png`
4. 同猫 `idle/`
5. 全局旧 `cat.png` / 矢量占位

缺图不得抛未处理异常。循环播放；`hooked` 可与现圆环并存。

Unity 主窗口宠物用同一套路径（`StreamingAssets/Pet/<petId>/<clip>/` 由构建从 OverlayResources 同步），与 Overlay **同名文件**。

### 3.4 Overlay HUD：Unity 调、导出、Overlay 播

#### 编辑

- 新建 Prefab：建议 `Assets/Resources/Desktop/Prefabs/OverlayHud.prefab`。
- Canvas：**960×560**，Scaler Constant Pixel Size、Scale Factor 1。
- 登记 `DesktopPrefabCatalog`（见 `docs/planning/reports/Unity桌面UI新建须同步Prefab.md`）。
- 每个可点控件：稳定 **widgetId**（见下表）+ 绑定现有 Overlay 命令语义；`Image` 引用 PNG（可放 `Assets/StreamingAssets/OverlayHud/` 或 OverlayResources 源目录，导出时拷贝）。
- Unity **运行时不要**把该 Prefab 画进玩家主窗口当第二套 Overlay；仅编辑器预览 + 导出。

#### 必须覆盖的 widgetId（与现 Overlay 行为对齐）

| widgetId | 命令/行为 | 现 XAML 大致位置 |
|----------|-----------|------------------|
| `btn_menu_toggle` | 展开/收起左上菜单 | 左上 8,8 约 36×32 |
| `btn_menu_map` | `menu_map` | 菜单内 |
| `btn_menu_shop` | `menu_shop` | |
| `btn_menu_friends` | `menu_friends` | |
| `btn_menu_catch` | `menu_catch` | |
| `btn_menu_leaderboard` | `menu_leaderboard` | |
| `btn_menu_settings` | `menu_settings` | |
| `cap_status` | 状态/鱼塘/钓位文字（可只导出矩形，文字仍 IPC） | 左上胶囊 |
| `btn_open_main` | `open_main` | 右上 |
| `btn_exit_pond` | 退出鱼塘 | 右上 |
| `btn_fishing_toggle` | 开始钓鱼 / 收杆（现有切换） | 右下 |
| `btn_groundbait` | 打窝 | 右下 |
| `btn_catch_leave` | 领鱼 / 离席 | 右下 |
| `dock_chat` | 公屏条 + 输入（结构可保留子控件） | 底栏 |

Debug / 出警按钮可不进美术 Prefab；无表时保持代码里现有显示逻辑。

#### 导出物

```text
OverlayResources/hud/overlay-hud.json
OverlayResources/hud/<spriteFile>.png
```

JSON 坐标系：**左上原点、Y 向下**，单位 CSS 像素，画布 960×560。每条含 `id`、`kind`（`button`/`panel`/`text`/`image`）、`x,y,w,h`、`sprite`（相对 `hud/` 的文件名）、可选 `z`、`visibleDefault`。

菜单：**Fish Social → Export Overlay HUD**。失败（画布不是 960×560、缺强制 id、重名）必须报错，**不得写半份文件**。

构建：拷到 `FishSocialOverlay.exe` 旁 `OverlayResources/hud/`。

#### Overlay 运行时

- 若存在合法 `overlay-hud.json`：按表创建/移动控件，按钮图用 JSON 的 `sprite`；HitTest 矩形 = 导出矩形。
- 若不存在或校验失败：回退当前 `MainWindow.xaml` 布局，打日志。
- 文字（状态、按钮「开始钓鱼」↔「收杆」）仍由 IPC 状态改 **Content**，不把文案烤进 PNG（PNG 可只做九宫格/底图）。
- 禁止第二套命令名。

### 3.5 交互与 UI

美术在 Unity 打开 `OverlayHud` Prefab 拖按钮、换 Sprite → 导出 → 重新打包或拷贝 OverlayResources → 重启 Overlay 验收。玩家无新操作。

### 3.6 规则与数值

- Overlay 窗口仍 **960×560**；猫显示约 **64×64**。
- 序列帧建议 8–12 fps（实现可配置常量）；不写入服务端。
- 不改 `shared/` 相位字符串。

---

## 4. 技术影响

### 4.1 数据模型

无新数据库表。磁盘约定见 §3。IPC 增加/收紧：

- 已有 `pondId`：用于选底图。
- 已有 `ownPetId` / `users[].petId`。
- `petVisualState` / 每人 `petVisualState` 取值扩展为 §3.3 的 `clip`（`sit`/`cast`/`reel` 等）。

### 4.2 API / Socket

| 类型 | 名称 | 说明 |
|------|------|------|
| REST | 无 | |
| Socket | 无新事件 | 仍用现有 `fishingPhase` |
| Named Pipe | `state` | 映射后的 `petVisualState`；不传图 |

### 4.3 涉及文件（预估）

- `desktop-overlay/PondScenePresenter.cs`：按 `pondId` 加载底图
- `desktop-overlay/OverlayPetActor.cs`：按 `petId`+clip 播序列帧
- `desktop-overlay/MainWindow.xaml(.cs)`：HUD 表驱动或回退
- `desktop-overlay/IpcProtocol.cs`：若需字段注释
- `fish-social-unity/.../PetStateController.cs`：相位 → clip
- `fish-social-unity/.../PetArtLoader.cs`：夹目录加载
- `fish-social-unity/.../OverlayPondStateBuilder.cs`：下发 clip
- `fish-social-unity/Assets/Scripts/Desktop/Editor/`：HUD 导出、资源拷贝、`DesktopPrefabCatalog`
- `desktop-overlay/OverlayResources/ponds/`、`pets/<id>/<clip>/`、`hud/`
- **不改** `mobile/`、`server/`、`shared/` 业务（除非实现时发现 snapshot 缺 `pondId` 已有字段——`pondId` 已在 Overlay state 中）

---

## 5. 验收标准

- [ ] 至少两个不同 `pondId` 进塘，Overlay 底图不是同一张；缺 `ponds/<id>.png` 时落到 `_default.png` 或旧 `pond.png`，不崩溃。
- [ ] 仓库存在六 `petId` × 六 `clip` 目录（可仅 `.gitkeep` + 可选 1 帧）；加载器按 §3.3 查找。
- [ ] 坐下 / 抛竿 / 等鱼 / 上钩 / 收杆 时 Overlay 与 Unity 主窗宠物使用同一 `petId`+`clip` 路径规则。
- [ ] 同塘另一玩家头像不同时，播的是对方 `petId` 的对应 clip，不是全塘一张图。
- [ ] Unity 可打开 Overlay HUD Prefab，移动「开始钓鱼」后导出 JSON，Overlay 重启后该按钮像素位置与 Prefab 一致（误差 ≤1px）。
- [ ] 导出菜单在画布尺寸错误或缺强制 `widgetId` 时失败且不写半份 JSON。
- [ ] 无 HUD JSON 时 Overlay 仍可用现有 XAML 操作钓鱼/菜单/聊天。
- [ ] Debug/Release 构建把 `ponds/`、`pets/`、`hud/` 拷到 Overlay exe 旁。
- [ ] Named Pipe 仍不传贴图；不改服务端钓鱼权威。

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 序列帧未齐 | 目录先行 + 回退链；1 帧即可验收加载 |
| HUD 导出与 XAML 双源 | 有 JSON 以 JSON 为准；无 JSON 回退 XAML |
| 与 ART-02 抢背景字段 | 底图路径本 spec 约定；ART-02 只增加钓位像素 |
| 按钮图含死文字 | 文案由 IPC 改 Button.Content；PNG 作底 |

**依赖：** Overlay 进程与 Pipe（07G）；`petId` 下发（已有）；ART-02 可并行，不阻塞本需求 P0。

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-28 | 策划 | 初稿已确认：分塘底图、六姿势序列帧目录/加载、Unity HUD Prefab 导出同步 Overlay |
