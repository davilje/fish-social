# Steam 桌面 Overlay：钓位座位预制体与空位显隐

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 座位预制体为唯一真相：椅图 + 猫对齐 + 塘内摆放 |
| 编号 | **STEAM-DESKTOP-14A** |
| 类型 | **功能**（含美术资源路径） |
| 负责人 | Unity 桌面 / Overlay 工程师 + 美术 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop / hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-08-29** |
| 完成时间 | **2026-08-30** |
| 上位需求 | `STEAM-DESKTOP-ART-02`、`STEAM-DESKTOP-ART-01`、`STEAM-DESKTOP-07B` |

---

## 1. 背景与目标

### 1.1 背景

原先存在双轨冲突：

1. **Pond Layout** 里 `kind=spot` 只是 24×24 锚点；14A 初稿用 `OverlaySpotMarker` + 全局 `seats/<pondId>.png` 事后贴椅。
2. **`OverlayPondActor`** 又定义了 `actor-seat` / `actor-pet` 等部件，但导出后 Overlay **不渲染** `actor-seat`，猫仍落在 spot 几何中心。

美术无法在 Unity 里「换椅图 → 把猫摆到椅上 → 再拖到岸边」一次做完。

### 1.2 目标

- **`OverlayPondActor.prefab` = 标准座位预制体**：椅图可换；猫 / 昵称 / 状态 / 圆环相对椅面手动对齐后保存。
- **每个塘 Prefab**（`OverlayLayouts/<pondId>.prefab`）摆放座位实例（挂 `spotId`），保存塘布局。
- 导出布局 JSON：座位框 + `actor-seat` 贴图 + `actor-pet` 等绝对坐标。
- Overlay 播放：画该座位椅图；猫站 `actor-pet`；点击命中座位矩形；空位半透明 / 落座显隐规则不变。
- `spotId` 权威不变，不新增玩法钓位。

### 1.3 非目标

- 不新增服务端 `spotId`，不改钓鱼公式。
- P0 **不做**高斯模糊空位（半透明即可）。
- 不把座位烤进塘底图。
- 不改 `mobile/` / `server/` / `shared/`。
- 不强制一次把所有旧塘 JSON 手改完；无 `actor-*` 时回退 spot 中心 + 圆点/`_default`。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 美术 | 打开 `OverlayPondActor`，换椅图、对齐猫 | 保存后可作为各塘座位模板 |
| 美术 | 在塘 Prefab 里拖座位到岸上 | 导出后 Overlay 像素一致 |
| 玩家 | 进塘未落座 | 空座位半透明可见，可点选 |
| 玩家 | 自己落座 | 空座位隐藏；占用座位不透明；猫站在椅上对齐点 |
| 玩家 | 离席后再等待 | 空座位重新半透明 |

---

## 3. 功能范围

### 3.1 功能列表

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 座位预制体 | P0 | `OverlayPondActor`：`actor-seat` + `actor-pet` + name/status/ring |
| 2 | 塘内摆放 | P0 | 每个 `kind=spot` 下嵌套座位部件；spot 宿主尺寸贴近座位簇 |
| 3 | 换图 | P0 | 每座位可挂不同 Image；导出写入 `sprite`，拷到 `seats/` |
| 4 | 导出绝对坐标 | P0 | 嵌套 `actor-*` 必须相对画布绝对像素，禁止写成父相对误当绝对 |
| 5 | Overlay 椅图 | P0 | 优先 `actor-seat.sprite` → spot.sprite → `seats/_default.png` → 圆点 |
| 6 | 猫落点 | P0 | 有 `actor-pet` 时用其矩形中心；否则 spot 中心 |
| 7 | 空位显隐 | P0 | 未占用 Opacity **0.40**；自己有 `ownSpotId` 时隐藏空位；占用 **1.0** |
| 8 | 点击选位 | P0 | 命中座位矩形（seat 或 spot），命令仍为现有 `spotId` |

### 3.2 编辑工作流

