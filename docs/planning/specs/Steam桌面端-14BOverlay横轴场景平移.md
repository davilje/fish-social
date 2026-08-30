# Steam 桌面 Overlay：横轴场景左右平移

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 塘内横轴场景长按箭头平滑平移 |
| 编号 | **STEAM-DESKTOP-14B** |
| 类型 | **功能** |
| 负责人 | Unity 桌面 / Overlay 工程师 + 美术 |
| 状态 | **已确认** |
| 目标版本 | v1.0-steam-desktop / hotfix |
| 优先级 | **P1**（依赖加宽底图与布局画布；窗口视口仍 960×560） |
| 设计时间 | **2026-08-29** |
| 完成时间 | |
| 上位需求 | `STEAM-DESKTOP-ART-02`、`STEAM-DESKTOP-ART-03`、`STEAM-DESKTOP-09D`、`STEAM-DESKTOP-14` |

---

## 1. 背景与目标

### 1.1 背景

塘景按横轴全景设计，但 Overlay **视口锁死 960×560**，底图 `UniformToFill` 铺满。左键拖的是**整个窗口**（`DragMove`），不能平移塘内场景。ART-02 导出器写死画布 960×560。

### 1.2 目标

- 场景内容宽于视口时，左右箭头 **长按平滑平移**（底图 + 装饰 + 座位 + 猫一起动）。
- **视口与窗口仍 960×560**；HUD 与 13A 渐隐跟窗口，不跟场景偏移。
- 布局 JSON `canvas.width` 为场景逻辑宽（P0 默认 **1920**，高仍 **560**）。

### 1.3 非目标

- 不改 Unity 主窗口世界地图（08A）。
- 不做上下平移、捏合缩放。
- 不把箭头放进场景渐隐层。
- `canvas.width ≤ 960` 时不显示箭头、不平移。
- 不改 `server/` / `shared/` / `mobile/`。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 玩家 | 宽场景塘 | 左右箭头；长按向该侧平滑移动，松开关 |
| 玩家 | 已到最左/最右 | 停止，不回弹出界 |
| 玩家 | 点空白可见塘图 | 仍可拖 Overlay 窗口（与 14 一致） |
| 美术 | 出 1920×560 横图 | Prefab 画布同步加宽后导出 |

---

## 3. 功能范围

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 加宽画布 | P0 | 导出器读取 Prefab 实际宽；JSON `canvas.width/height`；Overlay 场景层按此宽布局 |
| 2 | 平移变换 | P0 | 仅 `SceneContentCanvas`（或等价场景根）`TranslateTransform.X`；钳制 `[960 - sceneWidth, 0]` |
| 3 | 箭头 HUD | P0 | 视口左右两侧；长按持续平移（RepeatButton 或 16ms tick）；速度建议 **240～360 px/s**，须平滑 |
| 4 | 渐隐 | P0 | 13A mask 仍按 **视口** 960×560，不跟场景宽走 |
| 5 | 窄塘 | P0 | `canvas.width ≤ 960`：隐藏箭头 |

箭头建议 widgetId：`btn_pan_left` / `btn_pan_right`（可只在 Overlay XAML，不必强行进 OverlayHud Prefab）。须在 HUD 层、可命中。

### 3.2 规则

- 进塘默认偏移：优先让自己的座位落入视口中部；无座位则 `offsetX = 0`（看左半幅）。
- 切塘重置偏移。
- Pipe 仍不传图。

---

## 4. 技术影响

- `fish-social-unity/.../OverlayLayoutExporter.cs` — 取消写死 960 宽
- `desktop-overlay/MainWindow.xaml(.cs)`、`PondScenePresenter.cs`、`OverlayPondLayout.cs`
- `desktop-overlay/OverlayResources/layouts/*.json`、`ponds/<pondId>.png`（加宽）
- `OverlayEdgeVignette` — 仍按窗口尺寸 ApplySize

---

## 5. 验收标准

- [ ] 场景宽 1920、视口 960 时，长按左右箭头平滑平移，松开关，不越界
- [ ] 猫、座位、底图同步移动；HUD、渐隐不跟着偏
- [ ] 窄塘无箭头、不平移
- [ ] 点可见塘图仍可拖窗口；点箭头不拖窗口
- [ ] 13A 四边渐隐仍在
- [ ] 不改 `mobile/`、`server/`、`shared/`

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 底图仍 960 宽 | 本需求 P1；无宽图则本条无法验收平移距离 |
| 导出仍写死 960 | 必须改导出器 |
| 与窗口拖动抢左键 | 箭头独占；空白拖窗口 |

**依赖：** **14** 穿透；加宽 ART-02 Prefab + 横图。建议放在 14A 之后。

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-29 | 策划 | 初稿已确认；P1；默认场景宽 1920 |
