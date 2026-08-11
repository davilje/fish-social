# 星露谷式正交 Tilemap 场景

| 项 | 内容 |
|----|------|
| 功能名称 | 星露谷式正交 Tilemap（世界 + 鱼塘） |
| 状态 | **已实现** |
| 编号 | **FEAT-SCENE-TILE-1** |
| 设计时间 | **2026-07-27** |
| 完成时间 | **2026-07-27** |
| 目标版本 | hotfix / scene-tile |
| 优先级 | P0 |
| 前置 | FEAT-SCENE-ISO-1/2（已实现；观感未达标，本需求纠偏） |
| 关联 | [等距网格世界与鱼塘场景.md](./等距网格世界与鱼塘场景.md) · REF-SCENE-1 |
| 关联代码 | `WorldMapView` · `PondScene` · `worldTileMap` · `pondTileMap` · `tileMath` · `TileLayer` · `TileCameraView` |

> 将「假等距圆角扁矩形 + 椭圆稀疏大陆」改为**正交俯视密铺 Tilemap**（星露谷同族）。  
> 玩法权威不变：`pondId` / `spotId`；服务端原则上不改。

---

## 1. 背景与目标

### 1.1 背景

ISO-1/2 已有格子坐标与拖缩放，但绘制为 `borderRadius: 10` 的 48×24 AABB，大陆由椭圆稀疏生成，观感像软色块岛，不符合「每格属于规整 tiled grid」的预期。

### 1.2 目标

- **正交**方格：`x = col * TILE`，`y = row * TILE`（TILE 建议 32～40）
- **稠密**：每个 `(col,row)` 必有地形类型（`void` / `grass` / `dirt` / `water` / `shore` / `pond_entry` / `fish_spot` 等）
- **硬边**：tile 与相机视口均 `borderRadius: 0`；禁止圆角胶囊格、禁止悬浮大圆角长方形色块
- 陆地 = 轴对齐矩形（或阶梯矩形）板块；水面格对齐；禁止椭圆/圆稀疏生成
- 世界与鱼塘**共用** `tileMath` + `TileLayer`；保留视口 culling 与拖/缩
- 进塘弹窗、`pond_entry`、`*-spot-1..20` 语义保持

### 1.3 非目标

- Tiled 编辑器完整导入流水线（可二期另立）  
- 真 diamond 等距密铺（若以后要做另开 FEAT）  
- Unity / 真 3D  
- 改 FSM、生态、人数上限、协议事件名  

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 玩家 | 打开世界地图 | 看到规整方格地板；陆地/水为硬边色块拼成；可拖缩 |
| 玩家 | 点入口格 | 弹窗 → 进入对应塘（同 ISO-2） |
| 玩家 | 鱼塘内 | 同套正交格子；规整水区 + 20 岸边钓位格 |

---

## 3. 功能范围

| # | 工作 | 说明 |
|---|------|------|
| 1 | `shared/tileMath.ts` | `tileToScreen` / `screenToTile`；常量 `TILE_SIZE` |
| 2 | `TileLayer` | 视口内硬边方格；无圆角；可选 1px 格缝 |
| 3 | 世界稠密图 | 重写/替换 `isoWorldGrid`：建议 64×64；四塘矩形陆地 + 矩形/阶梯水面 + 入口格 |
| 4 | 鱼塘对齐 | `PondScene` 改正交；20 spot 锚格中心；去圆角码头底板 |
| 5 | 相机 | `IsoCameraView` 改正交 bounds（可改名 `TileCameraView`）；视口无圆角 |
| 6 | 清理 | 世界/塘绘制路径不再依赖扁矩形 ISO AABB + `borderRadius: 10` |

### 视觉禁令（验收）

- 禁止 tile `borderRadius > 0`  
- 禁止用独立大圆角矩形充当「区域」代替格子  
- 禁止椭圆稀疏大陆作为主生成方式  

---

## 4. 技术影响

### 4.1 数据

- 世界/塘：稠密二维地形（数组或 typed array）；`spotId → (col,row)`  
- **无 DB 迁移**  

### 4.2 API / Socket

无变更。

### 4.3 涉及文件（预估）

- `shared/tileMath.ts`（新）· `shared/isoWorldGrid.ts`（重写或 `worldTileMap.ts`）· 塘 grid 配置  
- `mobile/components/tiles/TileLayer.tsx`（新）  
- `mobile/components/WorldMapView.tsx` · `PondScene.tsx`  
- `mobile/lib/iso/IsoCameraView.tsx`  
- `server/`：不改  

---

## 5. 验收标准

- [x] 世界地图为正交密铺方格；任意 zoom 下格共边、无圆角胶囊感  
- [x] 四塘陆地/水面为矩形或阶梯矩形格块；存在可点 `pond_entry` → 弹窗进塘  
- [x] 鱼塘同套 Tile 渲染；20 钓位 id 与现网一致；角色锚在格中心  
- [x] 相机视口直角；拖/缩可用（Web 优先）  
- [x] 连服钓一条不回归  
- [x] 无旧式 48×24 圆角扁矩形作为主地形绘制  

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 64×64 全量 View 卡顿 | 视口 culling；TILE≥28 |
| 与「斜 45°」旧文案冲突 | 本 FEAT 明确改为正交星露谷风；旧 ISO 文案在变更记录注明纠偏 |

---

## 7. 实现摘要（2026-07-27）

- `shared/tileMath.ts`（TILE=32）· `pondTileMap.ts` · `worldTileMap.ts`（64×64 稠密）  
- `TileLayer` + `TileCameraView`（视口 `borderRadius: 0`）  
- `PondScene` / `WorldMapView` 改用正交密铺；spot id 仍为 `*-spot-1..20`  

---

## 8. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-27 | 初稿立项：**FEAT-SCENE-TILE-1** 已确认（纠偏 ISO-1/2 观感） |
| 2026-07-27 | **已实现**：正交密铺 Tilemap；禁圆角扁矩形主绘制 |
| 2026-07-27 | 体验纠偏另立 [Tilemap相机拖拽与HUD分层.md](./Tilemap相机拖拽与HUD分层.md)（**FEAT-SCENE-TILE-2**） |
