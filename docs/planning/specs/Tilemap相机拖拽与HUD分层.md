# Tilemap 相机拖拽与 HUD 分层

| 项 | 内容 |
|----|------|
| 功能名称 | Tilemap 相机拖拽修复 · 世界扩图 · 整塘热区 · HUD 分层 |
| 状态 | **已实现** |
| 编号 | **FEAT-SCENE-TILE-2** |
| 设计时间 | **2026-07-27** |
| 完成时间 | **2026-07-27** |
| 目标版本 | hotfix / scene-tile |
| 优先级 | P0 |
| 前置 | FEAT-SCENE-TILE-1（已实现） |
| 关联代码 | `TileCameraView` · `PondScene` · `WorldMapView` · `worldTileMap` · `pondTileMap` |

> 纠偏 TILE-1 上线后体验问题：塘内拖不动、空钓位误导矩形、世界偏小、仅入口格可点、文字跟场景糊在一层。

---

## 1. 背景与目标

### 1.1 背景

| 现象 | 根因摘要 |
|------|----------|
| 池塘内无法上下拖拽 | `clampTranslate` 在 content≤viewport 时把 `ty` 锁死为 0；塘图 24×24 易 fit 进视口 |
| 钓位旁莫名矩形 | `PondScene` 空位仍画 `dock` 装饰条，与 `fish_spot` 色重复 |
| 世界偏小 | `WORLD=64` |
| 仅入口格可进 | `onPress` 只绑 `pond_entry` |
| 文字观感差 | 标签在世界变换层内固定字号，与 tile 同层 |

### 1.2 目标

1. **塘内/世界均可拖拽**（含上下）；内容小于视口时仍可合理平移或提高默认缩放使可拖  
2. **去掉**误导性 `dock` 矩形；空钓位只靠 `fish_spot` 色（或等价清晰标记）  
3. **世界地图放大**（建议 **96 或 128** 边长），四塘陆地拉开  
4. **点击池塘范围内任意格**（含草/土/水/岸/入口）均弹出进塘弹窗  
5. **文字 UI 与场景分层**：Scene 层仅地形+角色；HUD 层塘名/人数/状态字，字号随相机 `scale` 动态调整（或屏幕空间恒定可读）  

### 1.3 非目标

- 改 Socket / spotId 权威、人数上限  
- Tiled 编辑器导入  
- Unity  
- 重做角色美术  

---

## 2. 功能范围

| # | 项 | 说明 |
|---|-----|------|
| A | 相机钳制 | 修 [`TileCameraView`](../../mobile/lib/iso/TileCameraView.tsx) `clampTranslate`；塘内初始 scale 保证可拖或允许居中过冲 |
| B | 去 dock | 删除/停用 `PondScene` `styles.dock`；空位不再画小棕条 |
| C | 扩世界 | `worldTileMap` 边长 ↑；矩形陆地重排；保留 culling |
| D | 整塘热区 | `pondId` 非空格均可 `onPress` → 同一进塘弹窗；入口格可保留高亮 |
| E | HUD 分层 | 世界塘名/人数、塘内状态/气泡：提到相机外 HUD；世界坐标→屏幕投影；`fontSize` 随 scale 或反缩放保持可读 |

---

## 3. 技术影响

- `mobile/lib/iso/TileCameraView.tsx`  
- `mobile/components/PondScene.tsx` · `WorldMapView.tsx`  
- `shared/worldTileMap.ts`（尺寸与热区数据）  
- `server/`：不改  

---

## 4. 验收标准

- [x] 鱼塘场景可上下左右拖拽（非仅缩放到塞满后锁死）  
- [x] 空钓位无「不知所云」小矩形 dock；钓位仍可辨  
- [x] 世界地图明显大于 TILE-1（≥96 边或等价更大可浏览范围）  
- [x] 点击某塘陆地/水面/岸等任意所属格 → 弹出该塘进入弹窗  
- [x] 塘名/人数（及塘内状态字）不在 Scene 变换层与 tile 混排；随缩放保持可读  
- [x] 进塘闭环、连服钓一条不回归  

---

## 5. 风险

| 风险 | 缓解 |
|------|------|
| 整塘热区与拖拽抢手势 | 短拖平移、点击阈值区分；Pressable 不阻断 Pan |
| 128 图性能 | 保持视口 culling |
| HUD 投影抖动 | 跟 `onViewChange` 同步更新 |

---

## 6. 实现摘要（2026-07-27）

- `TileCameraView`：`panable` 初始 scale + 内容≤视口时 overscroll；Pan 与点击错开  
- `PondScene`：移除 dock；状态字 HUD 屏幕投影  
- `worldTileMap`：`WORLD=96`，四象限拉开；`labels` 供 HUD  
- `WorldMapView`：`pondId` 格均可进塘；塘名/人数在相机外 HUD  

---

## 7. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-27 | 初稿立项：**FEAT-SCENE-TILE-2** 已确认 |
| 2026-07-27 | **已实现**：拖拽·去 dock·扩 96·整塘热区·HUD 分层 |
