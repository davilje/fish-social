# Steam 桌面 Overlay：HUD 聊天栏收口与预制体对齐

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay HUD 聊天栏收口与预制体像素对齐 |
| 编号 | **STEAM-DESKTOP-13** |
| 类型 | 功能 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop / hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-08-29** |
| 完成时间 | **2026-08-29** |
| 上位需求 | `STEAM-DESKTOP-ART-03`、`STEAM-DESKTOP-09C`、`STEAM-DESKTOP-08G` |

---

## 1. 背景与目标

### 1.1 背景

HUD 由 Unity `OverlayHud.prefab` 导出 `overlay-hud.json`。运行时曾把聊天子控件平铺到画布绝对坐标，并用硬编码高度做底部锚定，导致：聊天栏常驻两行、展开方向错误、预制体坐标与 Overlay 不一致。导出器只用 `Transform.Find` 找直接子节点，嵌套控件（如 `btn_fishing_toggle`）尺寸修复被跳过。

### 1.2 目标

- 聊天栏默认一行 + ▲，点击展开第二行输入；底边固定、**向上**展开。
- `dock_chat` 子控件使用相对父级坐标，嵌在内部 Canvas。
- Overlay 像素位置与 Prefab 导出 JSON 一致（误差 ≤1px）。
- 导出器能修复嵌套 HUD 控件尺寸。

### 1.3 非目标

- 不改公屏协议与气泡逻辑（09C）。
- 不把聊天栏移出 960×560 画布。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 玩家 | 进塘看 Overlay | 聊天栏一行预览 + ▲ |
| 玩家 | 点 ▲ | 输入行向上展开，底边不动 |
| 美术 | 改 Prefab 后导出 | Overlay 重启后位置与 Prefab 一致 |

---

## 3. 功能范围

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 折叠/展开 | P0 | 折叠高取 JSON `dock_chat.h`；展开高取子控件 `y+h` 最大 |
| 2 | 嵌套坐标 | P0 | 预览/开关/输入/发送相对 `dock_chat` |
| 3 | 导出修复 | P0 | `GetComponentsInChildren` 索引嵌套 widget |

---

## 4. 技术影响

- `desktop-overlay/OverlayHudLayout.cs`、`MainWindow.xaml.cs`
- `fish-social-unity/.../OverlayHudExporter.cs`、`DesktopPrefabBaker.cs`
- `desktop-overlay/OverlayResources/hud/overlay-hud.json`

不改 `server/` / `shared/`。

---

## 5. 验收标准

- [x] 默认一行 + ▲；点击向上展开，底边固定
- [x] 预制体 `dock_chat` 坐标与运行时一致
- [x] 嵌套按钮导出尺寸有效
- [x] 用户验收通过（2026-08-29）

---

## 6. 变更记录

| 日期 | 作者 | 说明 |
|------|------|------|
| 2026-08-29 | 策划 | 验收通过 → **已实现** |
