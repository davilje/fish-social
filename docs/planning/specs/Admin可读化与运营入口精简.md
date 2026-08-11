# Admin 可读化与运营入口精简

| 项 | 内容 |
|----|------|
| 状态 | **已实现** |
| 优先级 | **P0** |
| 编号 | **ADMIN-OBS-1.3** |
| 设计时间 | **2026-07-14** |
| 完成时间 | **2026-07-14** |
| 目标版本 | hotfix |
| 前置 | [`Admin-排障增强-v1.2.md`](./Admin-排障增强-v1.2.md)（**已实现**）· [`运营平台入口体验增强.md`](./运营平台入口体验增强.md)（**OPS-UX-1 已实现**） |
| 关联 | D-L2-15/16 · `METRICS_SKIP_BOTS` · `运营平台.html` · `admin-web/` |

---

## 1. 背景与问题

OPS-UX-1 / Admin v1.1–1.2 已能连上排障，但日常使用仍吃力：

| # | 痛点 | 表现 |
|---|------|------|
| P1 | Admin 大量 **JSON 原文** | Timeline `summary`、业务健康 `totals` 等用 `<pre>` 甩 JSON，策划读不动 |
| P2 | 有效字段埋在 payload 里 | 钓获数、钓鱼时长、断线次数等要人肉拆 JSON |
| P3 | Bot 相位刷埋点 | bot 状态机仍写 `fishing_phase_transition`，污染库与 Timeline |
| P4 | Timeline **必须先填 playerId** | 不能默认看到「全员摘要」，也无法先筛再精查 |
| P5 | 运营平台「实时运维」过载 | 长说明 + 5 个快捷按钮 + playerId 双按钮 + 五页签百科 + 新标签全屏卡 + 内嵌，重复且劝退 |

**北极星**：打开 Admin（经运营平台内嵌）→ 默认看到**真人玩家一览表** → 可筛选/点选精准用户 → 明细是表格不是 JSON 墙；入口页实时区**只剩内嵌 Admin**。

---

## 2. 目标用户

| 角色 | 诉求 |
|------|------|
| 策划 | 一眼看谁钓了多少、钓多久、断线几次；少碰 bot |
| 开发 | 点进某人再看 Timeline / Live；表格列稳定可对照 |
| 运维 | 运营平台少选择，打开就能用 Admin |

---

## 3. 功能范围

### 3.1 A — 运营平台「实时运维」精简（必须）

**决策：保留 Admin 内嵌，删除其余实时区冗余。**

| 删除 | 保留 |
|------|------|
| 大段「排障…密钥…bat」说明（可缩成内嵌条上一行） | **Admin iframe 内嵌**（主操作面） |
| 快捷按钮：鱼塘概览 / Timeline / Live / Debug / 业务健康 | 内嵌顶栏「全屏打开」**一个**小链（可选，不单独占卡片） |
| playerId 输入 + 打开 Timeline / Live | —（改在 Admin 内搜索） |
| 「Admin 五个页签」指南块 | —（Admin 内自解释） |
| 「Admin 运维台 · 新标签全屏」独立卡片 | — |
| 游戏 Web 卡 | **移出**实时运维区：可放到「更多」折叠，或今日运维旁极简链；不得与 Admin 并排抢视线 |

文案约束：实时区标题可保留「实时运维」，副文案 ≤ 1 行，例如：「需游戏服 :3001 · 密钥见内嵌页」。

验收：

- [ ] 实时运维区视觉上只有：短提示（可选）+ 内嵌 Admin（+ 可选全屏小链）  
- [ ] 不再出现五个快捷按钮、playerId 双开、五页签说明、全屏大卡  
- [ ] `verify:ops-portal-links` / OPS-UX 相关断言同步改掉对已删 UI 的依赖  

> 说明：OPS-UX-1 的 Admin 深链能力**保留在 Admin URL 契约上**（`?tab=&playerId=`），供日报/书签使用；**入口页不再暴露**深链控件。

