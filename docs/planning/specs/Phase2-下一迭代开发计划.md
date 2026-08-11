# Phase 2 — 下一迭代开发计划（ARC-08 / DP-B 之后）

| 字段 | 内容 |
|------|------|
| 状态 | **已确认** |
| 设计时间 | **2026-07-12** |
| 前置 | ARC-06/07/08 · DP-A · DP-B · BUG-11 **均已实现** |
| 开放范围 | **14 项已确认**：ARC-09/10/11 · BUG-08 · FEAT-05 · D-L3-02~10 |
| 不做 | R2-3（已文档化）· FEAT-06/C4 遗传 · 千人多机扩容（阶段4，需另立项） |
| 关联 | [`Phase2-剩余事项设计与风险.md`](./Phase2-剩余事项设计与风险.md) · [`C-调优与状态机.md`](./C-调优与状态机.md) · [`数据平台-Phase2-稳定增长.md`](./数据平台-Phase2-稳定增长.md) |

---

## 1. 进度快照（排期起点）

| 轨道 | 已完成 | 本迭代要做 |
|------|--------|------------|
| 工程 | Docker · JWT · gameState 拆分 | **ARC-09 → 10 → 11** · **BUG-08** |
| 数据 | Phase0/1 · DP-A · DP-B（幂等/admin-web/MetricsStore） | **DP-C**（L3 日报）→ **DP-D**（BI/合规） |
| 玩法 | A/B 期 | **FEAT-05**：C1+C7 → C5 → C6→C2 → C3 |

容量看板：阶段1（单店内测）已基本达成；本迭代目标推进 **阶段2（安全+测试）** 与 **阶段3（L3 日报）**；阶段4（千人）仍未立项。

---

## 2. 双轨 Sprint（严格顺序）

```
Track P 工程（约 8d）              Track D 数据（约 17d）
  N1  ARC-09 统一日志               DP-C1  D-L3-02 对照 + D-L3-09 索引
  N1  ARC-10 限流/连接 ║ BUG-08     DP-C2  D-L3-03/04/05 留存·经济·生态
  N2  ARC-11 单测 + CI              DP-C3  D-L3-07/08 版本·告警
                                    DP-D   D-L3-06 BI · D-L3-10 合规

Track G 玩法 FEAT-05（约 18d，错峰）
  建议：ARC-11 起步后再开 C6；C1/C7/C5 可与 N1~N2 后半并行
  G1 C1 热更 + C7 灰度
  G2 C5 图鉴
  G3 C6 状态机动画 → C2 Bot
  G4 C3 Sink（模拟通过或稳定后再做）
```

**原则**：不要在 ARC-10/11 未稳时并行大改 C6；DP-C 不依赖 PG 切读，可立即开。

---

## 3. 需求要点与验收

### 3.1 Track P — 工程

| 编号 | 要点 | 验收 |
|------|------|------|
| ARC-09 | 业务路径废弃裸 `logInfo`/`logWarn`，统一 `logStructuredEvent`；指标走 metrics | handler 无裸 log；关键路径带 correlationId；`verify:server-observability` |
| ARC-10 | Rate limit（全局/鉴权）、Max connections≈200、`dev-token` 仅 localhost | 超限 429；外网 dev-token 失败；连接满拒绝有日志 |
| ARC-11 | vitest：状态机/timer/幂等；GitHub Actions 跑 verify 子集 | PR CI 绿；本地 `verify:engineering` 扩展或新 `verify:ci-smoke` |
| BUG-08 | 背包/商店/图鉴 Modal 打开时会话计时不冻 | 手工 + `verify:session-timer-broadcast` |

细节：[`phase2-开发计划.md`](./phase2-开发计划.md) §3.4~3.7。

### 3.2 Track D — L3（DP-C / DP-D）

| 编号 | 要点 | 验收 |
|------|------|------|
| D-L3-02 | 线上日均 vs 模拟日均对照 | Admin/analytics 可看偏差% |
| D-L3-03 | D1/D7 · session 时长 | 可输出上周 cohort |
| D-L3-04 | faucet/sink 日报 | 连续 3 日失衡可标红 |
| D-L3-05 | 各塘人口/品质日报 | 人口率&lt;70% 标红 |
| D-L3-07 | 日报含 rulesVersion | 可对比调参前后 |
| D-L3-08 | daily 后阈值 Webhook | 模拟超标触发通知 |
| D-L3-09 | analytics index 含 live-daily | 可打开最新线上日报告 |
| D-L3-06 | 日导 CSV/Parquet（可选 BI） | 运营可拖 30 日图 |
| D-L3-10 | 删号匿名化 · 导出 API | timeline 不可还原身份 |

细节：[`数据平台-Phase2-稳定增长.md`](./数据平台-Phase2-稳定增长.md) §4 DP-C/D。  
协作：`@data-analyst` 出报告模板；`@backend-dev` 接管道与 API。

### 3.3 Track G — FEAT-05 子任务（C4 不做）

| 子任务 | 优先级 | 依赖 | 验收摘要 |
|--------|--------|------|----------|
| C1 热更配置 | P1 | — | 改配置无需重启；审计可查 |
| C7 灰度指标 | P1 | 可与 C1 并行 | 灰度开关有指标 |
| C5 图鉴 | P1 | B0/B1 已有 | 个人图鉴+首次解锁 |
| C6 状态机动画 | P1 | C1；建议 ARC-11 | 阶段动画+hookEndsAt |
| C2 Bot 适配 | P1 | C6 | 真/Bot 混合塘 Bot 占比≤40% |
| C3 Sink | P2 | 模拟或上线稳定 | 30 日金币波动≤±15% |

权威：[`C-调优与状态机.md`](./C-调优与状态机.md)。C6 动画规格：[`状态机需求描述.md`](./状态机需求描述.md)。

---

## 4. 建议两周排期（给项目经理）

| 周 | Track P | Track D | Track G |
|----|---------|---------|---------|
| **W1** | ARC-09 · ARC-10 · BUG-08 | DP-C1（02+09）起步 | — |
| **W2** | ARC-11 | DP-C2（03/04/05） | C1+C7 起步 |
| **W3+** | 护栏维护 | DP-C3 → DP-D | C5 → C6→C2 → C3 |

---

## 5. 开发交接口令

**工程轨（先做）：**

```
@docs/planning/prompts/phase2-backend-dev.prompt.md 按下一迭代：先 ARC-09，并行 ARC-10 与 BUG-08，再 ARC-11
```

**数据轨（可并行）：**

```
@docs/planning/prompts/data-platform-phase2-dev.prompt.md 从 DP-C 开始实现（D-L3-02 → … → DP-D）
```

**玩法轨（ARC-11 起步后）：**

```
@docs/planning/prompts/feat05-c-phase-dev.prompt.md 从 C1+C7 开始，严格按 C 文档顺序；C4 不做
```

---

## 6. 变更记录

| 日期 | 变更 |
|------|------|
| 2026-07-12 | 初稿：以 ARC-08/DP-B 已实现为起点；14 项排入 N1/N2 + DP-C/D + FEAT-05；设计时间 2026-07-12 |
| 2026-08-10 | **收尾验收**：ARC-06～11 → **已实现**（`verify:deploy` · `verify:engineering`；ARC-11 门禁改为 isFishingActive 语义对齐）；D-L3-02/09 → **已实现**（`verify:data-platform-dp-c` + 日批 live-daily 样本）；风险：线上日捕获量现为 0、warehouse/latest 的 dateKey 可能落后最新 daily |
