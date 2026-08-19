# Steam 桌面 Overlay 场景布局管线

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Steam 桌面 Overlay 场景布局管线 |
| 编号 | **STEAM-DESKTOP-ART-02** |
| 类型 | **美术**（含 Overlay 像素对齐接入；资源替换仍属 `STEAM-DESKTOP-ART-01`） |
| 负责人 | Unity 桌面 / Overlay 工程师 + 美术 |
| 状态 | **已确认** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P1（后续；不阻塞 07E/07F） |
| 设计时间 | **2026-08-16** |
| 完成时间 | |
| 上位需求 | `STEAM-DESKTOP-07G`、`STEAM-DESKTOP-ART-01`、`STEAM-DESKTOP-07B` |
| 关联 | `STEAM-DESKTOP-07`、`STEAM-DESKTOP-UI` |

---

## 1. 背景与目标

### 1.1 背景

当前 Overlay（`FishSocialOverlay.exe`，`960×480`）把服务端钓位世界坐标用 `MapToScene` **自动缩放到画布**。美术替换 `OverlayResources/pond.png` 后，猫和钓位点不会落在图上的岸/石上，只能“大概在框里”。

产品需要：在 Unity 里用 **与 Overlay 同尺寸的 Canvas** 摆 2D 场景，存成 Prefab，再导出物体坐标；Overlay 按同一套数字 **像素级一一对应** 摆本地 PNG。

Overlay 是独立 WPF 进程，**不能**加载 Unity Prefab、Canvas 或 uGUI。可交接的只有：

- 布局 JSON（画布尺寸、物体 id / 种类 / 像素矩形 / 资源文件名 / `spotId`）
- `OverlayResources/` 里的同名 PNG
- Named Pipe 上的运行时占用（谁站在哪个 `spotId`、`petVisualState`）

### 1.2 目标

- Unity 作为 **Overlay 场景编辑器**：`960×480` Canvas 上摆背景、装饰、钓位锚点；保存 Prefab。
- 编辑器导出 **布局表 JSON**（不是 Prefab 字节、不是贴图）。
- Overlay 作为 **播放器**：读布局表 + 本地图，按导出坐标摆放；钓位像素坐标为真相，**停止**对该塘使用 `MapToScene` 自动缩放。
- 钓鱼权威仍在服务端：Prefab 钓位必须绑定已有 `spotId`；人站哪由 `pond_snapshot` 的 `spotId` 决定。

### 1.3 非目标（明确不做）

- 不启动第二个 Unity Player，不在 Overlay 内跑 uGUI / Camera / Unity 场景。
- 不经 Named Pipe 传输 Prefab、贴图或逐帧像素。
- 不改 Node / `shared` / `mobile` 的钓位权威、钓鱼公式或 Socket 协议。
- 不把服务端 tile-world `x/y` 当作 Overlay 像素真相（有布局表时只作占用键 `spotId`）。
- 不把本管线接到主窗口 `1280×720` 宠物栏（主窗口仍走 `Resources/Pet/`）。
- 不重写 `STEAM-DESKTOP-07A`～`07E` 登录、导航、弹窗、会话。
- 不在本期引入 Spine、换装或一塘一 Overlay 进程。
- 不要求 ART-01 的 PNG 必须先全部交付才能开工导出工具（可用占位图验收管线）。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 美术 | 在 Unity 中打开 Overlay 布局 Prefab，按 `960×480` 摆塘、岸、钓位锚点 | 所见即 Overlay 上的像素位置 |
| 程序 | 菜单导出布局 JSON，构建时拷到 Overlay 旁 | Overlay 启动后按表摆图，无需手写坐标 |
| 玩家 | 进塘后 Overlay 显示鱼塘 | 猫站在图上的钓位，不随世界坐标被整体缩放漂移 |
| 玩家 | 无钓位（等待位） | 使用布局表中的 waiting 区域；无表时回退现有岸边排列 |

---

## 3. 功能范围