```text
1. 编辑 OverlayPondActor.prefab
   - 替换 actor-seat Image（椅）
   - 拖 actor-pet 对齐椅面
   - 调 name / status / ring
2. 塘 Prefab：每个 spot 下有座位部件（Baker 可补齐）
   - 拖动整个 spot 到岸上正确位置
3. Fish Social → Export Overlay Layout
4. 重启 Overlay 验收
```

### 3.3 资源路径

```text
desktop-overlay/OverlayResources/seats/_default.png     ← 缺图回退
desktop-overlay/OverlayResources/seats/<file>.png       ← 导出拷贝的椅图
Assets/Resources/Desktop/Prefabs/OverlayPondActor.prefab
Assets/Desktop/OverlayLayouts/<pondId>.prefab
```

布局 JSON 示例（节选）：

```json
{
  "id": "calm-spot-1",
  "kind": "spot",
  "spotId": "calm-spot-1",
  "x": 393, "y": 429, "w": 84, "h": 122,
  "anchor": "bottom-center",
  "z": 10,
  "sprite": "seats/_default.png"
},
{
  "id": "calm-spot-1-seat",
  "kind": "actor-seat",
  "spotId": "calm-spot-1",
  "x": 373, "y": 495, "w": 64, "h": 32,
  "anchor": "top-left",
  "sprite": "seats/_default.png"
},
{
  "id": "calm-spot-1-pet",
  "kind": "actor-pet",
  "spotId": "calm-spot-1",
  "x": 361, "y": 415, "w": 64, "h": 64,
  "anchor": "top-left"
}
```

### 3.4 空位显隐规则

占用键：`ownSpotId` + `users[].spotId`（已有 IPC）。

| 自己是否有钓位 | 该 spot 是否被占用 | 显示 |
|----------------|--------------------|------|
| 否 | 否 | 半透明座位，可点 |
| 否 | 是 | 不透明座位 |
| 是 | 否 | **隐藏** |
| 是 | 是 | 不透明座位 |

椅 z 低于猫；猫脚落在 `actor-pet`。

---

## 4. 技术影响

- `fish-social-unity/.../OverlayPondActorBaker.cs` — 座位模板与嵌套到 spot
- `fish-social-unity/.../OverlayPondLayoutBaker.cs` — 新 spot 宿主尺寸
- `fish-social-unity/.../OverlayLayoutExporter.cs` — 绝对坐标；座位贴图拷 `seats/`
- `desktop-overlay/PondScenePresenter.cs` — 椅图 / 猫落点
- `desktop-overlay/OverlayPondLayout.cs` — `TryGetPetPoint`；Seat 参与 chrome
- `desktop-overlay/OverlaySeatArt.cs` — 优先 per-seat sprite
- `desktop-overlay/OverlaySpotMarker.cs` — 仍作点击与显隐载体
- `desktop-overlay/OverlayResources/seats/`、`layouts/<pondId>.json`

不改 Pipe 字段语义。弱化「整塘一张 `seats/<pondId>.png`」为主路径（可作回退）。

---

## 5. 验收标准

- [x] 在 `OverlayPondActor` 换椅图并对齐猫后，嵌到塘 Prefab 导出，Overlay 椅与猫相对位置与 Unity 一致（≤2px）
- [x] 进塘未落座：空座位半透明（约 40%），可点选 `spotId`
- [x] 自己落座：空座位消失；自己座位不透明；猫站在 `actor-pet` 而非 spot 几何中心（有 chrome 时）
- [x] 离席后空座位恢复半透明
- [x] 无 `actor-*` 的旧布局：回退 spot 中心 + `_default`/圆点，不崩
- [x] Debug/Release 构建带上 `seats/`
- [x] 不改 `mobile/`、`server/`、`shared/`

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 旧 JSON 无 actor 部件 | 回退路径保留 |
| 嵌套导出写成相对坐标 | 导出器强制相对画布绝对像素 |
| 正式椅图未齐 | `_default.png` 验收管线 |

**依赖：** ART-02 布局表；ART-01 可并行出图。

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-29 | 策划 | 初稿已确认（spot 圆点改椅图） |
| 2026-08-30 | 策划 | 用户验收通过，状态改为 **已实现** |
| 2026-08-30 | 策划 | **修订**：座位预制体为唯一真相；废弃双轨；猫跟 actor-pet |