---

### 3.2 B — Admin：默认全员表 + 筛选 + 精准查找（必须）

#### B1. 新默认页：玩家一览（建议作首页 / 新 tab「玩家」）

进入 Admin 默认打开 **玩家一览**（可替换原默认 `ponds`，或新增 tab `players` 并默认选中——实现二选一，**推荐新 tab 且默认**）。

| 列（中文） | 含义 | 备注 |
|------------|------|------|
| 昵称 | 展示名 | 可空则 — |
| playerId | 精准 ID | 可复制 |
| 鱼塘 | 当前/最近 pondId | |
| 相位 | fishingPhase | 中文标签可选 |
| 本局时长 | sessionFishingMs | 格式化 `mm:ss` / `XhYm` |
| 近 N 小时钓获 | catch / accept 计数 | N 默认 24，可调 |
| 近 N 小时断线 | disconnect 相关计数 | |
| 近 N 小时上钩 | bite_hook 等 | 可选列 |
| 类型 | 真人 / 机器人 | |
| 操作 | 「Timeline」「Live」「Debug」 | 跳转已有页并带 playerId |

数据：新增或扩展 Admin API，例如：

`GET /api/admin/players/overview?hours=24&humansOnly=1`

返回行数组；聚合来自 `fishing_metrics`（及可选内存态合并当前相位/时长）。

#### B2. 筛选

| 筛选项 | 行为 |
|--------|------|
| 仅真人（默认 **开**） | 隐藏 bot 行 |
| 鱼塘 | 下拉多塘 / 全部 |
| 相位 | 可选 |
| 时间窗 hours | 默认 24，与聚合一致 |

#### B3. 精准查找

- 顶部搜索框：支持 **playerId 精确**、昵称 **包含**（至少一种精确 ID）。  
- 回车 / 按钮过滤表格；若唯一命中可高亮。  
- 保留「打开 Timeline」：填 ID 后跳转 Timeline tab（深链）。

#### B4. 空态

无玩家：说明「近 N 小时无埋点 / 无在塘用户」，勿只甩 JSON 错误。

验收：

- [ ] 连接成功后默认可见玩家表（无需先填 ID）  
- [ ] 默认不展示 bot（可关掉「仅真人」查看）  
- [ ] 精确 playerId 可滤到单行并进 Timeline/Live  
- [ ] 表格列含时长、钓获、断线等，**无整页 JSON summary**  

---

### 3.3 C — Admin：去掉 JSON 墙，改可读表/卡片（必须）

凡 Admin 面向人的摘要，**禁止**用大块 `JSON.stringify` 作为主展示（调试「原始 JSON」可折叠到最底，默认收起）。

| 页面 | 现状 | 目标 |
|------|------|------|
| 玩家 Timeline | `summary` 为 `<pre>` JSON；事件 payload 整段 JSON | **摘要区**：卡片/表 — 钓获、上钩、脱钩、断线、进塘失败等计数；事件表列：时间、事件中文名、鱼塘、**关键字段摘要**（非整包 JSON） |
| 业务健康 | `totals` 为 JSON | **汇总卡**：总钓获、平均断线率、活跃等；日表保持并补中文表头 |
| Live Inspector | 部分已是卡片 | 巩固卡片；最近事件同 Timeline 摘要列规则 |
| Fishing Debug | 已有表则保持 | activeFishers 列继续中文；禁止新增 JSON 墙 |
| 鱼塘概览 | 已有表 | 保持；可链到该塘玩家筛选 |

**事件摘要列规则（Timeline / Live 最近事件）**：

- 优先展示 schema/文档中的核心字段（如 `sessionFishingMs`、`reason`、`joinKind`、`f/t/c` 短码译回相位名）。  
- 单格文本建议 ≤ 80 字；完整 payload 放「详情」展开或 title 悬停。

验收：

- [ ] Timeline / 业务健康主路径无大块 JSON  
- [ ] 摘要区可见钓获、时长相关、断线等数字  
- [ ] 事件行可读  