### 3.1 功能列表

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | Overlay 布局 Prefab 规范 | P0 | 固定 Canvas `960×480`、原点与锚点；物体挂 `kind` / `spotId` / 资源名 |
| 2 | 编辑器导出布局 JSON | P0 | 根据 Prefab `RectTransform` 写出 Overlay 坐标系（左上原点、Y 向下） |
| 3 | Overlay 读表摆放静态层 | P0 | 背景与装饰按 `sprite` 文件名加载 `OverlayResources/` |
| 4 | Overlay 钓位像素表 | P0 | `kind=spot` 的 `spotId` → `(x,y)`；猫站该点，不再对该塘 `MapToScene` |
| 5 | 构建拷贝 | P0 | JSON + PNG 随 Overlay 发布到 exe 旁 `OverlayResources/` |
| 6 | 无布局回退 | P1 | 某 `pondId` 无 JSON 时保持现有 `MapToScene`，不得崩进程 |
| 7 | 等待位从布局导出 | P1 | Prefab 可标 `kind=waiting`；缺省沿用现岸边格子 |

### 3.2 职责切分

```text
Unity Editor（编辑器，不进 Overlay 进程）
  960×480 Canvas Prefab
        ↓ 导出
  OverlayResources/layouts/<pondId>.json
  OverlayResources/*.png          ← ART-01 交付的图，文件名与 JSON.sprite 一致

Unity 运行时（唯一业务进程）
  Socket pond_snapshot → 占用：playerId / spotId / petVisualState
        ↓ Named Pipe（仍不传图、不传 Prefab）
  Overlay（播放器）
  读 JSON 摆静态层 + 按 spotId 把猫放到布局表坐标
```

**Unity Canvas 只用于编辑与预览，运行时 Overlay 不加载该 Prefab。**

### 3.3 交互与 UI（编辑侧）

- 推荐路径：`fish-social-unity/Assets/Desktop/OverlayLayouts/<pondId>.prefab`（实现时可微调，须写入 README）。
- Canvas：`Screen Space - Overlay` 或固定像素预览均可；**导出必须以 960×480 逻辑像素为准**。
- `Canvas Scaler`：Constant Pixel Size，Reference `960×480`，Scale Factor `1`。
- 每个钓位物体：组件或命名约定必须带 **现有** `spotId`（如 `calm-spot-1`），禁止只放无名空物体。
- 编辑器菜单建议：`Fish Social → Export Overlay Layout`；可一次导出当前 Prefab 或全部塘。
- 导出失败（缺 `spotId`、画布不是 960×480、重名）必须在 Console 报明确错误，不得写出半份表。

### 3.4 规则与数值

- Overlay 窗口仍为 **960×480**（07B/07G）。
- 猫咪显示基准仍为 **128×128**；猫的落点以钓位锚点为准（锚点建议为猫脚底中心；JSON 须写清 `anchor`：`bottom-center` 或等价 pivot）。
- 不改 `shared/` 钓位列表；布局表 `spotId` 必须是该塘快照里会出现的 id。
- 多塘：一塘一份 JSON，文件名或字段内 `pondId` 与进塘 id 一致（如 `pond-calm`）。

---

## 4. 技术影响

### 4.1 数据模型（布局 JSON）

导出坐标系必须是 **Overlay/WPF Canvas**：原点画布左上，`x` 向右，`y` 向下。Unity 侧 `RectTransform` 若为 Y 向上，**由导出器换算**，Overlay 不再猜测 Unity 锚点。

示意（字段名以实现为准，语义必须覆盖）：

```json
{
  "version": 1,
  "pondId": "pond-calm",
  "canvas": { "width": 960, "height": 480, "origin": "top-left" },
  "objects": [
    {
      "id": "pond-bg",
      "kind": "sprite",
      "sprite": "pond.png",
      "x": 0, "y": 0, "w": 960, "h": 480,
      "z": 0
    },
    {
      "id": "calm-spot-1",
      "kind": "spot",
      "spotId": "calm-spot-1",
      "x": 240, "y": 400, "w": 24, "h": 24,
      "anchor": "bottom-center",
      "z": 10
    },
    {
      "id": "cat-size",
      "kind": "pet-size",
      "w": 128,
      "h": 128
    }
  ]
}
```

