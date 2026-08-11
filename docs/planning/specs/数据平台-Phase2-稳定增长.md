# 数据平台 Phase 2 — 稳定增长（需求设计）

| 字段 | 内容 |
|------|------|
| 状态 | **DP-A 已实现** |
| 编号范围 | D-L1-10 · D-L1-12 · D-L2-04 · D-L2-09 · D-L2-10 · D-L2-14 · D-L3-02~10 |
| 目标版本 | v0.8.x（数据平台稳定增长）· 嵌入产品 Phase 2 Sprint 5+ |
| 设计时间 | **2026-07-12** |
| 前置 | Phase 0/1 数据平台 **已实现** · BUG-11 **已实现** · 权威表 `项目开发需求计划表.xlsx` |
| 关联 | [`三层数据体系-可观测性补充-v0.6.md`](./三层数据体系-可观测性补充-v0.6.md) · [`phase2-开发计划.md`](./phase2-开发计划.md) · 三层 xlsx 阶段路线图 |

---

## 1. 与历史路线图对照（进度结论）

早期约定的三层阶段 vs **当前计划表**：

### 1.1 Phase 0（上线前）— **全部已实现**

| 原计划 | 表中状态 |
|--------|----------|
| D-L1-01/03/05/08 · D-L2-01/02/03/11 · D-L3-01 | **已实现** |
| 实时排障最小闭环 L1-06 + L2-06 | **已实现**（落在 Phase 1 批次） |

### 1.2 Phase 1（内测~小规模商用）— **主体已实现；L3 日报批延后**

| 原计划 | 表中状态 | 说明 |
|--------|----------|------|
| D-L1-04 Loki · D-L1-06/07 链路告警 | **已实现** | — |
| D-L2-05~07 Grafana/Admin/RBAC | **已实现** | — |
| D-L2-09 业务健康看板 | **未开始** → 本设计纳入 Sprint DP-A | 原 Phase 1，现并入稳定增长 |
| D-L2-12 客户端上报 · D-L2-13 Live Inspector · D-L1-11 动态采样 | **已实现** | 原「Phase C 补充」已提前完成 |
| D-L1-09 日志合规 | **已实现**（记在 Phase 1） | 原 Phase 2，已提前 |
| D-L3-02~05,07~09 线上对照与日报 | **未开始** | 原 Phase 1 L3，**整体延至本 Phase 2** |

### 1.3 Phase 2（稳定增长）— **本设计范围（15 项 → 已确认）**

| 编号 | 标题（与三层清单对齐） | 优先级 | Sprint |
|------|------------------------|--------|--------|
| D-L1-10 | OpenTelemetry 分布式追踪 | P1 | DP-A |
| D-L1-12 | Socket 全事件 Tap | P1 | DP-A |
| D-L2-09 | 业务健康看板（咬钩/断线/分塘） | P1 | DP-A |
| D-L2-10 | 关键事件 eventId 幂等 | P1 | DP-B |
| D-L2-04 | Metrics 存储演进（PG/Timescale） | P2 | DP-B |
| D-L2-14 | 独立 admin-web 运维台 | P1 | DP-B |
| D-L3-02 | 线上实测 vs 模拟对照 | P1 | DP-C |
| D-L3-03 | 留存与会话时长分析 | P1 | DP-C |
| D-L3-04 | 经济 faucet/sink 日报 | P1 | DP-C |
| D-L3-05 | 鱼塘生态健康日报 | P1 | DP-C |
| D-L3-07 | 规则版本切分分析 | P1 | DP-C |
| D-L3-08 | 运营指标告警 | P1 | DP-C |
| D-L3-09 | analytics 索引纳入线上日报 | P1 | DP-C |
| D-L3-06 | BI / 数仓对接 | P2 | DP-D |
| D-L3-10 | 用户数据导出/删除与指标脱敏 | P2 | DP-D |

> **命名勘误**：计划表旧称「用户模型分析 / 客服会话 / 渠道报表…」与三层清单不一致；本设计起 **以本节标题为准**，并回写计划表。

### 1.4 不在本设计（产品 Phase 2 其它轨）

已实现：ARC-06 Docker · ARC-07 Mobile JWT。  
仍走 [`phase2-开发计划.md`](./phase2-开发计划.md)：ARC-08~11 · FEAT-05(C 期) · BUG-08。与数据平台 Sprint **可并行**，互不阻塞 DP-A。

---

## 2. 目标与非目标

### 2.1 目标

- 排障从「查日志/timeline」升级到 **可追踪 span + Socket 全量采样**
- 运营可看 **线上 vs 模拟、留存、经济、生态** 日报，并与 `docs/analytics` 打通
- Metrics 可 **幂等**、可演进到 **PG**；Admin 有独立 **admin-web**
- 商用前补齐 **BI 导出** 与 **用户数据删除/脱敏**

### 2.2 非目标

- 不重做 Phase 0/1 已交付能力
- 不做 FEAT-05-C4 繁衍遗传
- 不把 OTel/Live Inspector 替代 L3 日报（Live Inspector 已交付；OTel 是补充）

---

## 3. Sprint 安排