---

### 3.4 D — Bot 不再写相位切换埋点（必须）

| 项 | 要求 |
|----|------|
| 范围 | `fishing_phase_transition`（及非法跳转 `phase_transition_invalid` 若来自 bot） |
| 默认 | **bot 不落库**（不写 `fishing_metrics`） |
| 日志 | 建议 bot 相位也不打 info 结构化日志（或仅 `ECOLOGY_VERBOSE`/debug 级）；避免控制台刷 bot |
| 开关 | 可选 `METRICS_BOT_PHASE=1` 恢复 bot 相位埋点（压测对齐用）；**默认关** |
| 与 `METRICS_SKIP_BOTS` | 可并存：本需求至少保证相位类默认跳过 bot；全量跳过 bot 仍可由现有 env 控制 |

验收：

- [ ] 真人相位仍有 transition 埋点  
- [ ] 默认 bot 压测/挂机不增加 `fishing_phase_transition` 行  
- [ ] 埋点表 / schema 说明同步（metrics-catalog-sync）  

---

## 4. 非目标

- 不重做运营日报 / BI CSV  
- 不删除 Admin 五个能力页签本身（只改默认页与展示）  
- 不在本需求做诊断包 zip（仍属 ADMIN-OBS-1.2 P2）  
- 不改客户端游戏 UI  

---

## 5. 信息架构（Admin 目标）

```
Admin
├── 玩家（默认）← 一览表 + 筛选 + 搜索
├── 鱼塘概览
├── 玩家 Timeline   ← 表格式摘要 + 事件表
├── Fishing Debug
├── 业务健康        ← 卡片汇总 + 日表
└── Live Inspector
```

运营平台：

```
…今日运维 / 每日必看…
实时运维
  └── [短提示]
  └── Admin iframe
  └── （可选）全屏 ↗
…更多（含游戏 Web 等）…
```

---

## 6. 涉及文件（预期）

| 区域 | 路径 |
|------|------|
| 入口 | `运营平台.html`（删减实时区）；`verify-ops-portal-links.ts` |
| Admin UI | `admin-web/src/App.tsx` · 新 `PlayersPage.tsx` · `TimelinePage.tsx` · `BusinessHealthPage.tsx` · Live/Debug 按需 |
| API | `server` Admin routes（players overview 聚合） |
| Bot 相位 | `fishingObservability.ts` / `recordPhaseTransition` · `fishingMetrics.ts` |
| 文档 | 本 spec · 埋点表 EVENTS 说明 |

---

## 7. 验收总表

| # | 标准 |
|---|------|
| 1 | 运营平台实时区仅内嵌 Admin（+ 可选全屏小链），无快捷/百科/双开 |
| 2 | Admin 默认玩家一览表，含时长/钓获/断线等列 |
| 3 | 仅真人默认开；可筛塘；可精确查 playerId |
| 4 | Timeline / 业务健康主展示非 JSON 墙 |
| 5 | 默认 bot 无相位 transition 落库 |
| 6 | 相关 verify 更新且绿；计划表本项可标已实现 |

---

## 8. 实现顺序建议

1. **A** 入口精简（最快可见）  
2. **D** bot 相位停写（减噪、减库）  
3. **C** Timeline / 业务健康表格化（可先做、不依赖新 API）  
4. **B** players overview API + 默认页  

---

## 9. 派发

提示词：[`../prompts/admin-readable-ops-slim-dev.prompt.md`](../prompts/admin-readable-ops-slim-dev.prompt.md)

```text
@docs/planning/prompts/admin-readable-ops-slim-dev.prompt.md 按此实现 ADMIN-OBS-1.3
```

---

## 10. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-14 | **已确认**：Admin 表格化 + 全员默认/筛选 + bot 停写相位埋点 + 运营入口实时区只留内嵌 |
| 2026-07-14 | **已实现**：A/D/C/B；`verify:ops-portal-links`；METRICS_BOT_PHASE |