| `kind` | 含义 | Overlay 行为 |
|--------|------|----------------|
| `sprite` | 静态图（背景、岸、装饰） | 按矩形加载 `sprite` 文件 |
| `spot` | 钓位锚点 | 建立 `spotId` → 像素点；可画调试圆，正式包可隐藏 |
| `waiting` | 无钓位排列区 | 替代硬编码 `WaitingLane` |
| `pet-size` | 猫显示尺寸 | 覆盖默认 128×128（可选） |

运行时占用 **不** 写入 JSON。Pipe 仍只传 `ownSpotId` / `users[].spotId` / `petVisualState`。

### 4.2 API / Socket

| 类型 | 名称 | 说明 |
|------|------|------|
| REST | — | 不新增 |
| Socket | 现有 `pond_snapshot` 等 | 不改协议；`spotId` 仅作占用键 |
| Named Pipe | 现有 `state` | **不**传布局 JSON 全文（文件本地加载）；不传图。若需切塘，可用已有 `pondId` 让 Overlay 换本地文件 |

### 4.3 涉及文件（预估）

- `fish-social-unity/Assets/`：Overlay 布局 Prefab、Editor 导出脚本（**不**改 `mobile/`）
- `desktop-overlay/PondScenePresenter.cs`：有布局表时停用该塘 `MapToScene`；按表摆静态层与猫
- `desktop-overlay/` 构建拷贝：`OverlayResources/layouts/`、PNG
- `docs/planning/`：本 spec；Overlay README 补充编辑器约定
- `shared/` / `server/` / `mobile/`：**不改**

---

## 5. 验收标准

- [ ] 存在可打开的 `960×480` Overlay 布局 Prefab；钓位物体带合法 `spotId`。
- [ ] 编辑器能从 Prefab 导出 JSON；坐标为 Overlay 左上原点；画布尺寸为 960×480。
- [ ] Overlay 启动后按 JSON 摆放对应 PNG；同一 `spotId` 在 Unity 预览与 Overlay 上像素位置一致（允许 1px 级圆整误差）。
- [ ] 玩家占用某 `spotId` 时，猫出现在该锚点，**不再**按服务端世界坐标自动缩放整塘。
- [ ] Named Pipe 不传输图片或 Prefab；Overlay 仍不连 Socket。
- [ ] 无 JSON 的塘回退现有 `MapToScene`，进程不崩溃。
- [ ] 不修改 `mobile/`、`server/`、`shared/` 业务逻辑；不启动第二 Unity Player。
- [ ] Windows 构建（`Fish Social → Build Windows + Native Overlay` 或现行等价菜单）把 JSON 与资源带到 Overlay 旁，Development/Release 包都能读到。

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| Unity 与 WPF 锚点/Y 轴不一致 | 导出器统一成 Overlay 左上坐标；JSON 写死 `origin` |
| 美术用了服务端没有的 `spotId` | 导出校验对照已知塘位表；运行时未知 id 不画猫、打日志 |
| `MapToScene` 与像素表混用 | 某塘一旦有 JSON，该塘全部位置走表，禁止半自动缩放 |
| ART-01 图未齐 | 先占位 PNG 验收管线；正式图替换不改 JSON 字段语义 |
| 主窗口与 Overlay 两套猫 | 文档写清本需求只约束 Overlay；主窗口不走本布局表 |

**依赖：** `STEAM-DESKTOP-07G` Overlay 进程与 Pipe；`STEAM-DESKTOP-07B/07C` 占用与序列帧；`STEAM-DESKTOP-ART-01` 正式贴图可并行。

**建议开工时机：** 07G 可运行且 ART-01 路径约定已有之后；不阻塞 07E 弹窗验收。

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-20 | 策划 | **STEAM-DESKTOP-09D**：Overlay 正式画布增至 **960×560**；后续导出 JSON 的 `canvas.height` 与 09D 对齐（宽仍 960） |
| 2026-08-16 | 策划 | 初稿：Unity Canvas Prefab → 布局 JSON → Overlay 像素真相处；类型归入美术 `STEAM-DESKTOP-ART-02` |
