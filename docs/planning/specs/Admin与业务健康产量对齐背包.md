# Admin 与业务健康产量对齐背包

| 项 | 内容 |
|----|------|
| 状态 | **已实现** |
| 优先级 | **P0** |
| 编号 | **OPS-CATCH-1.1** |
| 设计时间 | **2026-07-19** |
| 完成时间 | **2026-07-19** |
| 目标版本 | hotfix |
| 前置 | [`看板产量人机分列-背包口径.md`](./看板产量人机分列-背包口径.md)（**OPS-CATCH-1 已实现**） |
| 关联 | `adminPlayersOverview` · `businessHealth` · `aggregate-daily-metrics` · `inventory` |

---

## 1. 背景

OPS-CATCH-1 已让运营平台 / 日报 / 增长看板按 **`inventory` 入库**计产量（含 bot）。  
Admin 玩家「钓获」与业务健康仍用 `catch_accept` / `pending_catch_accept` → bot 恒为 0，与入口总量矛盾。

**北极星**：Admin 个人钓获、业务健康日/塘产量与 OPS-CATCH-1 **同一口径**（背包入库）。

---

## 2. 口径（与 OPS-CATCH-1 一致）

| 项 | 约定 |
|----|------|
| 1 次产量 | `inventory` 一行，`caught_at` 落在统计窗口 |
| 真人 / bot | `player_id NOT LIKE 'bot-%'` / `LIKE 'bot-%'`（含池化 `bot-pool-*` 等前缀，统一 `bot-%`） |
| 时区 | 业务健康按日：`Asia/Shanghai` 自然日（与日报一致；修正若仍用 UTC `dateKeyOffset`） |

---

## 3. 功能要求

### 3.1 A — Admin 玩家一览 `catchCount`

| 项 | 要求 |
|----|------|
| 现状 | `SUM(catch_accept/pending_catch_accept)` from `fishing_metrics` |
| 目标 | 窗口内 `COUNT(*)` from `inventory` WHERE `caught_at >= since`（与 overview 的 hours 一致） |
| bot | 必须计入；列表筛「含 bot」时 bot 行钓获 >0（有入库时） |
| 上钩/断线 | 可仍用 metrics（本需求不强制改） |

### 3.2 B — `daily_pond_stats` / `daily_player_stats` 聚合

| 项 | 要求 |
|----|------|
| 脚本 | `aggregate-daily-metrics.mjs`（及日批调用链） |
| `catch_count` | 改为按 `inventory` 聚合（塘：优先 `inventory.pond_id`；无则沿用 OPS-CATCH-1 时间线对齐策略） |
| `daily_player_stats.catch_count` | 按玩家背包入库计数（**含 bot**，便于 Admin/健康一致；活跃人数若原排除 bot 则保持排除） |
| hook/escape | 仍可来自 metrics |

### 3.3 C — 业务健康 API / UI

| 项 | 要求 |
|----|------|
| 数据源 | 读已对齐的 `daily_pond_stats`（或 API 内直接查 inventory 按上海日，二选一；推荐改聚合表以免双口径） |
| 展示 | 日总钓获、分塘钓获含 bot；可选副文案「含机器人」或人机分列（有则更好，非必须） |
| 获鱼率 | `catch / hook`：分子改为背包产量后，分母仍为 `bite_hook`（含 bot 钩）时比率才有意义——**保持 hook 含 bot**（现状塘聚合未剔 bot） |

### 3.4 历史日

- 改聚合后，需能对近 N 日 **重跑** `aggregate-daily-metrics`（或日批）刷新 `catch_count`；不强制改已生成 HTML 报告（与 OPS-CATCH-1「看板文件」约束不冲突：本条改的是 Admin 实时读的 SQLite 聚合表）。

---

## 4. 非目标

- 不强制 bot 补写 `catch_accept` 埋点  
- 不改游戏内钓鱼逻辑  
- 不改 DAU/留存排除 bot 的规则  

---

## 5. 验收

- [x] Admin 玩家表：有背包入库的 bot，`catchCount` > 0  
- [x] 业务健康近几日总钓获 ≈ 运营平台/日报同日背包总量（人机合计）  
- [x] 分塘产量含 bot（在有 `pond_id` 或可对齐时）  
- [x] verify 覆盖：overview / business-health 或聚合脚本断言 inventory 口径  
- [x] spec → 已实现；`npm run planning:master-xlsx`  

---

## 6. 派发

提示词：[`../prompts/admin-business-health-catch-inventory-dev.prompt.md`](../prompts/admin-business-health-catch-inventory-dev.prompt.md)

```text
@docs/planning/prompts/admin-business-health-catch-inventory-dev.prompt.md 按此实现 OPS-CATCH-1.1
```

---

## 7. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-19 | **已确认**：Admin + 业务健康产量对齐 inventory |
| 2026-07-19 | **已实现**：overview/聚合/健康 catch←inventory；上海日；`verify:ops-catch-inventory-admin` |
