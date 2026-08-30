# Steam 桌面 Overlay：透明区点击穿透回归

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 透明像素点击穿透桌面（保留四边渐隐） |
| 编号 | **STEAM-DESKTOP-14** |
| 类型 | **Bug修复** |
| 负责人 | Unity 桌面 / Overlay 工程师 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop / hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-08-29** |
| 完成时间 | **2026-08-29** |
| 上位需求 | `STEAM-DESKTOP-07G`、`STEAM-DESKTOP-13A` |

---

## 1. 现象与复现

| 项 | 内容 |
|----|------|
| 步骤 | Overlay 置顶；把鼠标放到窗口矩形内、但塘图/HUD/猫都不在的透明或已渐隐到几乎看不见的区域；点击桌面图标或其它窗口 |
| 预期 | 点击穿透到桌面（07G：只对场景/宠物/HUD 命中） |
| 实际 | Overlay 吃掉点击；无法点到后面的桌面 |
| 平台 | Windows Overlay `FishSocialOverlay.exe`，画布 960×560 |
| 复现 | 稳定；进塘后全窗口均可复现 |

本缺陷**不经 Socket**，无 commandId。证据在 Overlay 命中与分层窗口 alpha，不在服务端耗时。

## 2. 根因分层

| 层 | 结论 |
|----|------|
| 服务端 / Socket | 无关 |
| Named Pipe | 无关 |
| Unity 主线程 | 无关 |
| Overlay 绘制 | **13A** 为 `OpacityMask` 在 `SceneFadeHost` / `SceneContentCanvas` 铺了 `Background="#01000000"`（alpha=1）。DWM 把非零 alpha 当可点 |
| Overlay 命中 | `WM_NCHITTEST` 在整个 `PondScene` 矩形内返回 `HTCLIENT`，覆盖系统按像素 alpha 穿透 |
| 底图 | 铺满 960×560 的 `Image` 按控件矩形命中，不看 PNG 透明通道 |

**13A 的视觉渐隐（`OverlayEdgeVignette` / Absolute `OpacityMask`）不是根因，本需求不得拆掉。**

## 3. 背景与目标

### 3.1 目标

- 合成后 **alpha=0**（透出桌面）的像素：点击穿透。
- **猫、座位、可点钓位、HUD 按钮/聊天条**：仍命中 Overlay。
- **四边 40px 半透明渐隐视觉效果保持**，验收与 13A 相同（非黑边、底部有效、HUD 不参与渐隐）。
- 渐隐带上塘图仍可见的区域：允许命中（便于拖窗口/点岸），不要求这条带穿透。

### 3.2 非目标

- 不取消 13A，不改渐隐距离（仍 40px）。
- 不改 Unity 主窗口。
- 不改 `server/` / `shared/` / `mobile/`。
- 不做不规则水面轮廓穿透（仍按像素 alpha + 控件命中）。

---

## 4. 功能范围

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 去掉整层「隐形玻璃」 | P0 | 场景宿主背景改为真正 Transparent；不要用 `#01000000` 填满来骗命中 |
| 2 | 按控件命中 | P0 | `WM_NCHITTEST`：命中猫/座位/HUD/可见钓位 → `HTCLIENT`；否则 `HTTRANSPARENT` |
| 3 | 保留 OpacityMask | P0 | `OverlayEdgeVignette` 逻辑与 40px 距离不变 |
| 4 | 拖窗口 | P0 | 点在可见塘图（含渐隐带内仍能看见的图）上仍可 `DragMove` |

### 4.1 交互

- 点桌面空白（Overlay 透明洞）：桌面响应。
- 点猫、按钮、可见座位：Overlay 响应。
- 边缘仍看到淡出的塘：可拖 Overlay 窗口。

---

## 5. 技术影响

- `desktop-overlay/MainWindow.xaml(.cs)` — `WindowHook`、`SceneFadeHost` / `SceneContentCanvas` Background、`IsHitTestVisible`
- `desktop-overlay/OverlayEdgeVignette.cs` — **只允许为命中做兼容，禁止删 mask**
- 可选：`PondScenePresenter` 底图命中与 PNG alpha

不改 Pipe 协议。

**回滚：** 恢复 `#01000000` + 整窗 `HTCLIENT`（穿透会再坏，渐隐仍在）。

---

## 6. 验收标准

- [x] 窗口矩形内、合成透明处点击可落到桌面图标/其它窗口
- [x] 猫、HUD、可见座位/钓位仍可点、可拖（点可见塘图可拖窗口）
- [x] 13A：四边 40px 渐隐仍在；非黑边；底部有效；聊天/钓鱼按钮不被渐隐
- [x] 首次/连续点击 HUD 不误穿；拖窗口不卡死
- [x] 不改 `mobile/`、`server/`、`shared/`

---

## 7. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 去掉填充后 mask 宿主无尺寸 | mask 仍绑在固定 960×560 的 Border；子树塘图继续被 fade |
| 底图 Image 整块抢命中 | 透明通道或缩小命中到非透明像素；或仅对 Image `IsHitTestVisible=false`、拖动走父级可见区域策略 |
| 与 14A 座位抢命中 | 本需求先落地；座位图层随后接入同一套 HitTest |

**建议顺序：** 本需求先于 14A / 14B。

---

## 8. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-29 | 策划 | 初稿已确认：穿透回归且保留 13A 渐隐 |
| 2026-08-29 | 开发 | 用户验收通过 → **已实现** |
