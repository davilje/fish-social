# Phase 2 开发需求交接包（Kickoff）

| 字段 | 内容 |
|------|------|
| 状态 | **可交接** |
| 日期 | 2026-07-12 |
| 下一迭代权威 | [`Phase2-下一迭代开发计划.md`](./Phase2-下一迭代开发计划.md) |
| 后端 Prompt | [`phase2-backend-dev.prompt.md`](../prompts/phase2-backend-dev.prompt.md)（N1：ARC-09/10 · BUG-08 → N2：ARC-11） |
| 数据 Prompt | [`data-platform-phase2-dev.prompt.md`](../prompts/data-platform-phase2-dev.prompt.md)（**DP-C 起**） |
| 玩法 Prompt | [`feat05-c-phase-dev.prompt.md`](../prompts/feat05-c-phase-dev.prompt.md) |
| 风险 | [`Phase2-剩余事项设计与风险.md`](./Phase2-剩余事项设计与风险.md) |
| 看板 | [`../策划进度看板.html`](../策划进度看板.html) |

---

## 0. 已完成门禁（勿重做）

| 项 | 状态 |
|----|------|
| BUG-11 启动挂死 | **已实现** |
| ARC-06 Docker · ARC-07 JWT · ARC-08 gameState 拆分 | **已实现** |
| DP-A（OTel/Tap/业务健康）· DP-B（幂等/admin-web/MetricsStore） | **已实现** |

---

## 1. 本迭代要做什么（14 项）

| 轨 | 内容 | 顺序 |
|----|------|------|
| **工程 N1→N2** | ARC-09 日志 · ARC-10 安全 · BUG-08 Modal → ARC-11 CI | 先安全护栏 |
| **数据 DP-C→D** | D-L3-02~10 日报/对照/BI/合规 | 可与工程并行 |
| **玩法 FEAT-05** | C1+C7 → C5 → C6→C2 → C3（C4 不做） | ARC-11 起步后再开 C6 |

---

## 2. 推荐启动口令

```
@docs/planning/prompts/phase2-backend-dev.prompt.md 先 ARC-09，并行 ARC-10 与 BUG-08，再 ARC-11
```

```
@docs/planning/prompts/data-platform-phase2-dev.prompt.md 从 DP-C 开始（D-L3-02 + D-L3-09）
```

```
@docs/planning/prompts/feat05-c-phase-dev.prompt.md 从 C1+C7 开始（建议 ARC-11 起步后开 C6）
```

---

## 3. 变更记录

| 日期 | 变更 |
|------|------|
| 2026-07-12 | ARC-08/DP-B 已实现后：Kickoff 指向下一迭代；N1/N2 + DP-C + FEAT-05 |
| 2026-07-12 | DP-A 已实现；剩余事项设计定稿 |
| 2026-07-12 | BUG-11 门禁通过 |