```
DP-A（约 6d）实时排障深化 + 业务看板
  D-L1-10 OTel · D-L1-12 Socket Tap · D-L2-09 业务健康看板

DP-B（约 10d）存储与运维台
  D-L2-10 幂等 · D-L2-14 admin-web · D-L2-04 PG（可后置）

DP-C（约 12d）L3 线上分析（原 Phase 1 欠账）
  D-L3-02/03/04/05/07/08/09

DP-D（约 5d）BI + 合规
  D-L3-06 · D-L3-10
```

建议嵌入产品 Phase 2：**Sprint 5 = DP-A + DP-B 前半**；L3（DP-C/D）由 `@data-analyst` + `@backend-dev` 贯穿。

---

## 4. 需求要点（验收摘要）

### DP-A

| 编号 | 建设要点 | 验收 |
|------|----------|------|
| D-L1-10 | OTLP → Jaeger/Tempo；join→bite→disconnect span；trace_id≈correlationId | UI 可按 correlationId 看 span 链 |
| D-L1-12 | onAny/onAnyOutgoing；生产 1% / debug 目标 100%；payload 脱敏截断 | debug 模式可见任意 client emit |
| D-L2-09 | 基于 daily_*：日钓量、断线率、bite hit/miss、分塘人口 | Admin/Grafana 可看 7 日趋势 |

### DP-B

| 编号 | 建设要点 | 验收 |
|------|----------|------|
| D-L2-10 | catch/pending_accept 带业务 eventId；聚合 DISTINCT | 重复 accept 不双计 |
| D-L2-14 | Vite admin-web：timeline / fishing-debug / Live Inspector | 桌面可完成 SOP |
| D-L2-04 | MetricsStore 抽象；PG/Timescale 双写验证后切读 | 压测写入不丢；timeline &lt;500ms |

### DP-C

| 编号 | 建设要点 | 验收 |
|------|----------|------|
| D-L3-02 | 模拟日均 vs 线上日均对照页 | 可展示偏差% |
| D-L3-03 | D1/D7 · session 时长分布 | 可输出上周 cohort |
| D-L3-04 | 日 faucet/sink 曲线 | 连续 3 日失衡可告警 |
| D-L3-05 | 各塘人口/品质日报 | 人口率&lt;70% 标红 |
| D-L3-07 | metrics/日报含 rulesVersion | 可对比调参前后 |
| D-L3-08 | daily 后阈值 Webhook | 模拟超标触发通知 |
| D-L3-09 | analytics index 含 live-daily | 可打开最新线上日报告 |

### DP-D

| 编号 | 建设要点 | 验收 |
|------|----------|------|
| D-L3-06 | 日导 CSV/Parquet；Metabase/Superset 可选 | 运营可拖 30 日图 |
| D-L3-10 | 删号匿名化 metrics；导出 API | timeline 不可还原身份 |

详细步骤仍以 [`scripts/build-data-platform-roadmap-xlsx.py`](../../../scripts/build-data-platform-roadmap-xlsx.py) 各需求 sheet 为准。

---

## 5. 开发交接

| 角色 | Prompt |
|------|--------|
| 后端 | [`data-platform-phase2-dev.prompt.md`](../prompts/data-platform-phase2-dev.prompt.md) |
| 数据分析 | DP-C/D 与 `@data-analyst` 协作；读本 spec §4 DP-C/D |
| 前端 | D-L2-14 admin-web 可 `@frontend-dev` 并行 |

```
@docs/planning/prompts/data-platform-phase2-dev.prompt.md 从 DP-B 开始实现
```

---

## 6. 计划表时间字段

| 动作 | 设计时间 | 完成时间 |
|------|----------|----------|
| 本需求集确认（今日） | **2026-07-12** | （空） |
| 各编号验收通过日 | 保留 2026-07-12 | 填验收当日 |

---

## 7. 变更记录

| 日期 | 变更 |
|------|------|
| 2026-07-12 | **DP-B 已实现**：D-L2-10 幂等 · D-L2-14 admin-web · D-L2-04 MetricsStore；`verify:data-platform-dp-b` |
| 2026-07-12 | DP-D **已实现**：D-L3-10 export/erase · D-L3-06 warehouse CSV · `verify:data-platform-dp-d` |
| 2026-07-12 | DP-D 专文定稿：仅剩 D-L3-06/10；见 [`数据平台-DP-D-BI与合规交接.md`](./数据平台-DP-D-BI与合规交接.md) |
| 2026-07-12 | **DP-A 已实现**：D-L1-10 OTel · D-L1-12 Socket Tap · D-L2-09 业务健康看板；`verify:data-platform-dp-a` |
| 2026-07-12 | 初稿：对照历史 Phase0/1/2 与计划表；15 项 → **已确认**；Sprint DP-A~D；修正 L3 命名 |
| 2026-08-10 | **DP-C1 收尾**：D-L3-02 / D-L3-09 → **已实现**；`verify:data-platform-dp-c`；manifest 含多日 `live-daily`；sim 取自真实 runs（非长期 fallback）；风险：近期 live catches=0（环境无产量）、warehouse/latest dateKey 可能滞后 |
