# Steam 桌面 Overlay：头顶状态图标与 actor-ring 进度环

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | 状态图标仅上钩/打窝；进度环改到预制体头顶 ring 槽并加 ring-bg |
| 编号 | **STEAM-DESKTOP-14E** |
| 类型 | **功能**（`STEAM-DESKTOP-14D` 修订） |
| 负责人 | Overlay / Unity 桌面工程师 |
| 状态 | **已实现** |
| 目标版本 | hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-09-01** |
| 完成时间 | **2026-09-01** |
| 上位需求 | `STEAM-DESKTOP-14D`、`STEAM-DESKTOP-09D`、`FEAT-GROUND-01` |

---

## 1. 背景与目标

### 1.1 背景

14D 验收口径：等鱼也出 `fishing.png`；上钩圆环 **套在 64 猫身外圈**。现网产品改为：等鱼不要常驻图标；环跟预制体 **actor-ring / actor-ring-bg** 走，套在头顶而不是套猫。

### 1.2 目标

| 相位 | 图标 | 环 |
|------|------|-----|
| idle / seated / 未钓 | 无 | 无 |
| waiting / baiting / casting / fishing（竿在水里） | **无** | 无 |
| hooked（及收杆倒计时相位） | `hooked.png` | `ring-bg` + `hook-ring`，进度 `hookDeadlineMs` |
| groundbaiting | `groundbait.png` | 同上，打窝倒计时 |

图标与环都落在 **actor-status / actor-ring / actor-ring-bg** 槽，不套猫身。

### 1.3 非目标

- 不恢复头顶相位中文（14D 这条仍有效）。
- 不改 09B/18 悬停文案。
- 不把环改回套猫。
- 不改 `mobile/` 钓鱼公式。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 玩家 | 等鱼 | 只有猫 + 昵称，无钓鱼图标、无环 |
| 玩家 | 上钩 | 头顶 hooked 图标 + 环从满收到空 |
| 玩家 | 打窝等待 | 头顶打窝图标 + 环 |
| 玩家 | 坐下未开钓 | 无 icon、无环 |

---

## 3. 功能范围

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 图标映射 | P0 | `hooked` → hooked；`groundbaiting` → groundbait；其余相位 `Source=null` |
| 2 | 环槽 | P0 | 环与 bg 用 chrome `Ring` / `RingBg`（相对 pet）；Baker 导出 `actor-ring-bg` |
| 3 | 资源 | P0 | `OverlayResources/status/hooked.png`、`groundbait.png`、`hook-ring.png`、`ring-bg.png`；`fishing.png` 可留文件但 Overlay 不再用于等鱼 |
| 4 | 布局 | P0 | 21 塘 layout JSON 含 `actor-ring-bg`，与 Unity Prefab / Baker 一致 |

14D「圆环套猫、等鱼出 fishing.png」被本单取代。

---

## 4. 技术影响

- `desktop-overlay/OverlayPetActor.cs` `ResolveStatusIconKind` / `UpdateHookRing`
- `desktop-overlay/OverlayStatusIcons.cs`、`OverlayPondLayout.cs`
- `fish-social-unity/.../OverlayPondActorBaker.cs`、`OverlayLayoutExporter.cs`
- `scripts/patch-overlay-ring-bg.py`（存量 JSON 补槽）
- Unity `OverlayPondActor.prefab`：`actor-ring-bg`

---

## 5. 验收标准

- [x] 等鱼：无状态图标、无环
- [x] 上钩：hooked 图标 + 头顶 ring-bg/hook-ring，进度随截止时间
- [x] 打窝中：groundbait 图标 + 同一套环
- [x] idle/坐下：无 icon、无环
- [x] 环不套在猫身外圈；位置跟该座 actor-ring
- [x] 头顶仍无「钓鱼中」「上钩」中文

---

## 6. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-09-01 | 策划 | 用户验收通过，状态改为 **已实现** |
| 2026-09-01 | 策划 | 已确认：修订 14D；等鱼无图标；环改头顶槽 + ring-bg |
