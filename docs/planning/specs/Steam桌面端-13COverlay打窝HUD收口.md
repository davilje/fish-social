# Steam 桌面 Overlay：打窝 HUD 收口

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 打窝只显示次数，成功提示走 txt_error |
| 编号 | **STEAM-DESKTOP-13C** |
| 类型 | 功能 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop / hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-08-29** |
| 完成时间 | **2026-08-29** |
| 上位需求 | `FEAT-GROUND-01`、`STEAM-DESKTOP-ART-03`、`STEAM-DESKTOP-08G` |

---

## 1. 背景与目标

### 1.1 背景

`FEAT-GROUND-01` 已实现打窝权威（50 层、咬钩/尺寸加成仍在服务端）。Overlay 旁路曾显示「窝 x/50 + 咬+% + 尺+m」，信息过载。`txt_error` 原在 `cap_status` 内，预制体已上移到钓鱼/打窝/离席按钮上方。

### 1.2 目标

- Overlay **只展示打窝次数**，格式 **`打窝0/50`**，写在打窝按钮上。
- 每次打窝成功，`txt_error` 显示：**打窝成功，希望鱼儿能快快长大**。
- `txt_error` 位置与 Prefab 一致：画布 **(371, 484) 218×22**，不再挂在 `cap_status` 下。

### 1.3 非目标

- 不改服务端打窝数值、扣金、等待时间、50 层上限。
- Overlay 不再展示咬钩率/尺寸加成/剩余口数（权威仍可计算，只是 HUD 不画）。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 玩家 | seated 可打窝 | 按钮「打窝n/50」，可点 |
| 玩家 | 打窝成功 | 次数 +1；按钮上方提示成功文案 |
| 玩家 | 金币不足等失败 | `txt_error` 仍为服务端错误句 |

---

## 3. 功能范围

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 按钮文案 | P0 | `打窝{stack}/{max}`，max 默认 50 |
| 2 | 隐藏旁路加成 | P0 | `txt_groundbait` 常隐 |
| 3 | 成功文案 | P0 | Unity `_nativeOverlayError` 成功句，经 IPC `errorMessage` |
| 4 | txt_error 布局 | P0 | 无 parentId；居中 |

---

## 4. 技术影响

- `desktop-overlay/MainWindow.xaml.cs`、`OverlayResources/hud/overlay-hud.json`、`OverlayHudLayout.cs`
- `fish-social-unity/.../DesktopAppBootstrap.cs`（成功句）
- `OverlayHudWidgetCatalog.cs`（`txt_error` 脱离 cap_status）

---

## 5. 验收标准

- [x] 按钮只显示次数，无咬钩/尺寸旁路
- [x] 打窝成功出现指定文案于 txt_error
- [x] txt_error 在钓鱼三按钮上方，与 Prefab 对齐
- [x] 用户验收通过（2026-08-29）

---

## 6. 变更记录

| 日期 | 作者 | 说明 |
|------|------|------|
| 2026-08-29 | 策划 | 验收通过 → **已实现**；服务端加成保留、Overlay 不展示 |
