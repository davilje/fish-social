# Steam 桌面 Overlay：场景四边半透明渐隐

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 场景层四边半透明渐隐 |
| 编号 | **STEAM-DESKTOP-13A** |
| 类型 | 功能 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop / hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-08-29** |
| 完成时间 | **2026-08-29** |
| 上位需求 | `STEAM-DESKTOP-07G`、`STEAM-DESKTOP-ART-03` |

---

## 1. 背景与目标

### 1.1 背景

Overlay 场景贴窗口边缘曾硬切。先叠深色矩形（看起来像黑边），后改 `OpacityMask`。位图 mask 用包围盒相对坐标时，子树（宠物/标签）撑高包围盒，**顶部渐隐可见、底部 100px 仍全不透明**。HUD 若与场景同层 mask，底栏也会被淡出。

### 1.2 目标

- 仅**场景层**（底图/水/装饰/钓位/角色）四边向透明渐隐，透出桌面。
- HUD 不参与渐隐。
- 上下左右渐隐距离均为 **40px**。
- 渐隐按 **960×560 布局坐标**，不跟子控件包围盒走。

### 1.3 非目标

- 不做黑色实色遮罩。
- 不按水面轮廓做不规则 mask。
- 不改 Unity 主窗口渲染。

---

## 2. 根因（底部曾无效）

`OpacityMask` 默认 `RelativeToBoundingBox` 映射到含子树溢出的包围盒；`ClipToBounds` 只裁绘制。渐变 offset=1 落到裁剪区下方，窗口内 y=460～560 仍采到不透明段。左右不受影响（溢出主要在竖直方向）。

修复：`LinearGradientBrush.MappingMode = Absolute`，终点固定为布局高/宽。

---

## 3. 功能范围

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 场景/HUD 分层 | P0 | mask 在 `SceneFadeHost` / `SceneContentCanvas`；HUD 为兄弟 |
| 2 | Absolute 渐变 | P0 | 上/下、左/右各一条 LinearGradientBrush |
| 3 | 距离 | P0 | 四边均为 40px |

---

## 4. 技术影响

- `desktop-overlay/OverlayEdgeVignette.cs`
- `desktop-overlay/MainWindow.xaml(.cs)`（`SceneFadeHost`、`SceneContentCanvas`）

---

## 5. 验收标准

- [x] 四边 40px 透明度渐隐，非黑边
- [x] 底部水面在 y→560 可见渐隐
- [x] HUD（聊天/钓鱼按钮）边缘保持清晰
- [x] 用户验收通过（2026-08-29）

---

## 6. 变更记录

| 日期 | 作者 | 说明 |
|------|------|------|
| 2026-08-29 | 策划 | 验收通过 → **已实现** |
