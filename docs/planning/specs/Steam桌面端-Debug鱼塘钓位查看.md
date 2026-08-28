# STEAM-DESKTOP-12B：Overlay Debug 鱼塘/钓位查看与强制上钩

## 元信息

| 字段 | 内容 |
|------|------|
| 编号 | `STEAM-DESKTOP-12B` |
| 类型 | 功能 / 调试工具 |
| 状态 | **已实现** |
| 设计时间 | **2026-08-25** |
| 完成时间 | **2026-08-26** |
| 目标版本 | hotfix |
| 优先级 | P1 |
| 依赖 | `STEAM-DESKTOP-12` |
| 平台 | Steam Overlay + Unity 桌面端 + 服务端 Debug API |

## 1. 背景与目标

在 **STEAM-DESKTOP-12** Overlay 玩法 Debug 弹窗上增强：任意鱼塘内可查看塘内/当前钓位鱼实体与钓位数据，选中鱼可强制上钩，便于联调咬钩与生态。

### 非目标

- 替代 Admin Web A2 钓鱼概率面板（完整塘报表仍以 Admin 为准）
- Release 默认暴露（仍受 `GameplayDebugGate` / `GAMEPLAY_DEBUG` 约束）
- 编辑鱼属性、迁徙、补鱼等写操作（本票只读列表 + 强制上钩）

---

## 2. Overlay 布局

| 项 | 要求 |
|----|------|
| 列布局 | **3 列**：左=原有 Debug 动作 + 查看入口；中=鱼列表 / 钓位摘要入口结果；右=选中鱼属性或钓位详细数据 |
| 宽度 | 相对 STEAM-DESKTOP-12 弹窗（360）**×1.3** → **468** |
| 高度 | 可滚动；建议 MaxHeight ≥ 520 |

### 左列入口（新增）

| 按钮 | 行为 |
|------|------|
| 查看当前鱼塘的鱼 | 拉取当前所在塘全部 `pond_fish`，中列列表；未进塘提示 |
| 查看当前钓位的鱼 | 拉取当前 `ownSpotId` 上的鱼；无钓位提示 |
| 查看钓位数据 | 拉取当前钓位 bite/spot 调试数据（倍率、pBite、贡献列表等），右列展示 |

### 中列 / 右列

- 中列：鱼条目（种名/品质/体长/短 id）；点选后右列展示**完整属性**
- 右列鱼属性至少含：`id`、`pondId`、`spotId`、`speciesId`、种名、`quality`、`sizeM`、`bornAt`、`generation`、`biteMultiplier`、`escapeMultiplier`、`birthSizeM`、是否近体型上限
- 右列底部：**强制上钩**（仅选中鱼时可用）

---

## 3. 强制上钩规则

| 条件 | 行为 |
|------|------|
| 玩家在塘且 `fishingPhase === waiting` | 以选中 `fishId` 直接进入 hooked（不掷 miss；Debug 默认 `escaped=false`） |
| 非 waiting / 有 pending catch / 鱼不属于当前塘 | 中文失败提示，不静默 |
| 鱼可来自塘内任意钓位 | 允许「查看塘内鱼」后强制上钩，不要求鱼在当前钓位 |

收杆时长仍走正式 `calcHookDurationMs`（可受 instant test / scale 影响）。

---

## 4. 技术要点

| 类型 | 名称 | 说明 |
|------|------|------|
| REST GET | `/api/debug/gameplay/pond-fish?scope=pond\|spot` | 鉴权 + Debug 开关；返回鱼列表 |
| REST GET | `/api/debug/gameplay/spot-stats` | 当前钓位 + 塘 summary 片段 |
| REST POST | `/api/debug/gameplay` | 扩展 `action=force_bite` + `fishId` |
| IPC | Overlay ↔ Unity | `gameplay_debug` text：`list_pond_fish` / `list_spot_fish` / `spot_stats` / `force_bite:<id>`；state 带 debug 载荷 |

---

## 5. 验收标准

- [x] 任意已进塘场景：Debug 弹窗为 **3 列**，宽度约 **468**
- [x] 「查看当前鱼塘的鱼」列出塘内鱼；点选可见完整属性
- [x] 「查看当前钓位的鱼」仅列当前钓位鱼；无钓位有提示
- [x] 「查看钓位数据」可见当前钓位倍率 / pBite / 贡献等
- [x] waiting 下对列表中任意塘内鱼「强制上钩」进入 hooked；非 waiting 有明确错误
- [x] Release / 未开 Debug 时入口与接口仍拒绝

---

## 6. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-25 | 策划 | 立项：Overlay Debug 鱼塘/钓位查看与强制上钩；3 列 ×1.3 宽 |
| 2026-08-26 | 开发 | Overlay/Unity/服务端落地；`verify:steam-desktop-12b` 通过 → **已实现** |
