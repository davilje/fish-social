# Steam 桌面 Overlay：HUD 正式素材与文字样式对齐

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Unity OverlayHud 换正式图并导出字体/对齐到 Overlay |
| 编号 | **STEAM-DESKTOP-14C** |
| 类型 | **美术**（含 Overlay 接入） |
| 负责人 | Unity 桌面 / Overlay 工程师 + 美术 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop / hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-08-29** |
| 完成时间 | **2026-08-30** |
| 上位需求 | `STEAM-DESKTOP-ART-03`、`STEAM-DESKTOP-13` |

---

## 1. 背景与目标

### 1.1 背景

ART-03 已能从 `OverlayHud.prefab` 导出 `x,y,w,h` 和按钮 `sprite`。Prefab 上仍是默认 uGUI 资源。导出 JSON **没有**字体、字号、颜色、对齐；Overlay `NormalizeForCanvas` 还把所有 `TextBlock` **强制左对齐**，与 Unity 居中不一致。

动态文案（「开始钓鱼」↔「收杆」、打窝次数）仍应由 IPC 改 Content，**不要烤进 PNG**（ART-03 已约定）。

### 1.2 目标

- 美术在 Unity 替换 OverlayHud 的 Image Sprite；导出后 Overlay 贴图与矩形一致（误差 ≤1px，延续 13）。
- 导出并应用：**字体文件、字号、颜色、字重、水平对齐（左/中/右）、按钮 Content 对齐**。
- Unity 与 Overlay **使用同一套 TTF/OTF**。

### 1.3 非目标

- 不要求汉字笔画与 Unity **像素级截图一致**（排版引擎不同；同一字体 + 字号即可）。
- 不把会变的按钮字画进 PNG。
- 不重做 13 聊天栏展开方向。
- 不改 `server/` / `shared/` / `mobile/`。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 美术 | Prefab 换按钮图、改 Text 字号/居中 | 导出 → 拷资源 → 重启 Overlay 后位置、图、对齐一致 |
| 玩家 | 开钓/收杆 | 按钮字仍随相位变，底板图不变 |

---

## 3. 功能范围

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 换正式 Sprite | P0 | Prefab Image；导出拷到 `OverlayResources/hud/` |
| 2 | 文字样式导出 | P0 | JSON 增补见下；Overlay 停止强制左对齐 |
| 3 | 嵌入字体 | P0 | `OverlayResources/hud/fonts/<文件>`；WPF `FontFamily` 指向该文件 |
| 4 | 按钮 Content 对齐 | P0 | 对应 Unity `alignment` / WPF `HorizontalContentAlignment` |

建议 JSON 字段（实现名可微调，语义必须覆盖）：

| 字段 | 说明 |
|------|------|
| `fontFile` | 相对 `hud/fonts/` |
| `fontSize` | px，与 Prefab 一致 |
| `fontColor` | `#AARRGGBB` |
| `fontWeight` | normal / bold |
| `textAlign` | `left` / `center` / `right` |
| `contentAlign` | 按钮内容对齐，同上 |

`kind=text` / `button` 需要文字样式的控件都要导出。

---

## 4. 技术影响

- `fish-social-unity/.../OverlayHudExporter.cs`、`DesktopOverlayHudWidget.cs`
- `desktop-overlay/OverlayHudLayout.cs`（**删除或收窄**强制 `TextAlignment.Left`）
- `desktop-overlay/OverlayResources/hud/`、`hud/fonts/`
- 构建拷贝 `hud/` 整目录

不改命令名。

---

## 5. 验收标准

- [x] Prefab 换图并导出后，Overlay 按钮/面板贴图与矩形误差 ≤1px
- [x] 状态字、按钮字的字号、颜色、左/中/右对齐与 Prefab 一致（允许字形 1px 级光栅差）
- [x] Overlay 不再把所有 HUD 文字强制左对齐
- [x] 「开始钓鱼」/「收杆」/打窝次数仍为运行时文字，未烤进 PNG
- [x] 缺字体文件时回退系统字体并打日志，不崩
- [x] 不改 `mobile/`、`server/`、`shared/`

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 中文字体授权 | 只用项目已有可分发字体 |
| 与 13 聊天嵌套坐标 | 只加样式字段，不改相对坐标算法 |

**依赖：** ART-03 / 13。可与 14A 并行（不同目录）。

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-30 | 策划 | 用户验收通过，状态改为 **已实现** |
| 2026-08-29 | 策划 | 初稿已确认 |
